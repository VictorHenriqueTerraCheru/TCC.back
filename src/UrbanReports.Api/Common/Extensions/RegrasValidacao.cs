using FluentValidation;
using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Persistence;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Common;

public static class RegrasValidacao
{
    private static readonly HashSet<string> NomesStatus = new(Enum.GetNames<StatusProblema>(), StringComparer.OrdinalIgnoreCase);

    public static IRuleBuilderOptions<T, double> Latitude<T>(this IRuleBuilder<T, double> regra) =>
        regra.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, double> Longitude<T>(this IRuleBuilder<T, double> regra) =>
        regra.InclusiveBetween(-180, 180);

    public static IRuleBuilderOptions<T, double?> Latitude<T>(this IRuleBuilder<T, double?> regra) =>
        regra.InclusiveBetween(-90, 90);

    public static IRuleBuilderOptions<T, double?> Longitude<T>(this IRuleBuilder<T, double?> regra) =>
        regra.InclusiveBetween(-180, 180);

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
