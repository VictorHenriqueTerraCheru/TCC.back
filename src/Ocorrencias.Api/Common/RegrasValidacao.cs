using FluentValidation;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Common;

public static class RegrasValidacao
{
    public static IRuleBuilderOptions<T, double> Latitude<T>(this IRuleBuilder<T, double> regra) =>
        regra.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, double> Longitude<T>(this IRuleBuilder<T, double> regra) =>
        regra.InclusiveBetween(-180, 180);

    public static IRuleBuilderOptions<T, double?> Latitude<T>(this IRuleBuilder<T, double?> regra) =>
        regra.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, double?> Longitude<T>(this IRuleBuilder<T, double?> regra) =>
        regra.InclusiveBetween(-180, 180);

    /// <summary>Lista separada por vírgula de grupos de categoria (ex.: <c>viaPublica,eletrica</c>).</summary>
    public static IRuleBuilderOptions<T, string?> ListaDeGrupos<T>(this IRuleBuilder<T, string?> regra) =>
        regra.Must(g => Listas.Separar(g).All(Grupos.Conhecidos.Contains))
            .WithMessage("Grupo desconhecido. Valores aceitos: " + string.Join(", ", Grupos.Conhecidos) + ".");

    /// <summary>Lista separada por vírgula de status em camelCase (ex.: <c>nova,ativa</c>).</summary>
    public static IRuleBuilderOptions<T, string?> ListaDeStatus<T>(this IRuleBuilder<T, string?> regra) =>
        regra.Must(s => Listas.Separar(s).All(v => Enum.TryParse<StatusProblema>(v, ignoreCase: true, out _)))
            .WithMessage("Status desconhecido. Valores aceitos: nova, ativa, possivelmenteResolvida, resolvida.");
}

public static class Listas
{
    public static string[] Separar(string? valores) =>
        string.IsNullOrWhiteSpace(valores)
            ? []
            : valores.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public static class Grupos
{
    /// <summary>Grupos do seed de categorias (docs/API.md).</summary>
    public static readonly IReadOnlySet<string> Conhecidos =
        new HashSet<string>(["viaPublica", "eletrica", "limpeza", "seguranca", "transito"], StringComparer.Ordinal);
}
