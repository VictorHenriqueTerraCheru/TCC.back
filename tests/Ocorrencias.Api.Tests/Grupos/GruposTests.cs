using System.Net;
using System.Net.Http.Json;
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Grupos;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Tests.Grupos;

/// <summary>Os grupos criados aqui ficam no banco compartilhado; os rótulos são únicos por execução.</summary>
[Collection(ApiCollection.Nome)]
public sealed class GruposTests(ApiFactory fabrica)
{
    private static string RotuloUnico(string prefixo = "Grupo") => $"{prefixo} {Guid.NewGuid():N}"[..28];

    private static Task<HttpResponseMessage> CriarAsync(Pessoa admin, string rotulo, string tipo = "problema", string cor = "azul") =>
        admin.Cliente.PostAsJsonAsync("/api/v1/admin/grupos", new { rotulo, tipo, cor }, ClienteApi.Json);

    [Theory]
    [InlineData("Água e esgoto", "aguaEEsgoto")]
    [InlineData("Via pública", "viaPublica")]
    [InlineData("  trânsito   lento! ", "transitoLento")]
    [InlineData("123 obras", "grupo123Obras")]
    [InlineData("!!!", "grupo")]
    public void Chave_e_camelCase_sem_acento(string rotulo, string esperada) =>
        GrupoService.ChaveDe(rotulo).Should().Be(esperada);

