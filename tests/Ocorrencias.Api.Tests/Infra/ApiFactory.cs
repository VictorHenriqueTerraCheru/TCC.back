using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Time.Testing;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Infra.Data;
using Ocorrencias.Api.Infra.Geocodificacao;
using Testcontainers.PostgreSql;

namespace Ocorrencias.Api.Tests.Infra;

/// <summary>API real contra um PostGIS descartável (Testcontainers), com relógio controlado pelos testes.</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string OrigemFront = "http://localhost:5173";

    private readonly PostgreSqlContainer _banco = new PostgreSqlBuilder("postgis/postgis:16-3.4").Build();

    /// <summary>Começa alinhado em microssegundos (precisão do timestamptz); avance em unidades inteiras.</summary>
    public FakeTimeProvider Relogio { get; } = new(RelogioPostgres.Truncar(DateTimeOffset.UtcNow));

    public NominatimFalso Nominatim { get; } = new();

    /// <summary>Gera coordenadas distintas na Zona Sul do Rio, para os testes não compartilharem cache.</summary>
    public static (double Lat, double Lng) PontoUnico()
    {
        var n = Interlocked.Increment(ref _sequencia);
        return (-22.95 - n * 0.001, -43.18 - n * 0.001);
    }

    private static int _sequencia;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Default", _banco.GetConnectionString());
        builder.UseSetting("Jwt:Chave", "chave-de-teste-com-pelo-menos-32-caracteres");
        builder.UseSetting("Cors:Origens:0", OrigemFront);
        builder.UseSetting("CicloDeVida:Habilitado", "false");
        builder.ConfigureTestServices(s =>
        {
            s.AddSingleton<TimeProvider>(Relogio);
            s.AddSingleton<INominatimClient>(Nominatim);
        });
    }

    public async Task InitializeAsync()
    {
        await _banco.StartAsync();

        await using var escopo = Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
    }

    /// <summary>Executa algo direto no banco (preparar cenário ou conferir o que foi gravado).</summary>
    public async Task<T> NoBancoAsync<T>(Func<AppDbContext, Task<T>> acao)
    {
        await using var escopo = Services.CreateAsyncScope();
        return await acao(escopo.ServiceProvider.GetRequiredService<AppDbContext>());
    }

    public Task NoBancoAsync(Func<AppDbContext, Task> acao) =>
        NoBancoAsync(async db => { await acao(db); return true; });

    async Task IAsyncLifetime.DisposeAsync()
    {
        await DisposeAsync();
        await _banco.DisposeAsync();
    }
}

[CollectionDefinition(Nome)]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>
{
    public const string Nome = "Api";
}
