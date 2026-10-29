using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace UrbanReports.Api.Common;

public static class BloqueioDoCatalogo
{
    private const long Chave = 7_410_001;

    public static Task TomarAsync(this DatabaseFacade banco, CancellationToken ct) =>
        banco.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({Chave})", ct);
}
