using System.Runtime.CompilerServices;

namespace Ocorrencias.Api.Tests.Confianca.Motor;

/// <summary>O motor de confiança é puro: só BCL e os enums de Common (garantia que antes vinha do projeto Domain separado).</summary>
public sealed class PurezaDoMotorTests
{
    [Fact]
    public void Motor_so_usa_bcl()
    {
        var pasta = Path.Combine(RaizDoRepositorio(), "src", "Ocorrencias.Api", "Feature", "Confianca", "Motor");
        Directory.Exists(pasta).Should().BeTrue(pasta);

        var arquivos = Directory.GetFiles(pasta, "*.cs");
        arquivos.Should().HaveCountGreaterThanOrEqualTo(5);

        var usings = arquivos
            .SelectMany(File.ReadLines)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("using ", StringComparison.Ordinal) && l.EndsWith(';'))
            .Select(l => l["using ".Length..^1].Trim())
            .Distinct();

        usings.Should().OnlyContain(u => u == "System" || u.StartsWith("System.", StringComparison.Ordinal) || u == "Ocorrencias.Api.Common");
    }

    private static string RaizDoRepositorio([CallerFilePath] string arquivo = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(arquivo)!);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0)
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
