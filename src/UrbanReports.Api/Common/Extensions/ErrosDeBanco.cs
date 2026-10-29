using Npgsql;

namespace UrbanReports.Api.Common;

public static class ErrosDeBanco
{
    public static bool ViolouUnicidade(this Exception erro) => SqlState(erro) == PostgresErrorCodes.UniqueViolation;

    public static bool ViolouChaveEstrangeira(this Exception erro) => SqlState(erro) == PostgresErrorCodes.ForeignKeyViolation;

    private static string? SqlState(Exception erro) =>
        (erro as PostgresException ?? erro.InnerException as PostgresException)?.SqlState;
}
