using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;
using static Ocorrencias.Domain.Tests.Confianca.Cenario;

namespace Ocorrencias.Domain.Tests.Confianca;

/// <summary>Propriedades simples do motor, verificadas sobre votos aleatórios com semente fixa.</summary>
public sealed class PropriedadesTests
{
    private const int Amostras = 500;

    public static TheoryData<string> Configuracoes => ["A", "B", "B-v3", "B-v5"];

    private static ConfiguracaoConfianca Config(string nome) => nome switch
    {
        "A" => ConfiguracaoConfianca.A,
        "B" => ConfiguracaoConfianca.B,
        "B-v3" => ConfiguracaoConfianca.B.ComPesoVerificado(3),
        "B-v5" => ConfiguracaoConfianca.B.ComPesoVerificado(5),
        _ => throw new ArgumentOutOfRangeException(nameof(nome)),
    };

    private static VotoFato VotoAleatorio(Random rnd, ValorVoto? valor = null) => Voto(
        valor ?? (rnd.Next(2) == 0 ? ValorVoto.Positivo : ValorVoto.Negativo),
        (Perfil)rnd.Next(1, 4),
        rnd.Next(4) == 0 ? null : rnd.NextDouble() * 5000,
        T0.AddHours(-rnd.Next(0, 24 * 90)));

    private static List<VotoFato> VotosAleatorios(Random rnd) =>
        Enumerable.Range(0, rnd.Next(0, 60)).Select(_ => VotoAleatorio(rnd)).ToList();

    [Theory]
    [MemberData(nameof(Configuracoes))]
    public void Indice_fica_em_0_inclusive_a_100_exclusive(string nome)
    {
        var cfg = Config(nome);
        var rnd = new Random(42);

        for (var i = 0; i < Amostras; i++)
        {
            var r = MotorConfianca.CalcularConfianca(VotosAleatorios(rnd), T0, cfg);

            r.Indice.Should().BeGreaterThanOrEqualTo(0).And.BeLessThan(100);
        }
    }

    [Theory]
    [MemberData(nameof(Configuracoes))]
    public void Indice_cresce_quando_entra_um_sim(string nome)
    {
        var cfg = Config(nome);
        var rnd = new Random(7);

        for (var i = 0; i < Amostras; i++)
        {
            var votos = VotosAleatorios(rnd);
            var antes = MotorConfianca.CalcularConfianca(votos, T0, cfg);

            votos.Add(VotoAleatorio(rnd, ValorVoto.Positivo));
            var depois = MotorConfianca.CalcularConfianca(votos, T0, cfg);

            depois.EPositivo.Should().BeGreaterThan(antes.EPositivo);
            depois.Indice.Should().BeGreaterThan(antes.Indice);
        }
    }

    [Fact]
    public void Config_A_independe_de_agora()
    {
        var rnd = new Random(2026);

        for (var i = 0; i < Amostras; i++)
        {
            var votos = VotosAleatorios(rnd);
            var agora = MotorConfianca.CalcularConfianca(votos, T0, ConfiguracaoConfianca.A);
            var depois = MotorConfianca.CalcularConfianca(votos, T0.AddDays(rnd.Next(1, 1000)), ConfiguracaoConfianca.A);

            depois.Should().Be(agora);
        }
    }
}
