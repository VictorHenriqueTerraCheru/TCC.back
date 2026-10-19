using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Simulator.Modelo;

/// <summary>Estado ao fim de cada hora <c>h</c> (instante h + 1). <see cref="Status"/> 0 = problema ainda não existe.</summary>
public sealed class LinhaDoTempo(int horas)
{
    public StatusProblema[] Status { get; } = new StatusProblema[horas];
    public double[] Indice { get; } = new double[horas];

    /// <summary>Faixa visível ao público: em Nova (e antes de existir, ou já resolvido) não há faixa exibida.</summary>
    public FaixaConfianca? FaixaExibida(int hora, ConfiguracaoConfianca cfg) =>
        Status[hora] is StatusProblema.Ativa or StatusProblema.PossivelmenteResolvida
            ? MotorConfianca.ClassificarFaixa(Indice[hora], cfg)
            : null;
}

/// <summary>
/// Reproduz o que a API faz com cada voto (docs/diagramas/05_seq_votar.puml) e com o job de ciclo de vida,
/// chamando o mesmo <see cref="MotorConfianca"/> do Domain.
/// </summary>
public static class Avaliador
{
    public static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    public static DateTimeOffset Instante(double hora) => T0 + TimeSpan.FromHours(hora);

    /// <param name="intencoes">Ordenadas por hora.</param>
    public static LinhaDoTempo Avaliar(ProblemaSimulado problema, IReadOnlyList<Intencao> intencoes, ConfiguracaoConfianca cfg, int horas)
    {
        var linha = new LinhaDoTempo(horas);
        var votos = new Dictionary<Guid, VotoFato>();
        var criado = Instante(problema.CriadoHora);
        var estado = new EstadoProblema(StatusProblema.Nova, criado, criado, null);
        var proxima = 0;

        for (var h = 0; h < horas; h++)
        {
            var fimDaHora = h + 1.0;
            if (fimDaHora < problema.CriadoHora)
            {
                linha.Indice[h] = double.NaN;
                continue;
            }

            for (; proxima < intencoes.Count && intencoes[proxima].Hora < fimDaHora; proxima++)
            {
                var intencao = intencoes[proxima];
                if (intencao.Hora < problema.CriadoHora || !Aceita(intencao, problema, estado))
                {
                    continue;
                }

                // UPSERT por usuário, com perfil e distância (em metros inteiros, como a API grava).
                var instante = Instante(intencao.Hora);
                votos[intencao.UsuarioId] = new VotoFato(
                    intencao.UsuarioId, intencao.Valor, intencao.Perfil,
                    intencao.DistanciaMetros is { } d ? Math.Round(d, MidpointRounding.AwayFromZero) : null,
                    instante);
                estado = MotorConfianca.AvaliarCicloDeVida(estado, votos.Values, instante, cfg);
            }

            // Job de ciclo de vida (na API roda a cada 15 min; aqui, ao fim de cada passo de 1 h).
            var agora = Instante(fimDaHora);
            estado = MotorConfianca.AvaliarCicloDeVida(estado, votos.Values, agora, cfg);
            linha.Status[h] = estado.Status;
            linha.Indice[h] = ComoGravadoNaApi(MotorConfianca.CalcularConfianca(votos.Values, agora, cfg).Indice);
        }

        return linha;
    }

    /// <summary>
    /// A API grava a confiança em <c>numeric(5,2)</c> e classifica a faixa sobre o valor gravado
    /// (<c>RecalculoProblema.Aplicar</c>): o Simulator usa o mesmo arredondamento, senão índices em
    /// [69,995; 70) ou [39,995; 40) cairiam em faixas diferentes das do sistema avaliado.
    /// </summary>
    /// <remarks>Arredonda via <c>decimal</c>, exatamente como a API: em <c>double</c>, 39,995 é 39,99499… e iria para 39,99.</remarks>
    public static double ComoGravadoNaApi(double indice) => (double)Math.Round((decimal)indice, 2, MidpointRounding.AwayFromZero);

    /// <summary>Mesmas recusas da API: problema resolvido e autor votando no próprio problema.</summary>
    private static bool Aceita(Intencao intencao, ProblemaSimulado problema, EstadoProblema estado) =>
        estado.Status != StatusProblema.Resolvida && intencao.UsuarioId != problema.AutorId;
}
