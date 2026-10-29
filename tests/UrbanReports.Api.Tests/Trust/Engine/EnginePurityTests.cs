using System.Runtime.CompilerServices;

namespace UrbanReports.Api.Tests.Trust.Engine;

public sealed class EnginePurityTests
{
    [Fact]
    public void Engine_uses_only_bcl()
    {
        var folder = Path.Combine(RepositoryRoot(), "src", "UrbanReports.Api", "Feature", "Trust", "Engine");
        Directory.Exists(folder).Should().BeTrue(folder);

        var files = Directory.GetFiles(folder, "*.cs");
        files.Should().HaveCountGreaterThanOrEqualTo(5);

        var usings = files
            .SelectMany(File.ReadLines)
            .Select(l => l.Trim())
            .Where(l => l.StartsWith("using ", StringComparison.Ordinal) && l.EndsWith(';'))
            .Select(l => l["using ".Length..^1].Trim())
            .Distinct();

        usings.Should().OnlyContain(u => u == "System" || u.StartsWith("System.", StringComparison.Ordinal) || u == "UrbanReports.Api.Common");
    }

    private static string RepositoryRoot([CallerFilePath] string file = "")
    {
        var dir = new DirectoryInfo(Path.GetDirectoryName(file)!);
        while (dir is not null && dir.GetFiles("*.slnx").Length == 0)
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
