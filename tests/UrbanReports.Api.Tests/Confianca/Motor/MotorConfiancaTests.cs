using UrbanReports.Api.Common;
using UrbanReports.Api.Confianca.Motor;
using static UrbanReports.Api.Tests.Confianca.Motor.Cenario;

namespace UrbanReports.Api.Tests.Confianca.Motor;

/// <summary>Casos obrigatórios de docs/MOTOR_CONFIANCA.md (numeração da tabela).</summary>
public sealed class MotorConfiancaTests
{
    private const double Tolerancia = 1e-6;
    private const double ToleranciaIndice = 1e-3;

    private static readonly ConfiguracaoConfianca A = ConfiguracaoConfianca.A;
    private static readonly ConfiguracaoConfianca B = ConfiguracaoConfianca.B;

    // ---------- pesos ----------

    [Fact]
    public void Caso01_proximidade_ate_200m_vale_2() =>
        MotorConfianca.PesoProximidade(150, B).Should().BeApproximately(2, Tolerancia);

    [Fact]
    public void Caso02_proximidade_1100m_vale_1_5() =>
        MotorConfianca.PesoProximidade(1100, B).Should().BeApproximately(1.5, Tolerancia);

    [Fact]
    public void Caso03_proximidade_longe_ou_sem_gps_vale_1()
    {
        MotorConfianca.PesoProximidade(5000, B).Should().BeApproximately(1, Tolerancia);
        MotorConfianca.PesoProximidade(null, B).Should().BeApproximately(1, Tolerancia);
    }

    [Fact]
    public void Caso04_config_A_ignora_proximidade() =>
        MotorConfianca.PesoProximidade(50, A).Should().BeApproximately(1, Tolerancia);

    [Fact]
    public void Caso05_meia_vida_da_confianca_14_dias() =>
        MotorConfianca.FatorTemporal(TimeSpan.FromDays(14), B.MeiaVidaConfianca, B).Should().BeApproximately(0.5, Tolerancia);

    [Fact]
    public void Caso06_meia_vida_do_ciclo_de_vida_3_dias() =>
        MotorConfianca.FatorTemporal(TimeSpan.FromDays(3), B.MeiaVidaCicloDeVida, B).Should().BeApproximately(0.5, Tolerancia);

    // ---------- índice ----------

    private static List<VotoFato> QuinzeSimA100m() => Repetir(15, () => Sim());

    [Fact]
    public void Caso07_quinze_sim_agora()
    {
        var r = MotorConfianca.CalcularConfianca(QuinzeSimA100m(), T0, B);

        r.Indice.Should().BeApproximately(90.909, ToleranciaIndice);
        r.Faixa.Should().Be(FaixaConfianca.Alta);
    }

    [Fact]
    public void Caso08_quinze_sim_apos_28_dias()
    {
        var r = MotorConfianca.CalcularConfianca(QuinzeSimA100m(), T0.AddDays(28), B);

        r.Indice.Should().BeApproximately(71.429, ToleranciaIndice);
        r.Faixa.Should().Be(FaixaConfianca.Alta);
    }

    [Fact]
    public void Caso09_quinze_sim_apos_56_dias()
    {
        var r = MotorConfianca.CalcularConfianca(QuinzeSimA100m(), T0.AddDays(56), B);

        r.Indice.Should().BeApproximately(38.462, ToleranciaIndice);
        r.Faixa.Should().Be(FaixaConfianca.Baixa);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(28)]
    [InlineData(365)]
    public void Caso10_config_A_independe_do_tempo(int dias)
    {
        var r = MotorConfianca.CalcularConfianca(QuinzeSimA100m(), T0.AddDays(dias), A);

        r.Indice.Should().BeApproximately(83.333, ToleranciaIndice);
    }

    [Fact]
    public void Caso11_um_verificado_a_100m()
    {
        var r = MotorConfianca.CalcularConfianca([Sim(Perfil.CidadaoVerificado)], T0, B);

        r.Indice.Should().BeApproximately(86.957, ToleranciaIndice);
        r.Faixa.Should().Be(FaixaConfianca.Alta);
    }

    [Fact]
    public void Caso12_um_verificado_com_peso_3()
    {
        var cfg = B.ComPesoVerificado(3);

        var r = MotorConfianca.CalcularConfianca([Sim(Perfil.CidadaoVerificado)], T0, cfg);

        cfg.Nome.Should().Be("B-v3");
        r.EPositivo.Should().BeApproximately(6, Tolerancia);
        r.Indice.Should().BeApproximately(66.667, ToleranciaIndice);
        r.Faixa.Should().Be(FaixaConfianca.Media);
    }

    [Fact]
    public void Caso13_sem_votos()
    {
        var r = MotorConfianca.CalcularConfianca([], T0, B);

        r.Indice.Should().Be(0);
        r.Faixa.Should().Be(FaixaConfianca.Baixa);
    }

    // ---------- ciclo de vida ----------

