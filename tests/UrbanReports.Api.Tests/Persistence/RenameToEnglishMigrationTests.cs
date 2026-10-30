using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Tests.Persistence;

public sealed class RenameToEnglishMigrationTests : IAsyncLifetime
{
    private const string LastPortugueseMigration = "20261020185252_GruposDeCategoria";

    private static readonly Guid UserId = Guid.Parse("00000000-0000-4000-8000-000000000001");
    private static readonly Guid IssueId = Guid.Parse("00000000-0000-4000-8000-000000000002");
    private static readonly Guid VoteId = Guid.Parse("00000000-0000-4000-8000-000000000003");
    private static readonly Guid IncidentId = Guid.Parse("00000000-0000-4000-8000-000000000004");
    private static readonly Guid RequestId = Guid.Parse("00000000-0000-4000-8000-000000000005");

    private readonly PostgreSqlContainer _database = new PostgreSqlBuilder("postgis/postgis:16-3.4").Build();

    public Task InitializeAsync() => _database.StartAsync();

    public Task DisposeAsync() => _database.DisposeAsync().AsTask();

    [Fact]
    public async Task Rename_keeps_every_row()
    {
        await using var db = Context();
        await db.GetService<IMigrator>().MigrateAsync(LastPortugueseMigration);
        await db.Database.ExecuteSqlRawAsync($"""
            INSERT INTO grupos_categoria (chave, rotulo, tipo, cor, ordem) VALUES ('obras', 'Obras', 1, 9, 50);
            INSERT INTO categorias (id, nome, grupo, tipo, icone, ativa) VALUES (100, 'Obra parada', 'obras', 1, 'obra', true);
            INSERT INTO usuarios (id, nome, perfil, criado_em, verificado_em, email, normalized_email, email_confirmed,
                                  phone_number_confirmed, two_factor_enabled, lockout_enabled, access_failed_count)
            VALUES ('{UserId}', 'Ana', 2, '2026-10-01T10:00:00Z', '2026-10-02T10:00:00Z', 'ana@tcc.local', 'ANA@TCC.LOCAL',
                    false, false, false, true, 0);
            INSERT INTO problemas (id, categoria_id, autor_id, descricao, localizacao, endereco_aproximado, status, confianca,
                                   criado_em, status_alterado_em)
            VALUES ('{IssueId}', 100, '{UserId}', 'Obra parada há meses', ST_GeogFromText('POINT(-43.109 -22.905)'),
                    'Rua Moreira César, 100 — Icaraí', 2, 82.00, '2026-10-03T10:00:00Z', '2026-10-04T10:00:00Z');
            INSERT INTO votos (id, problema_id, usuario_id, valor, perfil_no_momento, distancia_metros, registrado_em)
            VALUES ('{VoteId}', '{IssueId}', '{UserId}', 1, 2, 120, '2026-10-05T10:00:00Z');
            INSERT INTO incidentes (id, categoria_id, autor_id, area_aproximada, raio_metros, rotulo_area, ocorrido_em, registrado_em)
            VALUES ('{IncidentId}', 100, '{UserId}', ST_GeogFromText('POINT(-43.109 -22.905)'), 400, 'Icaraí',
                    '2026-10-06T10:00:00Z', '2026-10-06T11:00:00Z');
            INSERT INTO solicitacoes_verificacao (id, usuario_id, tipo_vinculo, justificativa, status, criada_em)
            VALUES ('{RequestId}', '{UserId}', 1, 'Moro em Icaraí há dez anos e passo pela rua todos os dias.', 2,
                    '2026-10-01T12:00:00Z');
            """);

        await db.Database.MigrateAsync();

        var user = await db.Users.SingleAsync(u => u.Id == UserId);
        user.Name.Should().Be("Ana");
        user.Role.Should().Be(Role.VerifiedCitizen);
        user.VerifiedAt.Should().Be(DateTimeOffset.Parse("2026-10-02T10:00:00Z"));

        var issue = await db.Issues.SingleAsync(i => i.Id == IssueId);
        issue.TrustScore.Should().Be(82.00m);
        issue.Status.Should().Be(IssueStatus.Active);
        issue.ApproximateAddress.Should().Be("Rua Moreira César, 100 — Icaraí");
        issue.Location.X.Should().BeApproximately(-43.109, 1e-9);
        issue.Location.Y.Should().BeApproximately(-22.905, 1e-9);

        var vote = await db.Votes.SingleAsync(v => v.Id == VoteId);
        vote.Value.Should().Be(VoteValue.Positive);
        vote.RoleAtVote.Should().Be(Role.VerifiedCitizen);
        vote.DistanceMeters.Should().Be(120);

        var incident = await db.Incidents.SingleAsync(i => i.Id == IncidentId);
        incident.RadiusMeters.Should().Be(400);
        incident.AreaLabel.Should().Be("Icaraí");

        var request = await db.VerificationRequests.SingleAsync(r => r.Id == RequestId);
        request.AffiliationType.Should().Be(AffiliationType.LivesInArea);
        request.Status.Should().Be(VerificationStatus.Approved);

        var category = await db.Categories.SingleAsync(c => c.Id == 100);
        category.Group.Should().Be("obras");
        (await db.Groups.SingleAsync(g => g.Key == "obras")).Color.Should().Be(GroupColor.Pink);
    }

    [Fact]
    public async Task Rename_can_be_rolled_back()
    {
        await using var db = Context();
        await db.Database.MigrateAsync();

        await db.GetService<IMigrator>().MigrateAsync(LastPortugueseMigration);

        var tables = await db.Database
            .SqlQuery<string>($"SELECT table_name AS \"Value\" FROM information_schema.tables WHERE table_schema = 'public'")
            .ToListAsync();
        tables.Should().Contain(["usuarios", "problemas", "votos", "categorias", "grupos_categoria"]).And.NotContain("issues");
    }

    private AppDbContext Context() =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_database.GetConnectionString(), npgsql => npgsql.UseNetTopologySuite(geographyAsDefault: true))
            .UseSnakeCaseNamingConvention()
            .Options);
}
