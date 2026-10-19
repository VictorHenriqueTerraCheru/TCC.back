namespace Ocorrencias.Domain.Confianca;

/// <summary>
/// Motor de confiança (TCC, Seção 5.2). Funções puras: o instante atual e a configuração entram
/// como parâmetros. A API e o Simulator chamam exatamente estas funções.
/// </summary>
public static class MotorConfianca
{
    public static double PesoPerfil(Perfil perfil, ConfiguracaoConfianca cfg) => perfil switch
    {
        Perfil.Cidadao => cfg.PesoCidadao,
        Perfil.CidadaoVerificado => cfg.PesoVerificado,
        Perfil.Administrador => cfg.PesoAdministrador,
        _ => throw new ArgumentOutOfRangeException(nameof(perfil), perfil, "Perfil desconhecido."),
    };

    /// <summary>Bônus por proximidade; nunca menor que 1 (penalizar distância só faria o atacante desligar o GPS).</summary>
    public static double PesoProximidade(double? distanciaMetros, ConfiguracaoConfianca cfg)
    {
        if (!cfg.UsarProximidade || distanciaMetros is not { } d)
        {
            return 1;
        }

        if (d <= cfg.RaioBonusTotalMetros)
        {
            return cfg.BonusMaximo;
        }

        if (d <= cfg.RaioBonusZeroMetros)
        {
            var fracao = (d - cfg.RaioBonusTotalMetros) / (cfg.RaioBonusZeroMetros - cfg.RaioBonusTotalMetros);
            return cfg.BonusMaximo - fracao * (cfg.BonusMaximo - 1);
        }

        return 1;
    }

    /// <summary>
    /// <c>2^(−idade / meiaVida)</c>, com idade negativa tratada como zero. A meia-vida depende do uso:
    /// <see cref="ConfiguracaoConfianca.MeiaVidaConfianca"/> no índice, <see cref="ConfiguracaoConfianca.MeiaVidaCicloDeVida"/>
    /// no ciclo de vida.
    /// </summary>
    public static double FatorTemporal(TimeSpan idade, TimeSpan meiaVida, ConfiguracaoConfianca cfg)
    {
        if (!cfg.UsarDecaimento)
        {
            return 1;
        }

        var idadeEfetiva = idade < TimeSpan.Zero ? TimeSpan.Zero : idade;
        return Math.Pow(2, -(idadeEfetiva / meiaVida));
    }

    public static double PesoVoto(VotoFato voto, DateTimeOffset agora, TimeSpan meiaVida, ConfiguracaoConfianca cfg) =>
        PesoPerfil(voto.PerfilNoMomento, cfg)
        * PesoProximidade(voto.DistanciaMetros, cfg)
        * FatorTemporal(agora - voto.RegistradoEm, meiaVida, cfg);

    public static FaixaConfianca ClassificarFaixa(double indice, ConfiguracaoConfianca cfg) =>
        indice >= cfg.LimiarAlta ? FaixaConfianca.Alta
        : indice >= cfg.LimiarMedia ? FaixaConfianca.Media
        : FaixaConfianca.Baixa;

    /// <summary>Índice de confiança <c>C = 100 × E⁺ / (E⁺ + E⁻ + K)</c>, com a meia-vida da confiança.</summary>
    public static ResultadoConfianca CalcularConfianca(
        IReadOnlyCollection<VotoFato> votos, DateTimeOffset agora, ConfiguracaoConfianca cfg)
    {
        var (positivo, negativo) = SomarPesos(votos, cfg.MeiaVidaConfianca, agora, cfg);

        var denominador = positivo + negativo + cfg.K;
        var indice = denominador > 0 ? 100 * positivo / denominador : 0;

        return new ResultadoConfianca(indice, ClassificarFaixa(indice, cfg), positivo, negativo);
    }

    /// <summary>Avalia no máximo uma transição de status por chamada (o job roda a cada 15 min).</summary>
    public static EstadoProblema AvaliarCicloDeVida(
        EstadoProblema estado, IReadOnlyCollection<VotoFato> votos, DateTimeOffset agora, ConfiguracaoConfianca cfg)
    {
        switch (estado.Status)
        {
            case StatusProblema.Nova:
            {
                var votantes = votos.Select(v => v.UsuarioId).Distinct().Count();
                var saiDeNova = votantes >= cfg.VotantesParaSairDeNova
                    || agora - estado.CriadoEm >= cfg.TempoMaximoEmNova;

                return saiDeNova
                    ? estado with { Status = StatusProblema.Ativa, StatusAlteradoEm = agora }
                    : estado;
            }

            case StatusProblema.Ativa:
            {
                var (continua, resolvido) = SomarPesos(votos, cfg.MeiaVidaCicloDeVida, agora, cfg);
                var entra = resolvido >= cfg.PesoMinimoResolvido
                    && ProporcaoResolvido(continua, resolvido) >= cfg.ProporcaoResolvido;

                return entra
                    ? estado with
                    {
                        Status = StatusProblema.PossivelmenteResolvida,
                        StatusAlteradoEm = agora,
                        CondicaoResolucaoDesde = agora,
                    }
                    : estado;
            }

            case StatusProblema.PossivelmenteResolvida:
            {
                // Para permanecer basta a proporção: sem votos novos, R⁺ e R⁻ decaem juntos e p não muda.
                var (continua, resolvido) = SomarPesos(votos, cfg.MeiaVidaCicloDeVida, agora, cfg);

                if (ProporcaoResolvido(continua, resolvido) < cfg.ProporcaoResolvido)
                {
                    return estado with
                    {
                        Status = StatusProblema.Ativa,
                        StatusAlteradoEm = agora,
                        CondicaoResolucaoDesde = null,
                    };
                }

                var desde = estado.CondicaoResolucaoDesde ?? estado.StatusAlteradoEm;
                return agora - desde >= cfg.TempoConfirmacaoResolvida
                    ? estado with { Status = StatusProblema.Resolvida, StatusAlteradoEm = agora }
                    : estado;
            }

            case StatusProblema.Resolvida:
                return estado;

            default:
                throw new ArgumentOutOfRangeException(nameof(estado), estado.Status, "Status desconhecido.");
        }
    }

    private static (double Positivo, double Negativo) SomarPesos(
        IReadOnlyCollection<VotoFato> votos, TimeSpan meiaVida, DateTimeOffset agora, ConfiguracaoConfianca cfg)
    {
        double positivo = 0, negativo = 0;

        foreach (var voto in votos)
        {
            var peso = PesoVoto(voto, agora, meiaVida, cfg);
            if (voto.Valor == ValorVoto.Positivo)
            {
                positivo += peso;
            }
            else
            {
                negativo += peso;
            }
        }

        return (positivo, negativo);
    }

    /// <summary><c>p = R⁻ / (R⁺ + R⁻)</c>; 0 quando não há votos.</summary>
    private static double ProporcaoResolvido(double continua, double resolvido)
    {
        var total = continua + resolvido;
        return total > 0 ? resolvido / total : 0;
    }
}