    [Fact]
    public async Task Visitante_lista_o_seed_em_ordem()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/api/v1/grupos");
        var grupos = await resposta.LerAsync<List<GrupoResponse>>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        grupos.Take(5).Select(g => g.Chave).Should().Equal("viaPublica", "eletrica", "limpeza", "seguranca", "transito");
        grupos[0].Should().Be(new GrupoResponse("viaPublica", "Via pública", TipoOcorrencia.Problema, CorGrupo.Laranja, 1));
        grupos[3].Tipo.Should().Be(TipoOcorrencia.Incidente);
    }

    [Fact]
    public async Task Rotas_de_admin_exigem_perfil_administrador()
    {
        var cidadao = await fabrica.NovaPessoaAsync();

        (await CriarAsync(cidadao, RotuloUnico())).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cidadao.Cliente.PutAsJsonAsync("/api/v1/admin/grupos/limpeza", new { rotulo = "x", cor = "azul", ordem = 1 }, ClienteApi.Json))
            .StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await cidadao.Cliente.DeleteAsync("/api/v1/admin/grupos/limpeza")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fabrica.CreateClient().DeleteAsync("/api/v1/admin/grupos/limpeza")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Criar_gera_chave_sem_acento_e_ordem_no_fim()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var rotulo = RotuloUnico("Água");
        var maior = (await (await fabrica.CreateClient().GetAsync("/api/v1/grupos")).LerAsync<List<GrupoResponse>>()).Max(g => g.Ordem);

        var resposta = await CriarAsync(admin, rotulo, "incidente", "verdeAgua");
        var criado = await resposta.LerAsync<GrupoResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        criado.Chave.Should().StartWith("agua").And.MatchRegex("^[a-z][a-zA-Z0-9]*$");
        criado.Should().Be(new GrupoResponse(criado.Chave, rotulo, TipoOcorrencia.Incidente, CorGrupo.VerdeAgua, maior + 1));
    }

    [Fact]
    public async Task Rotulo_duplicado_ignora_maiusculas_e_chave_repetida_ganha_sufixo()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var rotulo = RotuloUnico("Zona");

        var primeiro = await (await CriarAsync(admin, rotulo)).LerAsync<GrupoResponse>();
        var duplicado = await CriarAsync(admin, rotulo.ToUpperInvariant());
        var mesmaChave = await CriarAsync(admin, rotulo + "!");

        duplicado.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await duplicado.LerJsonAsync()).GetProperty("type").GetString().Should().Be("grupo-rotulo-duplicado");
        mesmaChave.StatusCode.Should().Be(HttpStatusCode.Created);
        (await mesmaChave.LerAsync<GrupoResponse>()).Chave.Should().Be(primeiro.Chave + "2");
    }

    [Theory]
    [InlineData("", "problema", "azul")]
    [InlineData("Ok", "outro", "azul")]
    [InlineData("Ok", "problema", "turquesa")]
    public async Task Criar_valida_o_corpo(string rotulo, string tipo, string cor)
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);

        (await CriarAsync(admin, rotulo, tipo, cor)).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Editar_troca_rotulo_cor_e_ordem_mas_nao_o_tipo_nem_a_chave()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var criado = await (await CriarAsync(admin, RotuloUnico())).LerAsync<GrupoResponse>();
        var novo = RotuloUnico("Novo");

        var resposta = await admin.Cliente.PutAsJsonAsync($"/api/v1/admin/grupos/{criado.Chave}",
            new { rotulo = novo, cor = "roxo", ordem = 99, tipo = "incidente" }, ClienteApi.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        (await resposta.LerAsync<GrupoResponse>()).Should().Be(new GrupoResponse(criado.Chave, novo, TipoOcorrencia.Problema, CorGrupo.Roxo, 99));
    }

    [Fact]
    public async Task Editar_para_rotulo_de_outro_grupo_devolve_409_e_chave_inexistente_404()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var criado = await (await CriarAsync(admin, RotuloUnico())).LerAsync<GrupoResponse>();

        var conflito = await admin.Cliente.PutAsJsonAsync($"/api/v1/admin/grupos/{criado.Chave}",
            new { rotulo = "limpeza", cor = "azul", ordem = 9 }, ClienteApi.Json);
        var inexistente = await admin.Cliente.PutAsJsonAsync("/api/v1/admin/grupos/naoExiste",
            new { rotulo = "x", cor = "azul", ordem = 9 }, ClienteApi.Json);

        conflito.StatusCode.Should().Be(HttpStatusCode.Conflict);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Apagar_so_funciona_com_o_grupo_vazio()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var vazio = await (await CriarAsync(admin, RotuloUnico())).LerAsync<GrupoResponse>();
        var ocupado = await (await CriarAsync(admin, RotuloUnico())).LerAsync<GrupoResponse>();
        var categoria = await (await admin.Cliente.PostAsJsonAsync("/api/v1/admin/categorias",
            new { nome = RotuloUnico("Cat"), grupo = ocupado.Chave, tipo = "problema", icone = "x" }, ClienteApi.Json)).LerAsync<CategoriaAdminResponse>();
        try
        {
            var apagou = await admin.Cliente.DeleteAsync($"/api/v1/admin/grupos/{vazio.Chave}");
            var barrou = await admin.Cliente.DeleteAsync($"/api/v1/admin/grupos/{ocupado.Chave}");
            await admin.Cliente.PatchAsJsonAsync($"/api/v1/admin/categorias/{categoria.Id}/ativa", new { ativa = false }, ClienteApi.Json);
            var aindaBarra = await admin.Cliente.DeleteAsync($"/api/v1/admin/grupos/{ocupado.Chave}");
            var denovo = await admin.Cliente.DeleteAsync($"/api/v1/admin/grupos/{vazio.Chave}");

            apagou.StatusCode.Should().Be(HttpStatusCode.NoContent);
            barrou.StatusCode.Should().Be(HttpStatusCode.Conflict);
            (await barrou.LerJsonAsync()).GetProperty("type").GetString().Should().Be("grupo-em-uso");
            aindaBarra.StatusCode.Should().Be(HttpStatusCode.Conflict);
            denovo.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
        finally
        {
            await admin.Cliente.PatchAsJsonAsync($"/api/v1/admin/categorias/{categoria.Id}/ativa", new { ativa = false }, ClienteApi.Json);
        }
    }
}
