using UrbanReports.Api.Common;
using UrbanReports.Api.Confianca.Motor;
using static UrbanReports.Api.Tests.Confianca.Motor.Cenario;

namespace UrbanReports.Api.Tests.Confianca.Motor;

public sealed class RegrasComplementaresTests
{
    private static readonly ConfiguracaoConfianca B = ConfiguracaoConfianca.B;

    [Theory]
    [InlineData(Perfil.Cidadao, 1)]
    [InlineData(Perfil.CidadaoVerificado, 10)]
    [InlineData(Perfil.Administrador, 1)]
    public void Peso_de_perfil_na_config_B(Perfil perfil, double esperado) =>
        MotorConfianca.PesoPerfil(perfil, B).Should().Be(esperado);

    [Fact]
    public void Config_A_iguala_verificado_ao_cidadao() =>
        MotorConfianca.PesoPerfil(Perfil.CidadaoVerificado, ConfiguracaoConfianca.A).Should().Be(1);

    [Theory]
    [InlineData(0, 2)]
    [InlineData(200, 2)]
    [InlineData(2000, 1)]
    [InlineData(2000.001, 1)]
    public void Proximidade_nos_limites_das_faixas(double distancia, double esperado) =>
        MotorConfianca.PesoProximidade(distancia, B).Should().BeApproximately(esperado, 1e-6);

    [Fact]
    public void Proximidade_nunca_fica_abaixo_de_1()
    {
        for (var d = 0.0; d <= 10_000; d += 7.3)
        {
            MotorConfianca.PesoProximidade(d, B).Should().BeGreaterThanOrEqualTo(1);
        }
    }

    [Fact]
    public void Voto_registrado_no_futuro_nao_ganha_peso_extra() =>
        MotorConfianca.FatorTemporal(TimeSpan.FromHours(-5), B.MeiaVidaConfianca, B).Should().Be(1);

    [Fact]
    public void Config_A_nao_decai() =>
        MotorConfianca.FatorTemporal(TimeSpan.FromDays(365), ConfiguracaoConfianca.A.MeiaVidaCicloDeVida, ConfiguracaoConfianca.A).Should().Be(1);

    [Fact]
    public void Peso_do_voto_multiplica_perfil_proximidade_e_tempo()
    {
        var voto = Sim(Perfil.CidadaoVerificado, distancia: 1100, em: T0);

        MotorConfianca.PesoVoto(voto, T0.AddDays(14), B.MeiaVidaConfianca, B).Should().BeApproximately(10 * 1.5 * 0.5, 1e-9);
    }

    [Theory]
    [InlineData(39.999, FaixaConfianca.Baixa)]
    [InlineData(40, FaixaConfianca.Media)]
    [InlineData(69.999, FaixaConfianca.Media)]
    [InlineData(70, FaixaConfianca.Alta)]
    public void Faixas_pelos_limiares(double indice, FaixaConfianca esperada) =>
        MotorConfianca.ClassificarFaixa(indice, B).Should().Be(esperada);

    [Fact]
    public void Nova_nao_pula_direto_para_possivelmente_resolvida()
    {
        List<VotoFato> votos = [.. Repetir(5, () => Sim()), .. Repetir(10, () => Nao())];

        var e = MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Nova), votos, T0, B);

        e.Status.Should().Be(StatusProblema.Ativa);
        e.CondicaoResolucaoDesde.Should().BeNull();
    }

    [Fact]
    public void Votos_nao_tambem_contam_como_votantes()
    {
        List<VotoFato> votos = [.. Repetir(3, () => Sim()), .. Repetir(2, () => Nao())];

        MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Nova), votos, T0, B)
            .Status.Should().Be(StatusProblema.Ativa);
    }

    [Fact]
    public void Mesmo_voto_envelhece_devagar_no_indice_e_rapido_no_ciclo_de_vida()
    {
        var voto = Sim(em: T0);
        var agora = T0.AddDays(3);

        MotorConfianca.CalcularConfianca([voto], agora, B).EPositivo
            .Should().BeApproximately(2 * Math.Pow(2, -3.0 / 14), 1e-9);
        MotorConfianca.PesoVoto(voto, agora, B.MeiaVidaCicloDeVida, B).Should().BeApproximately(1, 1e-9);
    }

    [Fact]
    public void Ativa_exige_peso_minimo_de_resolvido_para_entrar()
    {
        var votos = Repetir(2, () => Nao());

        MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Ativa), votos, T0, B)
            .Status.Should().Be(StatusProblema.Ativa);
    }

    [Fact]
    public void Ativa_so_com_sim_continua_ativa() =>
        MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Ativa), [Sim()], T0.AddDays(30), B)
            .Status.Should().Be(StatusProblema.Ativa);

    [Fact]
    public void Proporcao_exatamente_no_limiar_entra_em_possivelmente_resolvida()
    {
        List<VotoFato> votos = [.. Repetir(3, () => Nao()), .. Repetir(2, () => Sim())];

        MotorConfianca.AvaliarCicloDeVida(Estado(StatusProblema.Ativa), votos, T0, B)
            .Status.Should().Be(StatusProblema.PossivelmenteResolvida);
    }

    [Fact]
    public void Resolvida_e_estado_final()
    {
        var estado = Estado(StatusProblema.Resolvida);
        var votos = Repetir(10, () => Sim());

        MotorConfianca.AvaliarCicloDeVida(estado, votos, T0.AddDays(10), B).Should().Be(estado);
    }

    [Fact]
    public void Sem_transicao_o_estado_volta_inalterado()
    {
        var estado = Estado(StatusProblema.Ativa, statusAlteradoEm: T0);

        MotorConfianca.AvaliarCicloDeVida(estado, [Sim()], T0.AddHours(1), B).Should().Be(estado);
    }
}
