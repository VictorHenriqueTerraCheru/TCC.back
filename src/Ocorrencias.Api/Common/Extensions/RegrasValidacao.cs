using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Common;

public static class RegrasValidacao
{
    /// <summary>Só nomes (camelCase ou não); <c>Enum.TryParse</c> aceitaria números como "1" ou "999".</summary>
    private static readonly HashSet<string> NomesStatus = new(Enum.GetNames<StatusProblema>(), StringComparer.OrdinalIgnoreCase);

    public static IRuleBuilderOptions<T, double> Latitude<T>(this IRuleBuilder<T, double> regra) =>
        regra.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, double> Longitude<T>(this IRuleBuilder<T, double> regra) =>
        regra.InclusiveBetween(-180, 180);

    public static IRuleBuilderOptions<T, double?> Latitude<T>(this IRuleBuilder<T, double?> regra) =>
        regra.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, double?> Longitude<T>(this IRuleBuilder<T, double?> regra) =>
        regra.InclusiveBetween(-180, 180);

    /// <summary>Lista separada por vírgula de grupos de categoria (ex.: <c>viaPublica,eletrica</c>); vale qualquer grupo cadastrado.</summary>
    public static IRuleBuilderOptionsConditions<T, string?> ListaDeGrupos<T>(this IRuleBuilder<T, string?> regra, AppDbContext db) =>
        regra.CustomAsync(async (valor, contexto, ct) =>
        {
            var pedidos = Listas.Separar(valor);
            if (pedidos.Length == 0)
            {
                return;
            }

            var conhecidos = await db.Grupos.OrderBy(g => g.Ordem).Select(g => g.Chave).ToListAsync(ct);
            if (!pedidos.All(conhecidos.Contains))
            {
                contexto.AddFailure("Grupo desconhecido. Valores aceitos: " + string.Join(", ", conhecidos) + ".");
            }
        });

    /// <summary>Lista separada por vírgula de status em camelCase (ex.: <c>nova,ativa</c>).</summary>
    public static IRuleBuilderOptions<T, string?> ListaDeStatus<T>(this IRuleBuilder<T, string?> regra) =>
        regra.Must(s => Listas.Separar(s).All(v => NomesStatus.Contains(v)))
            .WithMessage("Status desconhecido. Valores aceitos: nova, ativa, possivelmenteResolvida, resolvida.");
}

public static class Listas
{
    public static string[] Separar(string? valores) =>
        string.IsNullOrWhiteSpace(valores)
            ? []
            : valores.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}
