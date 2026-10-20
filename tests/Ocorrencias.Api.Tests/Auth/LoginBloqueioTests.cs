using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Tests.Infra;

namespace Ocorrencias.Api.Tests.Auth;

[Collection(ApiCollection.Nome)]
public sealed class LoginBloqueioTests(ApiFactory fabrica)
{
    private const int MaximoFalhas = 5;
    private readonly HttpClient _cliente = fabrica.CreateClient();

    private Task<HttpResponseMessage> Login(string email, string senha) =>
        _cliente.PostAsJsonAsync("/api/v1/auth/login", new { email, senha }, ClienteApi.Json);

    private async Task Falhar(string email, int vezes)
    {
        for (var i = 0; i < vezes; i++)
        {
            (await Login(email, "senha-errada")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        }
    }

    /// <summary>Corpo do problema sem o <c>traceId</c>, que muda a cada requisição.</summary>
    private static async Task<string> SemTraceId(HttpResponseMessage resposta) =>
        string.Join(";", (await resposta.LerJsonAsync()).EnumerateObject()
            .Where(p => p.Name != "traceId")
            .Select(p => $"{p.Name}={p.Value}"));

    [Fact]
    public async Task Apos_N_falhas_a_conta_bloqueia_mesmo_com_a_senha_certa()
    {
        var email = ClienteApi.EmailUnico();
        await _cliente.CadastrarAsync(email);

        await Falhar(email, MaximoFalhas);

        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Falhas_simultaneas_contam_todas_e_bloqueiam()
    {
        var email = ClienteApi.EmailUnico();
        await _cliente.CadastrarAsync(email);

        // Tentativas em paralelo não podem se atropelar: cada falha tem que contar.
        var respostas = await Task.WhenAll(Enumerable.Range(0, MaximoFalhas).Select(_ => Login(email, "senha-errada")));
        respostas.Should().OnlyContain(r => r.StatusCode == HttpStatusCode.Unauthorized);

        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Antes_de_N_falhas_a_senha_certa_entra_e_zera_o_contador()
    {
        var email = ClienteApi.EmailUnico();
        await _cliente.CadastrarAsync(email);

        await Falhar(email, MaximoFalhas - 1);
        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.OK);

        // Contador zerado: mais N-1 falhas não bloqueiam.
        await Falhar(email, MaximoFalhas - 1);
        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Resposta_de_conta_bloqueada_e_identica_a_de_credencial_invalida()
    {
        var email = ClienteApi.EmailUnico();
        await _cliente.CadastrarAsync(email);
        var invalida = await Login(email, "senha-errada");
        await Falhar(email, MaximoFalhas);

        var bloqueada = await Login(email, "senha-forte");

        bloqueada.StatusCode.Should().Be(invalida.StatusCode);
        (await SemTraceId(bloqueada)).Should().Be(await SemTraceId(invalida));
        bloqueada.Headers.Contains("Retry-After").Should().BeFalse();
    }

    [Fact]
    public async Task Bloqueio_de_uma_conta_nao_afeta_o_login_de_outra()
    {
        var bloqueado = ClienteApi.EmailUnico();
        var outro = ClienteApi.EmailUnico();
        await _cliente.CadastrarAsync(bloqueado);
        await _cliente.CadastrarAsync(outro);

        await Falhar(bloqueado, MaximoFalhas);

        (await Login(outro, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Conta_volta_a_entrar_quando_o_bloqueio_termina()
    {
        var email = ClienteApi.EmailUnico();
        await _cliente.CadastrarAsync(email);
        await Falhar(email, MaximoFalhas);
        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);

        // O Identity confere o fim do bloqueio com o relógio do sistema; vence o bloqueio direto no banco.
        await fabrica.NoBancoAsync(db => db.Database.ExecuteSqlAsync(
            $"UPDATE usuarios SET lockout_end = now() - interval '1 minute' WHERE normalized_email = {email.ToUpperInvariant()}"));

        (await Login(email, "senha-forte")).StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
