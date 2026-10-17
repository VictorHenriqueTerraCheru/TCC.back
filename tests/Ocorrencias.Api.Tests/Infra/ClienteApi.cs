using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Auth;

namespace Ocorrencias.Api.Tests.Infra;

public static class ClienteApi
{
    public static readonly JsonSerializerOptions Json = CriarJson();

    private static JsonSerializerOptions CriarJson()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonConfig.Configurar(opcoes);
        return opcoes;
    }

    public static string EmailUnico() => $"u{Guid.NewGuid():N}@teste.local";

    public static Task<HttpResponseMessage> PostarCadastroAsync(this HttpClient cliente, string email, string senha = "senha-forte", string nome = "Pessoa Teste") =>
        cliente.PostAsJsonAsync("/api/v1/auth/cadastro", new { nome, email, senha, aceitouTermos = true }, Json);

    public static async Task<RespostaAuth> CadastrarAsync(this HttpClient cliente, string? email = null)
    {
        var resposta = await cliente.PostarCadastroAsync(email ?? EmailUnico());
        resposta.EnsureSuccessStatusCode();
        return (await resposta.Content.ReadFromJsonAsync<RespostaAuth>(Json))!;
    }

    public static HttpClient Autenticado(this HttpClient cliente, string token)
    {
        cliente.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return cliente;
    }

    public static async Task<T> LerAsync<T>(this HttpResponseMessage resposta) =>
        (await resposta.Content.ReadFromJsonAsync<T>(Json))!;

    public static async Task<JsonElement> LerJsonAsync(this HttpResponseMessage resposta) =>
        await resposta.Content.ReadFromJsonAsync<JsonElement>(Json);
}
