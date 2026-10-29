using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using UrbanReports.Api.Geocodificacao;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Geocodificacao;

public sealed class ServicoGeocodificacaoTests : IDisposable
{
    private readonly NominatimFalso _nominatim = new();
    private readonly ServicoGeocodificacao _servico;

    public ServicoGeocodificacaoTests() => _servico = Criar(_nominatim);

    public void Dispose() => _servico.Dispose();

    private static ServicoGeocodificacao Criar(NominatimFalso nominatim, int limiteCache = 50_000) =>
        new(
            nominatim,
            Options.Create(new NominatimOptions { Email = "contato@example.com", LimiteEntradasCache = limiteCache }),
            NullLogger<ServicoGeocodificacao>.Instance);

    [Fact]
    public async Task Cache_respeita_o_limite_de_entradas()
    {
        using var servico = Criar(new NominatimFalso(), limiteCache: 2);

        for (var i = 0; i < 5; i++)
        {
            await servico.ReversaAsync(-22.95 - i * 0.001, -43.18, CancellationToken.None);
        }

        servico.EntradasEmCache.Should().BeLessThanOrEqualTo(2);
    }

    [Fact]
    public async Task Duas_chamadas_ao_mesmo_ponto_fazem_uma_unica_requisicao()
    {
        var primeira = await _servico.ReversaAsync(-22.9521, -43.1868, CancellationToken.None);
        var segunda = await _servico.ReversaAsync(-22.9521, -43.1868, CancellationToken.None);

        _nominatim.Chamadas.Should().Be(1);
        segunda.Should().Be(primeira);
    }

    [Fact]
    public async Task Pontos_iguais_em_4_casas_compartilham_o_cache()
    {
        await _servico.ReversaAsync(-22.95211, -43.18681, CancellationToken.None);
        await _servico.ReversaAsync(-22.95209, -43.18679, CancellationToken.None);

        _nominatim.Chamadas.Should().Be(1);
    }

    [Fact]
    public async Task Pontos_diferentes_em_4_casas_consultam_de_novo()
    {
        await _servico.ReversaAsync(-22.9521, -43.1868, CancellationToken.None);
        await _servico.ReversaAsync(-22.9522, -43.1868, CancellationToken.None);

        _nominatim.Chamadas.Should().Be(2);
    }

    [Fact]
    public async Task Nominatim_fora_do_ar_responde_indisponivel_e_nao_guarda_no_cache()
    {
        _nominatim.ForaDoAr(-22.9521, -43.1868);

        var falha = await _servico.ReversaAsync(-22.9521, -43.1868, CancellationToken.None);

        falha.Disponivel.Should().BeFalse();
        falha.Endereco.Should().Be("Próximo a -22.95210, -43.18680");

        _nominatim.ForaDoAr(-22.9521, -43.1868, fora: false);
        var depois = await _servico.ReversaAsync(-22.9521, -43.1868, CancellationToken.None);

        depois.Disponivel.Should().BeTrue();
        _nominatim.Chamadas.Should().Be(2);
    }

    [Fact]
    public async Task Indisponivel_usa_a_coordenada_original_no_texto()
    {
        _nominatim.ForaDoAr(-22.95213, -43.18677);

        var e = await _servico.ReversaAsync(-22.95213, -43.18677, CancellationToken.None);

        e.Endereco.Should().Be("Próximo a -22.95213, -43.18677");
    }

    [Fact]
    public async Task Chamadas_simultaneas_ao_mesmo_ponto_compartilham_a_requisicao()
    {
        _nominatim.Atraso = TimeSpan.FromMilliseconds(200);

        var resultados = await Task.WhenAll(Enumerable.Range(0, 5)
            .Select(_ => _servico.ReversaAsync(-22.9521, -43.1868, CancellationToken.None)));

        _nominatim.Chamadas.Should().Be(1);
        resultados.Should().AllSatisfy(r => r.Disponivel.Should().BeTrue());
    }
}