    [Fact]
    public void Caso14_nova_com_4_votantes_e_71h_continua_nova()
    {
        var votos = Repetir(4, () => Sim());

        var e = MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Nova), votos, T0.AddHours(71), B);

        e.Status.Should().Be(StatusProblema.Nova);
    }

    [Fact]
    public void Caso15_nova_com_5_votantes_distintos_vira_ativa()
    {
        var votos = Repetir(5, () => Sim());
        var agora = T0.AddHours(1);

        var e = MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Nova), votos, agora, B);

        e.Status.Should().Be(StatusProblema.Ativa);
        e.StatusAlteradoEm.Should().Be(agora);
    }

    [Fact]
    public void Caso16_nova_com_1_votante_e_72h_vira_ativa()
    {
        var e = MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Nova), [Sim()], T0.AddHours(72), B);

        e.Status.Should().Be(StatusProblema.Ativa);
    }

    /// <summary>Votos do caso 17: 3 "não" (R⁻ = 6) e 1 "sim" (R⁺ = 2), todos a 100 m em t0.</summary>
    private static List<VotoFato> VotosCaso17() => [.. Repetir(3, () => Nao()), Sim()];

    private static EstadoProblema EstadoAposCaso17() =>
        MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Ativa), VotosCaso17(), T0, B);

    [Fact]
    public void Caso17_ativa_com_maioria_nao_vira_possivelmente_resolvida()
    {
        var e = EstadoAposCaso17();

        e.Status.Should().Be(StatusProblema.PossivelmenteResolvida);
        e.CondicaoResolucaoDesde.Should().Be(T0);
        e.StatusAlteradoEm.Should().Be(T0);
    }

    [Fact]
    public void Caso18_apos_47h_sem_votos_continua_possivelmente_resolvida()
    {
        var e = MotorConfianca.AvaliarCicloDeVida(EstadoAposCaso17(), VotosCaso17(), T0.AddHours(47), B);

        e.Status.Should().Be(StatusProblema.PossivelmenteResolvida);
        e.CondicaoResolucaoDesde.Should().Be(T0);
    }

    [Fact]
    public void Caso19_apos_48h_sem_votos_vira_resolvida()
    {
        var agora = T0.AddHours(48);

        var e = MotorConfianca.AvaliarCicloDeVida(EstadoAposCaso17(), VotosCaso17(), agora, B);

        e.Status.Should().Be(StatusProblema.Resolvida);
        e.StatusAlteradoEm.Should().Be(agora);
    }

    [Fact]
    public void Caso20_tres_sim_derrubam_a_proporcao_e_volta_para_ativa()
    {
        var agora = T0.AddHours(1);
        List<VotoFato> votos = [.. VotosCaso17(), .. Repetir(3, () => Sim(em: agora))];

        var e = MotorConfianca.AvaliarCicloDeVida(EstadoAposCaso17(), votos, agora, B);

        e.Status.Should().Be(StatusProblema.Ativa);
        e.CondicaoResolucaoDesde.Should().BeNull();
        e.StatusAlteradoEm.Should().Be(agora);
    }

    /// <summary>
    /// Caso 21: dois votos do mesmo usuário não podem acontecer (a API garante a
    /// unicidade por índice único). O Domain assume isso e NÃO deduplica pesos; este teste só
    /// documenta o comportamento: ambos pesam no índice, mas contam como um único votante.
    /// </summary>
    [Fact]
    public void Caso21_unicidade_de_voto_e_garantida_pela_api_nao_pelo_domain()
    {
        var usuario = Guid.NewGuid();
        List<VotoFato> duplicados = [Sim(usuario: usuario), Sim(usuario: usuario)];

        var r = MotorConfianca.CalcularConfianca(duplicados, T0, B);
        r.EPositivo.Should().BeApproximately(4, Tolerancia);

        List<VotoFato> cincoDoMesmo = Repetir(5, () => Sim(usuario: usuario));
        MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Nova), cincoDoMesmo, T0, B)
            .Status.Should().Be(StatusProblema.Nova, "votantes conta UsuarioId distintos");
    }

    /// <summary>
    /// Caso 22: registro falso que ninguém encontra. Os "não" dados em Nova contam no índice e, depois
    /// que o problema sai de Nova, também no ciclo de vida: ele passa a PossivelmenteResolvida e sai do
    /// mapa em 48 h.
    /// </summary>
    [Fact]
    public void Caso22_nova_so_com_nao_sai_do_mapa_em_48h()
    {
        var votos = Repetir(5, () => Nao());

        MotorConfianca.CalcularConfianca(votos, T0, B).Faixa.Should().Be(FaixaConfianca.Baixa);

        var ativa = MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Nova), votos, T0, B);
        ativa.Status.Should().Be(StatusProblema.Ativa);

        var possivelmente = MotorConfianca.AvaliarCicloDeVida(ativa, votos, T0.AddMinutes(15), B);
        possivelmente.Status.Should().Be(StatusProblema.PossivelmenteResolvida);

        MotorConfianca.AvaliarCicloDeVida(possivelmente, votos, T0.AddMinutes(15).AddHours(48), B)
            .Status.Should().Be(StatusProblema.Resolvida);
    }
}
