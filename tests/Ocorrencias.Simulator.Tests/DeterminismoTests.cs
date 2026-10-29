using UrbanReports.Api.Confianca.Motor;
using Ocorrencias.Simulator.Experimento;
using Ocorrencias.Simulator.Modelo;
using Ocorrencias.Simulator.Saida;

namespace Ocorrencias.Simulator.Tests;

public sealed class DeterminismoTests
{
    private static readonly ConfiguracaoConfianca[] Configs = [ConfiguracaoConfianca.A, ConfiguracaoConfianca.B];
    private static readonly Variante[] Variantes = [Variante.S1, Variante.S2, Variante.S3];

    private static ResultadoExperimento Executar(int? paralelismo = null) =>
        Executor.Executar(CenarioPequeno.Valor, Configs, Variantes, [1, 2, 3], paralelismo);

    [Fact]
    public void Mesma_semente_gera_csv_identico_byte_a_byte()
    {
        var dirA = Directory.CreateTempSubdirectory("sim-a").FullName;
        var dirB = Directory.CreateTempSubdirectory("sim-b").FullName;
        try
        {
            var arquivos = Csv.Escrever(dirA, Executar());
            Csv.Escrever(dirB, Executar());

            arquivos.Should().HaveCount(6);
            foreach (var arquivo in arquivos)
            {
                File.ReadAllBytes(Path.Combine(dirB, arquivo)).Should().Equal(File.ReadAllBytes(Path.Combine(dirA, arquivo)), arquivo);
            }
        }
        finally
        {
            Directory.Delete(dirA, recursive: true);
            Directory.Delete(dirB, recursive: true);
        }
    }

    [Fact]
    public void Paralelismo_nao_altera_o_resultado()
    {
        var serial = Executar(paralelismo: 1);
        var paralelo = Executar(paralelismo: 8);

        paralelo.Diario.Should().Equal(serial.Diario);
        paralelo.Resolucoes.Should().Equal(serial.Resolucoes);
        paralelo.Ataques.Should().Equal(serial.Ataques);
    }

    [Fact]
    public void Sementes_diferentes_geram_mundos_diferentes()
    {
        var a = Mundo.Gerar(CenarioPequeno.Valor, 1);
        var b = Mundo.Gerar(CenarioPequeno.Valor, 2);

        b.Problemas.Select(p => p.Local).Should().NotEqual(a.Problemas.Select(p => p.Local));
    }

    [Fact]
    public void Simulator_referencia_so_a_api()
    {
        var referencias = typeof(Executor).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        referencias.Where(n => n.StartsWith("Ocorrencias", StringComparison.Ordinal) || n.StartsWith("UrbanReports", StringComparison.Ordinal)).Should().Equal("UrbanReports.Api");
        referencias.Should().OnlyContain(n => n == "UrbanReports.Api" || n.StartsWith("System", StringComparison.Ordinal) || n == "netstandard");
    }
}
