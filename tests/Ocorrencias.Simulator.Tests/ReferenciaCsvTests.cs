using System.Runtime.CompilerServices;
using Ocorrencias.Api.Confianca.Motor;
using Ocorrencias.Simulator.Experimento;
using Ocorrencias.Simulator.Modelo;
using Ocorrencias.Simulator.Saida;

namespace Ocorrencias.Simulator.Tests;

/// <summary>
/// Trava a saída do Simulator contra CSVs gravados antes do refactor para o inglês: os resultados
/// usados na monografia precisam continuar reproduzíveis byte a byte.
/// Regravar (só com mudança aprovada): <c>ATUALIZAR_REFERENCIA_CSV=1 dotnet test --filter Csv_igual_a_referencia</c>.
/// </summary>
public sealed class ReferenciaCsvTests
{
    [Fact]
    public void Csv_igual_a_referencia()
    {
        var referencia = DiretorioReferencia();
        var gerado = Directory.CreateTempSubdirectory("sim-ref").FullName;
        try
        {
            var resultado = Executor.Executar(
                CenarioPequeno.Valor,
                [ConfiguracaoConfianca.A, ConfiguracaoConfianca.B],
                [Variante.S1, Variante.S2, Variante.S3],
                [1, 2]);
            var arquivos = Csv.Escrever(gerado, resultado);

            if (Environment.GetEnvironmentVariable("ATUALIZAR_REFERENCIA_CSV") == "1")
            {
                Directory.CreateDirectory(referencia);
                foreach (var arquivo in arquivos)
                {
                    File.Copy(Path.Combine(gerado, arquivo), Path.Combine(referencia, arquivo), overwrite: true);
                }
            }

            Directory.Exists(referencia).Should().BeTrue("os CSVs de referência precisam existir em {0}", referencia);
            Directory.GetFiles(referencia, "*.csv").Select(Path.GetFileName).Should().BeEquivalentTo(arquivos);
            foreach (var arquivo in arquivos)
            {
                File.ReadAllBytes(Path.Combine(gerado, arquivo)).Should().Equal(File.ReadAllBytes(Path.Combine(referencia, arquivo)), arquivo);
            }
        }
        finally
        {
            Directory.Delete(gerado, recursive: true);
        }
    }

    private static string DiretorioReferencia([CallerFilePath] string arquivo = "") =>
        Path.Combine(Path.GetDirectoryName(arquivo)!, "Referencia");
}
