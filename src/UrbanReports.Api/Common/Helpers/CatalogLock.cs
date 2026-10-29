using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace UrbanReports.Api.Common;

public static class CatalogLock
{
    private const long Key = 7_410_001;

    public static Task LockCatalogAsync(this DatabaseFacade database, CancellationToken ct) =>
        database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({Key})", ct);
}
