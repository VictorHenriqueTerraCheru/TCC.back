using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Incidentes;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Tests.Incidentes;

[Collection(ApiCollection.Nome)]
public sealed class IncidentesTests(ApiFactory fabrica)
{
    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    private Task<HttpResponseMessage> RegistrarAsync(Pessoa pessoa, double lat, double lng, short categoriaId = 7, DateTimeOffset? ocorridoEm = null) =>
        pessoa.Cliente.PostAsJsonAsync("/api/v1/incidentes",
            new { categoriaId, lat, lng, ocorridoEm = ocorridoEm ?? fabrica.Relogio.GetUtcNow().AddMinutes(-30) },
            ClienteApi.Json);

    private static async Task<string?> TipoErroAsync(HttpResponseMessage resposta) =>
        (await resposta.LerJsonAsync()).GetProperty("type").GetString();

    [Fact]
    public async Task Registrar_arredonda_em_3_casas_guarda_o_rotulo_e_devolve_a_area()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (baseLat, baseLng) = ApiFactory.AreaIsolada();
        var (lat, lng) = (baseLat + 0.00041, baseLng - 0.00067);

        var resposta = await RegistrarAsync(pessoa, lat, lng);
        var corpo = await resposta.Content.ReadAsStringAsync();
        var area = await resposta.LerAsync<ObterAreaIncidentesResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        corpo.Should().NotContainEquivalentOf("autor");
        area.RaioMetros.Should().Be(400);
        area.AtualizadoEm.Should().Be(fabrica.Relogio.GetUtcNow());
        area.Nivel.Should().Be(NivelConcentracao.Baixa);
        area.Contagens.Should().Equal(
            new ObterAreaIncidentesContagem(7, "Roubo", 1, 1),
            new ObterAreaIncidentesContagem(8, "Acidente de trânsito", 0, 0));

