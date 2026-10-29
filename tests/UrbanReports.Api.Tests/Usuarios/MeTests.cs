using System.Net;
using System.Net.Http.Json;
using UrbanReports.Api.Usuarios;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Usuarios;

[Collection(ApiCollection.Nome)]
public sealed class MeTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Me_sem_token_devolve_401()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/api/v1/me");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Me_com_token_invalido_devolve_401()
    {
        var cliente = fabrica.CreateClient().Autenticado("nao.e.um-jwt");

        var resposta = await cliente.GetAsync("/api/v1/me");

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Cadastro_login_e_me_funcionam_em_sequencia()
    {
        var cliente = fabrica.CreateClient();
        var auth = await cliente.CadastrarAsync();

        var resposta = await cliente.Autenticado(auth.Token).GetAsync("/api/v1/me");
        var me = await resposta.LerAsync<ObterMeResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        me.Id.Should().Be(auth.Usuario.Id);
        me.Email.Should().Be(auth.Usuario.Email);
        me.Perfil.Should().Be(Perfil.Cidadao);
        me.CriadoEm.Should().Be(fabrica.Relogio.GetUtcNow());
        me.VerificadoEm.Should().BeNull();
        me.TotalRegistros.Should().Be(0);
        me.TotalVotos.Should().Be(0);
        me.Verificacao.Should().BeNull();
    }

    [Fact]
    public async Task Patch_me_atualiza_o_nome()
    {
        var cliente = fabrica.CreateClient();
        var auth = await cliente.CadastrarAsync();
        cliente.Autenticado(auth.Token);

        var resposta = await cliente.PatchAsJsonAsync("/api/v1/me", new { nome = "  Mariana S.  " }, ClienteApi.Json);
        var me = await resposta.LerAsync<ObterMeResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        me.Nome.Should().Be("Mariana S.");
        (await (await cliente.GetAsync("/api/v1/me")).LerAsync<ObterMeResponse>()).Nome.Should().Be("Mariana S.");
    }

    [Fact]
    public async Task Patch_me_com_nome_vazio_devolve_400()
    {
        var cliente = fabrica.CreateClient();
        cliente.Autenticado((await cliente.CadastrarAsync()).Token);

        var resposta = await cliente.PatchAsJsonAsync("/api/v1/me", new { nome = "" }, ClienteApi.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
