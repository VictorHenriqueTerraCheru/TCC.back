using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Grupos;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Tests.Categorias;

/// <summary>
/// As categorias criadas aqui são desativadas no fim: a coleção compartilha o banco e outros testes
/// esperam as categorias do seed (ex.: contagens da área de incidentes).
/// </summary>
[Collection(ApiCollection.Nome)]
public sealed class CategoriasAdminTests(ApiFactory fabrica)
{
    private static string NomeUnico() => $"Categoria {Guid.NewGuid():N}"[..30];

    private static Task<HttpResponseMessage> CriarAsync(Pessoa admin, string nome, string tipo = "incidente", string grupo = "seguranca", string icone = "inundacao") =>
        admin.Cliente.PostAsJsonAsync("/api/v1/admin/categorias", new { nome, grupo, tipo, icone }, ClienteApi.Json);

    private async Task DesativarAsync(Pessoa admin, params short[] ids)
    {
        foreach (var id in ids)
        {
            await admin.Cliente.PatchAsJsonAsync($"/api/v1/admin/categorias/{id}/ativa", new { ativa = false }, ClienteApi.Json);
        }
    }

    [Fact]
    public async Task Rotas_de_admin_exigem_perfil_administrador()
    {
        var cidadao = await fabrica.NovaPessoaAsync();
        var corpo = new { nome = NomeUnico(), grupo = "seguranca", tipo = "incidente", icone = "inundacao" };

        (await cidadao.Cliente.GetAsync("/api/v1/admin/categorias")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cidadao.Cliente.PostAsJsonAsync("/api/v1/admin/categorias", corpo, ClienteApi.Json)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cidadao.Cliente.PutAsJsonAsync("/api/v1/admin/categorias/1", new { nome = "x", grupo = "seguranca", icone = "x" }, ClienteApi.Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cidadao.Cliente.PatchAsJsonAsync("/api/v1/admin/categorias/1/ativa", new { ativa = false }, ClienteApi.Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fabrica.CreateClient().GetAsync("/api/v1/admin/categorias")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Criar_gera_o_proximo_id_e_a_categoria_ja_serve_para_registrar_incidente()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var pessoa = await fabrica.NovaPessoaAsync();
        var maior = await fabrica.NoBancoAsync(db => db.Categorias.MaxAsync(c => c.Id));
        var nome = NomeUnico();

        var resposta = await CriarAsync(admin, nome);
        var criada = await resposta.LerAsync<CategoriaAdminResponse>();
        try
        {
            resposta.StatusCode.Should().Be(HttpStatusCode.Created);
            criada.Should().Be(new CategoriaAdminResponse((short)(maior + 1), nome, "seguranca", TipoOcorrencia.Incidente, "inundacao", true));

            var publica = (await (await fabrica.CreateClient().GetAsync("/api/v1/categorias")).LerJsonAsync()).EnumerateArray();
            publica.Select(c => c.GetProperty("id").GetInt32()).Should().Contain(criada.Id);

            var (lat, lng) = ApiFactory.AreaIsolada();
            var incidente = await pessoa.Cliente.PostAsJsonAsync("/api/v1/incidentes",
                new { categoriaId = criada.Id, lat, lng, ocorridoEm = fabrica.Relogio.GetUtcNow().AddMinutes(-30) }, ClienteApi.Json);
            incidente.StatusCode.Should().Be(HttpStatusCode.Created);
        }
        finally
        {
            await DesativarAsync(admin, criada.Id);
        }
    }

    [Fact]
    public async Task Criacoes_simultaneas_recebem_ids_distintos_e_sequenciais()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => CriarAsync(admin, NomeUnico())));
        var ids = new List<short>();
        try
        {
            foreach (var r in respostas)
            {
                r.StatusCode.Should().Be(HttpStatusCode.Created);
                ids.Add((await r.LerAsync<CategoriaAdminResponse>()).Id);
            }

            ids.Distinct().Should().HaveCount(6);
            ids.Order().Zip(ids.Order().Skip(1), (a, b) => b - a).Should().OnlyContain(d => d == 1);
        }
        finally
        {
            await DesativarAsync(admin, [.. ids]);
        }
    }

