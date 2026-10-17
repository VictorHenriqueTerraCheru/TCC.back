using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using Ocorrencias.Domain.Confianca;
using Ocorrencias.Simulator.Modelo;

namespace Ocorrencias.Simulator;

/// <summary>
/// <c>--config A,B,B-v3,B-v5 --variantes S1,S2,S3 --sementes 30 --saida resultados/ --cenario cenarios/base.json</c>.
/// </summary>
public sealed record Opcoes(
    IReadOnlyList<ConfiguracaoConfianca> Configuracoes,
    IReadOnlyList<Variante> Variantes,
    IReadOnlyList<int> Sementes,
    string Saida,
    Cenario Cenario)
{
    public const string Uso = """
        Uso: dotnet run --project src/Ocorrencias.Simulator -- [opções]

          --config      Configurações do motor, separadas por vírgula (padrão: A,B,B-v3,B-v5)
          --variantes   Variantes de ataque: S1 (sem GPS), S2 (GPS falso), S3 (S2 + 1 verificada) (padrão: S1,S2,S3)
          --sementes    Quantidade de sementes, usadas como 1..N (padrão: 30)
          --saida       Diretório dos CSVs (padrão: resultados/)
          --cenario     JSON com os parâmetros do cenário (padrão: valores de cenarios/base.json embutidos no código)
        """;

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, // erro de digitação vira erro, não parâmetro ignorado
    };

    /// <exception cref="ArgumentException">Argumento inválido.</exception>
    public static Opcoes Ler(string[] args)
    {
        var valores = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var i = 0; i < args.Length; i++)
        {
            if (!args[i].StartsWith("--", StringComparison.Ordinal) || i + 1 >= args.Length)
            {
                throw new ArgumentException($"Argumento inesperado: '{args[i]}'.");
            }

            valores[args[i][2..]] = args[++i];
        }

        var desconhecidos = valores.Keys.Except(["config", "variantes", "sementes", "saida", "cenario"]).ToList();
        if (desconhecidos.Count > 0)
        {
            throw new ArgumentException($"Opção desconhecida: --{desconhecidos[0]}.");
        }

        var configuracoes = Separar(valores.GetValueOrDefault("config", "A,B,B-v3,B-v5"))
            .Select(ConfiguracaoConfianca.PorNome)
            .ToList();

        var variantes = Separar(valores.GetValueOrDefault("variantes", "S1,S2,S3"))
            .Select(v => Enum.TryParse<Variante>(v, ignoreCase: true, out var variante) && Enum.IsDefined(variante)
                ? variante
                : throw new ArgumentException($"Variante desconhecida: '{v}'."))
            .ToList();

        var quantidade = int.TryParse(valores.GetValueOrDefault("sementes", "30"), NumberStyles.None, CultureInfo.InvariantCulture, out var n) && n > 0
            ? n
            : throw new ArgumentException("--sementes deve ser um inteiro positivo.");

        var cenario = valores.TryGetValue("cenario", out var caminho)
            ? JsonSerializer.Deserialize<Cenario>(File.ReadAllText(caminho), Json) ?? throw new ArgumentException("Cenário vazio.")
            : new Cenario();

        if (configuracoes.Select(c => c.Nome).Distinct().Count() != configuracoes.Count)
        {
            throw new ArgumentException("Configurações repetidas em --config.");
        }

        return new Opcoes(configuracoes, variantes.Distinct().ToList(), Enumerable.Range(1, quantidade).ToList(),
            valores.GetValueOrDefault("saida", "resultados/"), cenario);
    }

    private static string[] Separar(string valor) =>
        valor.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
