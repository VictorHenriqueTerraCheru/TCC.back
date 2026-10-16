using Ocorrencias.Api.Categorias;
using System.Globalization;
using System.Net;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features.Problemas;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Tests.Problemas;

[Collection(ApiCollection.Nome)]
public sealed class ProblemasTests(ApiFactory fabrica)
{
    private static string Inv(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    [Fact]
    public async Task Registrar_cria_problema_nova_com_endereco_do_servidor()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();

        var resposta = await autor.PostarProblemaAsync(lat, lng, descricao: "  Buraco grande  ");
        var detalhe = await resposta.LerAsync<ObterDetalheProblema.Response>();

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        resposta.Headers.Location!.ToString().Should().Be($"/api/v1/problemas/{detalhe.Id}");
        detalhe.Status.Should().Be(StatusProblema.Nova);
        detalhe.Categoria.Should().Be(new CategoriaResumo(1, "Buraco na via", "viaPublica", "buraco"));
        detalhe.Descricao.Should().Be("Buraco grande");
        detalhe.Endereco.Should().Be("Rua Voluntários da Pátria, 170 — Botafogo");
        detalhe.Lat.Should().BeApproximately(lat, 1e-9);
        detalhe.Lng.Should().BeApproximately(lng, 1e-9);
        detalhe.CriadoEm.Should().Be(fabrica.Relogio.GetUtcNow());
        detalhe.Confianca.Should().BeNull();
        detalhe.Faixa.Should().BeNull();
        detalhe.Avaliacao.Should().Be(new ObterDetalheProblema.Avaliacao(0, 5, detalhe.CriadoEm.AddHours(72)));
        detalhe.SouAutor.Should().BeTrue();
        detalhe.MeusVotos.Should().Be(new ObterDetalheProblema.MeusVotos(null, null));
    }

    [Fact]
    public async Task Registrar_com_nominatim_fora_do_ar_ainda_registra()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        fabrica.Nominatim.ForaDoAr(lat, lng);

