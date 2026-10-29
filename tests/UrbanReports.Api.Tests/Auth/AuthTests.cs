using System.Net;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using UrbanReports.Api.Auth;
using UrbanReports.Api.Tests.Infra;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Auth;

[Collection(ApiCollection.Nome)]
public sealed class AuthTests(ApiFactory fabrica)
{
    private readonly HttpClient _cliente = fabrica.CreateClient();

    [Fact]
    public async Task Cadastro_cria_cidadao_e_devolve_token_de_7_dias()
    {
        var email = ClienteApi.EmailUnico();

        var resposta = await _cliente.PostarCadastroAsync(email, nome: "Mariana Souza");
        var corpo = await resposta.LerAsync<RespostaAuth>();

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        corpo.Usuario.Email.Should().Be(email);
        corpo.Usuario.Nome.Should().Be("Mariana Souza");
        corpo.Usuario.Perfil.Should().Be(Perfil.Cidadao);
        corpo.ExpiraEm.Should().Be(fabrica.Relogio.GetUtcNow().AddDays(7));

        var token = new JsonWebToken(corpo.Token);
        token.Subject.Should().Be(corpo.Usuario.Id.ToString());
        token.GetClaim("email").Value.Should().Be(email);
        token.GetClaim("nome").Value.Should().Be("Mariana Souza");
        token.GetClaim("perfil").Value.Should().Be("cidadao");
    }

    [Theory]
    [InlineData("o'brien")]
    [InlineData("joão.conceição")]
    public async Task Cadastro_aceita_emails_validos_com_apostrofo_ou_acento(string local)
    {
        var resposta = await _cliente.PostarCadastroAsync($"{local}.{Guid.NewGuid():N}@teste.local");

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }

    [Fact]
    public async Task Cadastro_com_email_existente_devolve_409_email_ja_cadastrado()
    {
        var email = ClienteApi.EmailUnico();
        await _cliente.CadastrarAsync(email);

        var resposta = await _cliente.PostarCadastroAsync(email.ToUpperInvariant());
        var problema = await resposta.LerJsonAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        problema.GetProperty("type").GetString().Should().Be("email-ja-cadastrado");
    }

    [Fact]
    public async Task Cadastro_invalido_devolve_400_com_campos_em_camel_case()
    {
        var resposta = await _cliente.PostAsJsonAsync("/api/v1/auth/cadastro",
            new { nome = "", email = "nao-e-email", senha = "curta", aceitouTermos = false }, ClienteApi.Json);
        var erros = (await resposta.LerJsonAsync()).GetProperty("errors");

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        erros.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("nome", "email", "senha", "aceitouTermos");
    }

    [Fact]
    public async Task Login_com_credenciais_corretas_devolve_200()
    {
        var email = ClienteApi.EmailUnico();
        await _cliente.CadastrarAsync(email);

        var resposta = await _cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, senha = "senha-forte" }, ClienteApi.Json);
        var corpo = await resposta.LerAsync<RespostaAuth>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        corpo.Usuario.Email.Should().Be(email);
        corpo.Token.Should().NotBeNullOrWhiteSpace();
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Login_invalido_devolve_401(bool emailExiste)
    {
        var email = ClienteApi.EmailUnico();
        if (emailExiste)
        {
            await _cliente.CadastrarAsync(email);
        }

        var resposta = await _cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, senha = "senha-errada" }, ClienteApi.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Token_expira_em_7_dias_pelo_relogio_da_aplicacao()
    {
        var auth = await _cliente.CadastrarAsync();
        var cliente = fabrica.CreateClient().Autenticado(auth.Token);
        (await cliente.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.OK);

        fabrica.Relogio.Advance(TimeSpan.FromDays(7) + TimeSpan.FromMinutes(6));

        (await cliente.GetAsync("/api/v1/me")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
