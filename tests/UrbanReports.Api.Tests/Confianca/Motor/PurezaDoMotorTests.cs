using System.Runtime.CompilerServices;

namespace UrbanReports.Api.Tests.Confianca.Motor;

public sealed class PurezaDoMotorTests
{
    [Fact]
    public void Motor_so_usa_bcl()
    {
        var pasta = Path.Combine(RaizDoRepositorio(), "src", "UrbanReports.Api", "Feature", "Confianca", "Motor");
        Directory.Exists(pasta).Should().BeTrue(pasta);

        var arquivos = Directory.GetFiles(pasta, "*.cs");
        arquivos.Should().HaveCountGreaterThanOrEqualTo(5);

        var usings = arquivos
            .SelectMany(File.ReadLines)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("using ", StringComparison.Ordinal) && l.EndsWith(';'))
            .Select(l => l["using ".Length..^1].Trim())
            .Distinct();

        usings.Should().OnlyContain(u => u == "System" || u.StartsWith("System.", StringComparison.Ordinal) || u == "UrbanReports.Api.Common");
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
