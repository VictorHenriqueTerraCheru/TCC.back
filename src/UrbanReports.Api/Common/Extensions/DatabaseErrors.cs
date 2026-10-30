using Npgsql;

namespace UrbanReports.Api.Common;

public static class DatabaseErrors
{
    public static bool ViolatedUniqueness(this Exception error) => SqlState(error) == PostgresErrorCodes.UniqueViolation;

    public static bool ViolatedForeignKey(this Exception error) => SqlState(error) == PostgresErrorCodes.ForeignKeyViolation;

    private static string? SqlState(Exception error) =>
        (error as PostgresException ?? error.InnerException as PostgresException)?.SqlState;
}
