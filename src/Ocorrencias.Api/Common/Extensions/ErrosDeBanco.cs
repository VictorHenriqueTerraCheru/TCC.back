using Npgsql;

namespace Ocorrencias.Api.Common;

/// <summary>Violações de constraint do PostgreSQL, para traduzir corridas entre requisições em erro de regra (409/400) em vez de 500.</summary>
public static class ErrosDeBanco
{
    public static bool ViolouUnicidade(this Exception erro) => SqlState(erro) == PostgresErrorCodes.UniqueViolation;

    public static bool ViolouChaveEstrangeira(this Exception erro) => SqlState(erro) == PostgresErrorCodes.ForeignKeyViolation;

    private static string? SqlState(Exception erro) =>
        (erro as PostgresException ?? erro.InnerException as PostgresException)?.SqlState;
}