        var gravado = await fabrica.NoBancoAsync(db => db.Incidentes.AsNoTracking()
            .Where(i => i.AutorId == pessoa.Id).SingleAsync());
        gravado.AreaAproximada.Y.Should().Be(Math.Round(lat, 3, MidpointRounding.AwayFromZero));
        gravado.AreaAproximada.X.Should().Be(Math.Round(lng, 3, MidpointRounding.AwayFromZero));
        gravado.RotuloArea.Should().Be("Rua Voluntários da Pátria, Botafogo");
        gravado.RaioMetros.Should().Be(100);
    }

    [Fact]
    public async Task Registrar_com_nominatim_fora_do_ar_grava_sem_rotulo()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();
        fabrica.Nominatim.ForaDoAr(lat, lng);

        var resposta = await RegistrarAsync(pessoa, lat, lng);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        var gravado = await fabrica.NoBancoAsync(db => db.Incidentes.AsNoTracking().SingleAsync(i => i.AutorId == pessoa.Id));
        gravado.RotuloArea.Should().BeNull();
    }

    [Fact]
    public async Task Registrar_com_categoria_de_problema_devolve_422()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();

        var resposta = await RegistrarAsync(pessoa, lat, lng, categoriaId: 1);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TipoErroAsync(resposta)).Should().Be("categoria-nao-e-incidente");
    }

    [Theory]
    [InlineData(-25 * 60)]
    [InlineData(1)]
    public async Task Horario_fora_das_ultimas_24h_devolve_422(int minutosDesdeAgora)
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();

        var resposta = await RegistrarAsync(pessoa, lat, lng, ocorridoEm: fabrica.Relogio.GetUtcNow().AddMinutes(minutosDesdeAgora));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TipoErroAsync(resposta)).Should().Be("horario-fora-da-janela");
    }

    [Fact]
    public async Task Decimo_primeiro_incidente_em_24h_devolve_429_e_libera_depois()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();

        for (var i = 0; i < 10; i++)
        {
            (await RegistrarAsync(pessoa, lat, lng)).StatusCode.Should().Be(HttpStatusCode.Created);
        }

        var decimoPrimeiro = await RegistrarAsync(pessoa, lat, lng);
        decimoPrimeiro.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
        (await TipoErroAsync(decimoPrimeiro)).Should().Be("limite-diario-incidentes");

        fabrica.Relogio.Advance(TimeSpan.FromHours(24));
        (await RegistrarAsync(pessoa, lat, lng)).StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Registros_simultaneos_nao_furam_o_limite()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 15).Select(_ => RegistrarAsync(pessoa, lat, lng)));

        respostas.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(10);
        respostas.Count(r => r.StatusCode == HttpStatusCode.TooManyRequests).Should().Be(5);
    }

    [Fact]
    public async Task Area_classifica_o_nivel_pelo_total_de_7_dias()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();
        for (var i = 0; i < 4; i++)
        {
            await RegistrarAsync(pessoa, Cenarios.Norte(lat, i * 50), lng, categoriaId: (short)(i % 2 == 0 ? 7 : 8));
        }

        var baixa = await (await fabrica.CreateClient().GetAsync(Inv($"/api/v1/incidentes/area?lat={lat}&lng={lng}")))
            .LerAsync<ObterAreaIncidentesResponse>();
        await RegistrarAsync(pessoa, lat, lng);
        var media = await (await fabrica.CreateClient().GetAsync(Inv($"/api/v1/incidentes/area?lat={lat}&lng={lng}")))
            .LerAsync<ObterAreaIncidentesResponse>();

        baixa.Nivel.Should().Be(NivelConcentracao.Baixa);
        media.Nivel.Should().Be(NivelConcentracao.Media);
        media.Contagens.Sum(c => c.Dias7).Should().Be(5);
    }

    [Fact]
    public async Task Area_ignora_incidentes_alem_de_400m()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();
        await RegistrarAsync(pessoa, Cenarios.Norte(lat, 300), lng);
        await RegistrarAsync(pessoa, Cenarios.Norte(lat, 500), lng);

        var area = await (await fabrica.CreateClient().GetAsync(Inv($"/api/v1/incidentes/area?lat={lat}&lng={lng}")))
            .LerAsync<ObterAreaIncidentesResponse>();

        area.Contagens.Sum(c => c.Dias30).Should().Be(1);
    }

    [Theory]
    [InlineData(0, NivelConcentracao.Baixa)]
    [InlineData(4, NivelConcentracao.Baixa)]
    [InlineData(5, NivelConcentracao.Media)]
    [InlineData(14, NivelConcentracao.Media)]
    [InlineData(15, NivelConcentracao.Alta)]
    public void Limiares_do_nivel(int total, NivelConcentracao esperado) =>
        IncidenteService.Nivel(total).Should().Be(esperado);

    [Fact]
    public async Task Calor_agrupa_por_ponto_arredondado_e_respeita_janela_e_grupos()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();
        await RegistrarAsync(pessoa, lat + 0.0001, lng);              // mesma célula
        await RegistrarAsync(pessoa, lat - 0.0002, lng + 0.0001);     // mesma célula
        await RegistrarAsync(pessoa, lat + 0.003, lng, categoriaId: 8); // outra célula, trânsito

        var area = Inv($"minLat={lat - 0.01}&minLng={lng - 0.01}&maxLat={lat + 0.01}&maxLng={lng + 0.01}");
        var celulas = await (await fabrica.CreateClient().GetAsync($"/api/v1/incidentes/calor?{area}"))
            .LerAsync<List<ObterCalorIncidentesResponse>>();
        var seguranca = await (await fabrica.CreateClient().GetAsync($"/api/v1/incidentes/calor?{area}&grupos=seguranca"))
            .LerAsync<List<ObterCalorIncidentesResponse>>();

        celulas.Should().BeEquivalentTo(
        [
            new ObterCalorIncidentesResponse(Math.Round(lat, 3), lng, 2),
            new ObterCalorIncidentesResponse(Math.Round(lat + 0.003, 3), lng, 1),
        ]);
        seguranca.Should().ContainSingle().Which.Quantidade.Should().Be(2);

        fabrica.Relogio.Advance(TimeSpan.FromDays(8));
        var em7Dias = await (await fabrica.CreateClient().GetAsync($"/api/v1/incidentes/calor?{area}&dias=7"))
            .LerAsync<List<ObterCalorIncidentesResponse>>();
        var em30Dias = await (await fabrica.CreateClient().GetAsync($"/api/v1/incidentes/calor?{area}&dias=30"))
            .LerAsync<List<ObterCalorIncidentesResponse>>();

        em7Dias.Should().BeEmpty();
        em30Dias.Sum(c => c.Quantidade).Should().Be(3);
    }

    [Fact]
    public async Task Calor_respeita_o_retangulo_exato_em_viewport_largo()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        await RegistrarAsync(pessoa, 70, 0);    // fora (entraria se as arestas fossem geodésicas)
        await RegistrarAsync(pessoa, 60.5, 10); // dentro (ficaria de fora em geography)

        var celulas = await (await fabrica.CreateClient().GetAsync("/api/v1/incidentes/calor?minLat=60&minLng=-60&maxLat=61&maxLng=60"))
            .LerAsync<List<ObterCalorIncidentesResponse>>();

        celulas.Should().ContainSingle().Which.Should().Be(new ObterCalorIncidentesResponse(60.5, 10, 1));
    }

    [Theory]
    [InlineData("dias=0")]
    [InlineData("dias=31")]
    [InlineData("grupos=viaPublica,xyz")]
    public async Task Calor_com_parametro_invalido_devolve_400(string parametro)
    {
        var resposta = await fabrica.CreateClient().GetAsync($"/api/v1/incidentes/calor?minLat=-23&minLng=-44&maxLat=-22&maxLng=-43&{parametro}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
