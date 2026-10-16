using Microsoft.Extensions.Time.Testing;
using Ocorrencias.Api.Infra.Geocodificacao;

namespace Ocorrencias.Api.Tests.Geocodificacao;

public sealed class LimitadorDeTaxaTests
{
    private readonly FakeTimeProvider _relogio = new(DateTimeOffset.UtcNow);

    private LimitadorDeTaxa Criar(TimeSpan? esperaMaxima = null) =>
        new(_relogio, TimeSpan.FromSeconds(1), esperaMaxima ?? TimeSpan.FromSeconds(30));

    [Fact]
    public async Task Segunda_chamada_espera_o_intervalo_minimo()
    {
        var limitador = Criar();
        await limitador.ExecutarAsync(_ => Task.FromResult(1), CancellationToken.None);

        var segunda = limitador.ExecutarAsync(_ => Task.FromResult(2), CancellationToken.None);
        await Task.Delay(50);
        segunda.IsCompleted.Should().BeFalse("ainda não passou 1 s desde a primeira");

        _relogio.Advance(TimeSpan.FromMilliseconds(999));
        await Task.Delay(50);
        segunda.IsCompleted.Should().BeFalse();

        _relogio.Advance(TimeSpan.FromMilliseconds(1));
        (await segunda.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(2);
    }

    [Fact]
    public async Task Chamada_apos_o_intervalo_nao_espera()
    {
        var limitador = Criar();
        await limitador.ExecutarAsync(_ => Task.FromResult(1), CancellationToken.None);
        _relogio.Advance(TimeSpan.FromSeconds(2));

        var resultado = await limitador.ExecutarAsync(_ => Task.FromResult(2), CancellationToken.None).WaitAsync(TimeSpan.FromSeconds(5));

        resultado.Should().Be(2);
    }

    [Fact]
    public async Task Chamadas_nunca_rodam_em_paralelo()
    {
        var limitador = Criar();
        var liberar = new TaskCompletionSource<int>();
        var primeira = limitador.ExecutarAsync(_ => liberar.Task, CancellationToken.None);

        var segundaIniciou = false;
        var segunda = limitador.ExecutarAsync(_ => { segundaIniciou = true; return Task.FromResult(2); }, CancellationToken.None);
        await Task.Delay(50);

        segundaIniciou.Should().BeFalse();
        liberar.SetResult(1);
        await primeira;
        _relogio.Advance(TimeSpan.FromSeconds(1));
        (await segunda.WaitAsync(TimeSpan.FromSeconds(5))).Should().Be(2);
    }

    [Fact]
    public async Task Fila_cheia_alem_da_espera_maxima_lanca_timeout()
    {
        var limitador = Criar(esperaMaxima: TimeSpan.FromMilliseconds(50));
        var bloqueio = new TaskCompletionSource<int>();
        _ = limitador.ExecutarAsync(_ => bloqueio.Task, CancellationToken.None);

        var acao = () => limitador.ExecutarAsync(_ => Task.FromResult(2), CancellationToken.None);

        await acao.Should().ThrowAsync<TimeoutException>();
        bloqueio.SetResult(1);
    }
}
