using System.Net;
using System.Net.Http.Json;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features.Usuarios;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Domain;
using static Ocorrencias.Api.Features.Usuarios.ObterHistorico;

namespace Ocorrencias.Api.Tests.Usuarios;

[Collection(ApiCollection.Nome)]
public sealed class HistoricoTests(ApiFactory fabrica)
{
    private static async Task<Paginado<Item>> HistoricoAsync(Pessoa pessoa, string consulta = "") =>
        await (await pessoa.Cliente.GetAsync($"/api/v1/me/historico{consulta}")).LerAsync<Paginado<Item>>();

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
        await eu.VotarExistenciaAsync(deOutro.Id, "naoVi");

        var historico = await HistoricoAsync(eu);

        historico.Total.Should().Be(3);
        historico.Itens.Select(i => i.Tipo).Should().Equal(TipoItem.VotoExistencia, TipoItem.RegistroIncidente, TipoItem.RegistroProblema);

        var voto = historico.Itens[0];
        voto.OcorrenciaId.Should().Be(deOutro.Id);
        voto.Valor.Should().Be(ValorItem.NaoVi);
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
        await eu.VotarExistenciaAsync(alheio.Id, "confirma");
        await fabrica.NoBancoAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(
            db.Problemas.Where(p => p.Id == alheio.Id), s => s.SetProperty(p => p.Status, StatusProblema.Ativa)));
        await eu.VotarPersistenciaAsync(alheio.Id, "resolvido");

        var registros = await HistoricoAsync(eu, "?filtro=registros");
        var votos = await HistoricoAsync(eu, "?filtro=votos");
        var pagina2 = await HistoricoAsync(eu, "?tamanho=2&pagina=2");

        registros.Itens.Should().ContainSingle().Which.Tipo.Should().Be(TipoItem.RegistroProblema);
        votos.Itens.Select(i => (i.Tipo, i.Valor)).Should().BeEquivalentTo(
            [(TipoItem.VotoExistencia, (ValorItem?)ValorItem.Confirma), (TipoItem.VotoPersistencia, (ValorItem?)ValorItem.Resolvido)]);
        pagina2.Total.Should().Be(3);
        pagina2.Itens.Should().ContainSingle().Which.Tipo.Should().Be(TipoItem.RegistroProblema);
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
