using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Ocorrencias.Api.Problemas;
using Ocorrencias.Api.Confianca;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Tests.Confianca;

[Collection(ApiCollection.Nome)]
public sealed class CicloDeVidaTests(ApiFactory fabrica)
{
    private async Task ProcessarAsync()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetRequiredService<ProcessadorCicloDeVida>().ProcessarAsync(CancellationToken.None);
    }

    private Task<ObterDetalheProblemaResponse> DetalheAsync(Guid id) =>
        fabrica.CreateClient().GetAsync($"/api/v1/problemas/{id}").ContinueWith(t => t.Result.LerAsync<ObterDetalheProblemaResponse>()).Unwrap();

    [Fact]
    public async Task Job_leva_possivelmente_resolvido_a_resolvido_apos_48h()
    {
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await fabrica.ProblemaAtivoAsync(lat, lng);
        // 4 "não" a ~100 m contra os 5 "sim" sem GPS: R⁻ = 8, R⁺ = 5, p ≈ 0,62 ≥ 0,6.
        for (var i = 0; i < 4; i++)
        {
            var votante = await fabrica.NovaPessoaAsync();
            await votante.VotarAsync(problema.Id, "nao", Cenarios.Norte(lat, 100), lng);
        }

        (await DetalheAsync(problema.Id)).Status.Should().Be(StatusProblema.PossivelmenteResolvida);

        fabrica.Relogio.Advance(TimeSpan.FromHours(47));
        await ProcessarAsync();
        (await DetalheAsync(problema.Id)).Status.Should().Be(StatusProblema.PossivelmenteResolvida);

        fabrica.Relogio.Advance(TimeSpan.FromHours(1));
        await ProcessarAsync();
        var gravado = await fabrica.NoBancoAsync(db => db.Problemas.AsNoTracking().SingleAsync(p => p.Id == problema.Id));
        gravado.Status.Should().Be(StatusProblema.Resolvida);
        gravado.StatusAlteradoEm.Should().Be(fabrica.Relogio.GetUtcNow());
    }

    [Fact]
    public async Task Job_tira_de_nova_apos_72h_sem_votos()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await autor.RegistrarProblemaAsync(lat, lng);

        fabrica.Relogio.Advance(TimeSpan.FromHours(72));
        await ProcessarAsync();

        var detalhe = await DetalheAsync(problema.Id);
        detalhe.Status.Should().Be(StatusProblema.Ativa);
        detalhe.Confianca.Should().Be(0);
        detalhe.Faixa.Should().Be(FaixaConfianca.Baixa);
    }

    [Fact]
    public async Task Job_aplica_o_decaimento_temporal_sem_votos_novos()
    {
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await fabrica.ProblemaAtivoAsync(lat, lng); // C = 62,5 com 5 votos agora

        fabrica.Relogio.Advance(TimeSpan.FromDays(14));
        await ProcessarAsync();

        // Meia-vida de 14 dias: E⁺ = 2,5 → C = 100 × 2,5 / 5,5 ≈ 45,5.
        (await DetalheAsync(problema.Id)).Confianca.Should().Be(45.5);
    }

    [Fact]
    public async Task Job_nao_mexe_em_resolvido()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await autor.RegistrarProblemaAsync(lat, lng);
        await fabrica.NoBancoAsync(db => db.Problemas.Where(p => p.Id == problema.Id).ExecuteUpdateAsync(s => s
            .SetProperty(p => p.Status, StatusProblema.Resolvida)
            .SetProperty(p => p.Confianca, (decimal?)12.34m)));
        var antes = await fabrica.NoBancoAsync(db => db.Problemas.AsNoTracking().SingleAsync(p => p.Id == problema.Id));

        fabrica.Relogio.Advance(TimeSpan.FromDays(30));
        await ProcessarAsync();

        var depois = await fabrica.NoBancoAsync(db => db.Problemas.AsNoTracking().SingleAsync(p => p.Id == problema.Id));
        depois.Status.Should().Be(StatusProblema.Resolvida);
        depois.Confianca.Should().Be(12.34m);
        depois.StatusAlteradoEm.Should().Be(antes.StatusAlteradoEm);
    }

    [Fact]
    public async Task Job_conta_a_transicao_e_na_execucao_seguinte_nao_acha_mais_nada()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await autor.RegistrarProblemaAsync(lat, lng);
        fabrica.Relogio.Advance(TimeSpan.FromHours(72));

        (await ProcessarContandoAsync()).Should().BeGreaterThanOrEqualTo(1);
        (await DetalheAsync(problema.Id)).Status.Should().Be(StatusProblema.Ativa);

        // O banco é compartilhado: esvazia transições encadeadas de problemas de outros testes.
        for (var i = 0; i < 5 && await ProcessarContandoAsync() > 0; i++)
        {
        }

        (await ProcessarContandoAsync()).Should().Be(0);
    }

    private async Task<int> ProcessarContandoAsync()
    {
        await using var escopo = fabrica.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<ProcessadorCicloDeVida>().ProcessarAsync(CancellationToken.None);
    }
}