    [Fact]
    public async Task Nome_duplicado_ignora_maiusculas_e_devolve_409()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);

        var resposta = await CriarAsync(admin, "BURACO NA VIA");
        var editar = await admin.Cliente.PutAsJsonAsync("/api/v1/admin/categorias/2",
            new { nome = "buraco na via", grupo = "viaPublica", icone = "calcada" }, ClienteApi.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await resposta.LerJsonAsync()).GetProperty("type").GetString().Should().Be("nome-duplicado");
        editar.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Theory]
    [InlineData("", "clima", "incidente", "inundacao")]
    [InlineData("Nome ok", "", "incidente", "inundacao")]
    [InlineData("Nome ok", "clima", "incidente", "Icone Invalido")]
    [InlineData("Nome ok", "clima", "outro", "inundacao")]
    public async Task Criar_valida_o_corpo(string nome, string grupo, string tipo, string icone)
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);

        (await CriarAsync(admin, nome, tipo, grupo, icone)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Desativar_esconde_da_lista_publica_barra_registro_novo_e_preserva_o_historico()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var pessoa = await fabrica.NovaPessoaAsync();
        var criada = await (await CriarAsync(admin, NomeUnico(), "problema", "limpeza", "entulho")).LerAsync<CategoriaAdminResponse>();
        try
        {
            await VerificarDesativacaoAsync(admin, pessoa, criada);
        }
        finally
        {
            await DesativarAsync(admin, criada.Id);
        }
    }

    private async Task VerificarDesativacaoAsync(Pessoa admin, Pessoa pessoa, CategoriaAdminResponse criada)
    {
        var (lat, lng) = ApiFactory.PontoUnico();

        var antes = await pessoa.Cliente.PostAsJsonAsync("/api/v1/problemas", new { categoriaId = criada.Id, lat, lng }, ClienteApi.Json);
        var id = (await antes.LerJsonAsync()).GetProperty("id").GetGuid();
        var desativar = await admin.Cliente.PatchAsJsonAsync($"/api/v1/admin/categorias/{criada.Id}/ativa", new { ativa = false }, ClienteApi.Json);

        var publica = (await (await fabrica.CreateClient().GetAsync("/api/v1/categorias")).LerJsonAsync()).EnumerateArray();
        var todas = (await (await admin.Cliente.GetAsync("/api/v1/admin/categorias")).LerJsonAsync()).EnumerateArray();
        var (lat2, lng2) = ApiFactory.PontoUnico();
        var depois = await pessoa.Cliente.PostAsJsonAsync("/api/v1/problemas", new { categoriaId = criada.Id, lat = lat2, lng = lng2 }, ClienteApi.Json);
        var detalhe = await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{id}");

        antes.StatusCode.Should().Be(HttpStatusCode.Created);
        desativar.StatusCode.Should().Be(HttpStatusCode.NoContent);
        publica.Select(c => c.GetProperty("id").GetInt32()).Should().NotContain(criada.Id);
        todas.Single(c => c.GetProperty("id").GetInt32() == criada.Id).GetProperty("ativa").GetBoolean().Should().BeFalse();
        depois.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await depois.LerJsonAsync()).GetProperty("errors").TryGetProperty("categoriaId", out _).Should().BeTrue();
        detalhe.StatusCode.Should().Be(HttpStatusCode.OK);
        (await detalhe.LerJsonAsync()).GetProperty("categoria").GetProperty("id").GetInt32().Should().Be(criada.Id);
    }

    [Fact]
    public async Task Editar_troca_nome_grupo_e_icone_mas_nao_o_tipo()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var criada = await (await CriarAsync(admin, NomeUnico())).LerAsync<CategoriaAdminResponse>();
        var novoNome = NomeUnico();
        try
        {
            var resposta = await admin.Cliente.PutAsJsonAsync($"/api/v1/admin/categorias/{criada.Id}",
                new { nome = novoNome, grupo = "transito", icone = "chuva", tipo = "problema" }, ClienteApi.Json);

            resposta.StatusCode.Should().Be(HttpStatusCode.OK);
            (await resposta.LerAsync<CategoriaAdminResponse>()).Should().Be(
                new CategoriaAdminResponse(criada.Id, novoNome, "transito", TipoOcorrencia.Incidente, "chuva", true));
        }
        finally
        {
            await DesativarAsync(admin, criada.Id);
        }
    }

    [Fact]
    public async Task Alterar_ativa_sem_o_campo_devolve_400_e_nao_desativa()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var criada = await (await CriarAsync(admin, NomeUnico())).LerAsync<CategoriaAdminResponse>();
        try
        {
            var vazio = await admin.Cliente.PatchAsJsonAsync($"/api/v1/admin/categorias/{criada.Id}/ativa", new { }, ClienteApi.Json);
            var errado = await admin.Cliente.PatchAsJsonAsync($"/api/v1/admin/categorias/{criada.Id}/ativa", new { ativo = false }, ClienteApi.Json);

            vazio.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            errado.StatusCode.Should().Be(HttpStatusCode.BadRequest);
            var publica = (await (await fabrica.CreateClient().GetAsync("/api/v1/categorias")).LerJsonAsync()).EnumerateArray();
            publica.Select(c => c.GetProperty("id").GetInt32()).Should().Contain(criada.Id);
        }
        finally
        {
            await DesativarAsync(admin, criada.Id);
        }
    }

    [Fact]
    public async Task Renomear_em_paralelo_para_o_mesmo_nome_deixa_so_uma_vencer()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var a = await (await CriarAsync(admin, NomeUnico())).LerAsync<CategoriaAdminResponse>();
        var b = await (await CriarAsync(admin, NomeUnico())).LerAsync<CategoriaAdminResponse>();
        var alvo = NomeUnico();
        try
        {
            var respostas = await Task.WhenAll(
                admin.Cliente.PutAsJsonAsync($"/api/v1/admin/categorias/{a.Id}",
                    new { nome = alvo.ToLowerInvariant(), grupo = "seguranca", icone = "x" }, ClienteApi.Json),
                admin.Cliente.PutAsJsonAsync($"/api/v1/admin/categorias/{b.Id}",
                    new { nome = alvo.ToUpperInvariant(), grupo = "seguranca", icone = "x" }, ClienteApi.Json));

            respostas.Select(r => r.StatusCode).Should().BeEquivalentTo([HttpStatusCode.OK, HttpStatusCode.Conflict]);
        }
        finally
        {
            await DesativarAsync(admin, a.Id, b.Id);
        }
    }

    [Fact]
    public async Task Id_inexistente_devolve_404()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);

        (await admin.Cliente.PutAsJsonAsync("/api/v1/admin/categorias/30000", new { nome = "x", grupo = "seguranca", icone = "x" }, ClienteApi.Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await admin.Cliente.PatchAsJsonAsync("/api/v1/admin/categorias/30000/ativa", new { ativa = true }, ClienteApi.Json))
            .StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Grupo_inexistente_devolve_400_e_grupo_de_outro_tipo_devolve_422()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);

        var inexistente = await CriarAsync(admin, NomeUnico(), "incidente", "naoExiste");
        var incompativel = await CriarAsync(admin, NomeUnico(), "problema", "seguranca");
        var editar = await admin.Cliente.PutAsJsonAsync("/api/v1/admin/categorias/1",
            new { nome = "Buraco na via", grupo = "seguranca", icone = "buraco" }, ClienteApi.Json);

        inexistente.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await inexistente.LerJsonAsync()).GetProperty("errors").TryGetProperty("grupo", out _).Should().BeTrue();
        incompativel.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await incompativel.LerJsonAsync()).GetProperty("type").GetString().Should().Be("grupo-tipo-incompativel");
        editar.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
    }

    [Fact]
    public async Task Grupo_novo_vale_no_filtro_de_problemas()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var grupo = await (await admin.Cliente.PostAsJsonAsync("/api/v1/admin/grupos",
            new { rotulo = $"Grupo {Guid.NewGuid():N}"[..20], tipo = "problema", cor = "azul" }, ClienteApi.Json)).LerAsync<GrupoResponse>();
        var criada = await (await CriarAsync(admin, NomeUnico(), "problema", grupo.Chave, "x")).LerAsync<CategoriaAdminResponse>();
        try
        {
            var area = "minLat=-23&maxLat=-22&minLng=-44&maxLng=-43";

            (await fabrica.CreateClient().GetAsync($"/api/v1/problemas?{area}&grupos={grupo.Chave}")).StatusCode.Should().Be(HttpStatusCode.OK);
            (await fabrica.CreateClient().GetAsync($"/api/v1/problemas?{area}&grupos={grupo.Chave}x")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
        finally
        {
            await DesativarAsync(admin, criada.Id);
        }
    }
}