        var resposta = await autor.PostarProblemaAsync(lat, lng);
        var detalhe = await resposta.LerAsync<ObterDetalheProblema.Response>();

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        detalhe.Endereco.Should().StartWith("Próximo a ");
    }

    [Fact]
    public async Task Registrar_com_categoria_de_incidente_devolve_422()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();

        var resposta = await autor.PostarProblemaAsync(lat, lng, categoriaId: 7);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await resposta.LerJsonAsync()).GetProperty("type").GetString().Should().Be("categoria-nao-e-problema");
    }

    [Theory]
    [InlineData(99, "categoriaId")]
    [InlineData(1, "descricao")]
    public async Task Registrar_invalido_devolve_400(short categoriaId, string campo)
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();

        var descricao = campo == "descricao" ? new string('x', 281) : null;

        var resposta = await autor.PostarProblemaAsync(lat, lng, categoriaId, descricao);
        var erros = (await resposta.LerJsonAsync()).GetProperty("errors");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        erros.TryGetProperty(campo, out _).Should().BeTrue();
    }

    [Fact]
    public async Task Registrar_exige_autenticacao()
    {
        var resposta = await fabrica.CreateClient().PostAsync("/api/v1/problemas", null);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Detalhe_para_visitante_nao_traz_meus_votos()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await autor.RegistrarProblemaAsync(lat, lng);

        var detalhe = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{problema.Id}"))
            .LerAsync<ObterDetalheProblema.Response>();

        detalhe.MeusVotos.Should().BeNull();
        detalhe.SouAutor.Should().BeFalse();
    }

    [Fact]
    public async Task Detalhe_inexistente_devolve_404()
    {
        var resposta = await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{Guid.NewGuid()}");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Duplicata_a_30m_e_encontrada_e_a_60m_nao()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var perto = await pessoa.RegistrarProblemaAsync(Cenarios.Norte(lat, 30), lng);
        await pessoa.RegistrarProblemaAsync(Cenarios.Norte(lat, 60), lng);
        await pessoa.RegistrarProblemaAsync(Cenarios.Norte(lat, 10), lng, categoriaId: 2); // outra categoria

        var resposta = await pessoa.Cliente.GetAsync(Inv($"/api/v1/problemas/duplicatas?categoriaId=1&lat={lat}&lng={lng}"));
        var duplicatas = await resposta.LerAsync<List<BuscarDuplicatas.Response>>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        duplicatas.Should().ContainSingle();
        duplicatas[0].Id.Should().Be(perto.Id);
        duplicatas[0].DistanciaMetros.Should().BeCloseTo(30, 1);
        duplicatas[0].Faixa.Should().BeNull("o problema ainda está em nova");
        duplicatas[0].Confirmacoes.Should().Be(0);
    }

    [Fact]
    public async Task Duplicatas_vem_ordenadas_por_distancia()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var quarenta = await pessoa.RegistrarProblemaAsync(Cenarios.Norte(lat, 40), lng);
        var dez = await pessoa.RegistrarProblemaAsync(Cenarios.Norte(lat, 10), lng);

        var duplicatas = await (await pessoa.Cliente.GetAsync(Inv($"/api/v1/problemas/duplicatas?categoriaId=1&lat={lat}&lng={lng}")))
            .LerAsync<List<BuscarDuplicatas.Response>>();

        duplicatas.Select(d => d.Id).Should().Equal(dez.Id, quarenta.Id);
    }

    [Fact]
    public async Task Listar_na_area_filtra_por_retangulo_grupo_e_esconde_confianca_em_nova()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var buraco = await pessoa.RegistrarProblemaAsync(lat, lng, categoriaId: 1);
        var poste = await pessoa.RegistrarProblemaAsync(Cenarios.Norte(lat, 20), lng, categoriaId: 3);
        await pessoa.RegistrarProblemaAsync(Cenarios.Norte(lat, 2000), lng); // fora do retângulo

        var area = Inv($"minLat={lat - 0.0005}&minLng={lng - 0.0005}&maxLat={lat + 0.0005}&maxLng={lng + 0.0005}");
        var todos = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas?{area}"))
            .LerAsync<List<ListarProblemasNaArea.Response>>();
        var eletrica = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas?{area}&grupos=eletrica"))
            .LerAsync<List<ListarProblemasNaArea.Response>>();

        todos.Select(p => p.Id).Should().BeEquivalentTo([buraco.Id, poste.Id]);
        todos.Should().AllSatisfy(p =>
        {
            p.Status.Should().Be(StatusProblema.Nova);
            p.Confianca.Should().BeNull();
            p.Faixa.Should().BeNull();
        });
        eletrica.Select(p => p.Id).Should().Equal(poste.Id);
    }

    [Fact]
    public async Task Listar_na_area_nunca_retorna_resolvida()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await pessoa.RegistrarProblemaAsync(lat, lng);
        await fabrica.NoBancoAsync(db => Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.ExecuteUpdateAsync(
            db.Problemas.Where(p => p.Id == problema.Id), s => s.SetProperty(p => p.Status, StatusProblema.Resolvida)));

        var area = Inv($"minLat={lat - 0.001}&minLng={lng - 0.001}&maxLat={lat + 0.001}&maxLng={lng + 0.001}");
        var lista = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas?{area}&status=nova,ativa,possivelmenteResolvida,resolvida"))
            .LerAsync<List<ListarProblemasNaArea.Response>>();

        lista.Should().NotContain(p => p.Id == problema.Id);
    }

    [Fact]
    public async Task Listar_na_area_respeita_o_retangulo_exato_em_viewport_largo()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        // Em geography as arestas do retângulo seriam geodésicas (a de lat 61 sobe até ~74° N em lng 0):
        // 70° N entraria e 60,5° N / 10° E ficaria de fora. O recorte precisa ser o retângulo de lat/lng.
        var fora = await pessoa.RegistrarProblemaAsync(70, 0);
        var dentro = await pessoa.RegistrarProblemaAsync(60.5, 10);

        var lista = await (await fabrica.CreateClient().GetAsync("/api/v1/problemas?minLat=60&minLng=-60&maxLat=61&maxLng=60"))
            .LerAsync<List<ListarProblemasNaArea.Response>>();

        lista.Select(p => p.Id).Should().Contain(dentro.Id).And.NotContain(fora.Id);
    }

    [Fact]
    public async Task Listar_na_area_pedindo_so_resolvida_devolve_vazio()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        await pessoa.RegistrarProblemaAsync(lat, lng);

        var area = Inv($"minLat={lat - 0.001}&minLng={lng - 0.001}&maxLat={lat + 0.001}&maxLng={lng + 0.001}");
        var lista = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas?{area}&status=resolvida"))
            .LerAsync<List<ListarProblemasNaArea.Response>>();

        lista.Should().BeEmpty();
    }

    [Theory]
    [InlineData("grupos=inexistente")]
    [InlineData("status=quebrado")]
    [InlineData("status=1")]
    [InlineData("status=999")]
    public async Task Listar_na_area_com_filtro_invalido_devolve_400(string filtro)
    {
        var resposta = await fabrica.CreateClient().GetAsync($"/api/v1/problemas?minLat=-23&minLng=-44&maxLat=-22&maxLng=-43&{filtro}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Theory]
    [InlineData("pagina=0")]
    [InlineData("pagina=2147483647&tamanho=100")]
    [InlineData("tamanho=101")]
    public async Task Recentes_com_paginacao_invalida_devolve_400(string paginacao)
    {
        var resposta = await fabrica.CreateClient().GetAsync($"/api/v1/problemas/recentes?lat=-22.95&lng=-43.18&{paginacao}");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Recentes_pagina_do_mais_novo_para_o_mais_antigo()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var ids = new List<Guid>();
        for (var i = 0; i < 3; i++)
        {
            ids.Add((await pessoa.RegistrarProblemaAsync(Cenarios.Norte(lat, i * 5), lng)).Id);
            fabrica.Relogio.Advance(TimeSpan.FromMinutes(1));
        }

        var url = Inv($"/api/v1/problemas/recentes?lat={lat}&lng={lng}&raioKm=0.05&tamanho=2");
        var pagina1 = await (await fabrica.CreateClient().GetAsync(url)).LerAsync<Paginado<ListarProblemasRecentes.Item>>();
        var pagina2 = await (await fabrica.CreateClient().GetAsync(url + "&pagina=2")).LerAsync<Paginado<ListarProblemasRecentes.Item>>();

        pagina1.Total.Should().Be(3);
        pagina1.Pagina.Should().Be(1);
        pagina1.Tamanho.Should().Be(2);
        pagina1.Itens.Select(i => i.Id).Should().Equal(ids[2], ids[1]);
        pagina2.Itens.Select(i => i.Id).Should().Equal(ids[0]);
    }
}
