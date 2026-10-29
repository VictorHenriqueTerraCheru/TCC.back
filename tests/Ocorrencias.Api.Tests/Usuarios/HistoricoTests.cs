using System.Net;
using System.Net.Http.Json;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Usuarios;
using Ocorrencias.Api.Tests.Infra;

namespace Ocorrencias.Api.Tests.Usuarios;

[Collection(ApiCollection.Nome)]
public sealed class HistoricoTests(ApiFactory fabrica)
{
    private static async Task<Paginado<ObterHistoricoItem>> HistoricoAsync(Pessoa pessoa, string consulta = "") =>
        await (await pessoa.Cliente.GetAsync($"/api/v1/me/historico{consulta}")).LerAsync<Paginado<ObterHistoricoItem>>();

    [Fact]
    public async Task Une_registros_e_votos_do_mais_recente_para_o_mais_antigo()
    {
        var eu = await fabrica.NovaPessoaAsync();
        var outro = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();

        var meuProblema = await eu.RegistrarProblemaAsync(lat, lng);
        fabrica.Relogio.Advance(TimeSpan.FromMinutes(1));
        await eu.Cliente.PostAsJsonAsync("/api/v1/incidentes",
            new { categoriaId = 7, lat, lng, ocorridoEm = fabrica.Relogio.GetUtcNow().AddMinutes(-10) }, ClienteApi.Json);
        fabrica.Relogio.Advance(TimeSpan.FromMinutes(1));
        var deOutro = await outro.RegistrarProblemaAsync(Cenarios.Norte(lat, 30), lng, categoriaId: 3);
        await eu.VotarAsync(deOutro.Id, "nao");

        var historico = await HistoricoAsync(eu);

        historico.Total.Should().Be(3);
        historico.Itens.Select(i => i.Tipo).Should().Equal(ObterHistoricoTipoItem.Voto, ObterHistoricoTipoItem.RegistroIncidente, ObterHistoricoTipoItem.RegistroProblema);

        var voto = historico.Itens[0];
        voto.OcorrenciaId.Should().Be(deOutro.Id);
        voto.Valor.Should().Be(ObterHistoricoValorItem.Nao);
        voto.StatusProblema.Should().Be(StatusProblema.Nova);
        voto.Categoria.Nome.Should().Be("Poste apagado");

        var incidente = historico.Itens[1];
        incidente.OcorrenciaId.Should().BeNull();
        incidente.Endereco.Should().Be("Rua Voluntários da Pátria, Botafogo");
        incidente.Valor.Should().BeNull();
        incidente.StatusProblema.Should().BeNull();

        var registro = historico.Itens[2];
        registro.OcorrenciaId.Should().Be(meuProblema.Id);
        registro.Endereco.Should().Be(meuProblema.Endereco);
        registro.Data.Should().Be(meuProblema.CriadoEm);
    }

    [Fact]
    public async Task Filtros_e_paginacao()
    {
        var eu = await fabrica.NovaPessoaAsync();
        var outro = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        await eu.RegistrarProblemaAsync(lat, lng);
        var alheio = await outro.RegistrarProblemaAsync(Cenarios.Norte(lat, 30), lng);
        fabrica.Relogio.Advance(TimeSpan.FromMinutes(1));
        await eu.VotarAsync(alheio.Id, "sim");
        await eu.VotarAsync(alheio.Id, "nao"); // substitui o "sim"

        var registros = await HistoricoAsync(eu, "?filtro=registros");
        var votos = await HistoricoAsync(eu, "?filtro=votos");
        var pagina2 = await HistoricoAsync(eu, "?tamanho=1&pagina=2");

        registros.Itens.Should().ContainSingle().Which.Tipo.Should().Be(ObterHistoricoTipoItem.RegistroProblema);
        votos.Itens.Select(i => (i.Tipo, i.Valor)).Should().Equal(
            [(ObterHistoricoTipoItem.Voto, (ObterHistoricoValorItem?)ObterHistoricoValorItem.Nao)]);
        pagina2.Total.Should().Be(2);
        pagina2.Itens.Should().ContainSingle().Which.Tipo.Should().Be(ObterHistoricoTipoItem.RegistroProblema);
    }

    [Fact]
    public async Task Paginacao_e_estavel_com_registros_no_mesmo_instante()
    {
        var eu = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.AreaIsolada();
        for (var i = 0; i < 5; i++)
        {
            // Rótulo distinto por incidente (para enxergar duplicatas) e mesmo instante (relógio parado):
            // só o desempate pela chave ordena.
            var latI = lat + i * 0.001;
            fabrica.Nominatim.Responder(latI, lng, NominatimFalso.Endereco(rua: $"Rua {i}"));
            (await eu.Cliente.PostAsJsonAsync("/api/v1/incidentes",
                new { categoriaId = 7, lat = latI, lng, ocorridoEm = fabrica.Relogio.GetUtcNow().AddMinutes(-5) }, ClienteApi.Json))
                .EnsureSuccessStatusCode();
        }

        var paginas = new List<ObterHistoricoItem>();
        for (var pagina = 1; pagina <= 5; pagina++)
        {
            paginas.AddRange((await HistoricoAsync(eu, $"?tamanho=1&pagina={pagina}")).Itens);
        }

        var completa = (await HistoricoAsync(eu, "?tamanho=5")).Itens;
        paginas.Should().Equal(completa);
        paginas.Select(i => i.Endereco).Should().OnlyHaveUniqueItems().And.HaveCount(5);
    }

    [Fact]
    public async Task Enums_do_historico_trafegam_em_camel_case()
    {
        var eu = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        await eu.RegistrarProblemaAsync(lat, lng);

        var json = await (await eu.Cliente.GetAsync("/api/v1/me/historico")).LerJsonAsync();

        json.GetProperty("itens")[0].GetProperty("tipo").GetString().Should().Be("registroProblema");
    }

    [Fact]
    public async Task Filtro_invalido_devolve_400()
    {
        var eu = await fabrica.NovaPessoaAsync();

        (await eu.Cliente.GetAsync("/api/v1/me/historico?filtro=outros")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
