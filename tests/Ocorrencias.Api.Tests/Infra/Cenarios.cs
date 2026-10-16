using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Features.Problemas;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Tests.Infra;

/// <summary>Pessoa autenticada nos testes.</summary>
public sealed record Pessoa(Guid Id, HttpClient Cliente);

public static class Cenarios
{
    /// <summary>Metros por grau de latitude usados para deslocar pontos (aproximação suficiente para 30–500 m).</summary>
    public const double MetrosPorGrauLat = 111_320;

    public static double Norte(double lat, double metros) => lat + metros / MetrosPorGrauLat;

    /// <summary>Cadastra pela API; <paramref name="perfil"/> diferente de cidadão é gravado direto no banco.</summary>
    public static async Task<Pessoa> NovaPessoaAsync(this ApiFactory fabrica, Perfil perfil = Perfil.Cidadao)
    {
        var cliente = fabrica.CreateClient();
        var auth = await cliente.CadastrarAsync();
        cliente.Autenticado(auth.Token);

        if (perfil != Perfil.Cidadao)
        {
            await fabrica.NoBancoAsync(db => db.Users
                .Where(u => u.Id == auth.Usuario.Id)
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.Perfil, perfil)));

            // Novo login: a claim "perfil" do token (usada pela policy de admin) precisa refletir o banco.
            var login = await cliente.PostAsJsonAsync("/api/v1/auth/login", new { email = auth.Usuario.Email, senha = "senha-forte" }, ClienteApi.Json);
            login.EnsureSuccessStatusCode();
            cliente.Autenticado((await login.LerAsync<Features.Auth.RespostaAuth>()).Token);
        }

        return new Pessoa(auth.Usuario.Id, cliente);
    }

    public static Task<HttpResponseMessage> PostarProblemaAsync(
        this Pessoa pessoa, double lat, double lng, short categoriaId = 1, string? descricao = null) =>
        pessoa.Cliente.PostAsJsonAsync("/api/v1/problemas", new { categoriaId, lat, lng, descricao }, ClienteApi.Json);

    public static async Task<ObterDetalheProblema.Response> RegistrarProblemaAsync(
        this Pessoa pessoa, double lat, double lng, short categoriaId = 1)
    {
        var resposta = await pessoa.PostarProblemaAsync(lat, lng, categoriaId);
        resposta.EnsureSuccessStatusCode();
        return await resposta.LerAsync<ObterDetalheProblema.Response>();
    }

    public static Task<HttpResponseMessage> VotarExistenciaAsync(
        this Pessoa pessoa, Guid problemaId, string valor, double? lat = null, double? lng = null) =>
        pessoa.Cliente.PutAsJsonAsync($"/api/v1/problemas/{problemaId}/votos/existencia", new { valor, lat, lng }, ClienteApi.Json);

    public static Task<HttpResponseMessage> VotarPersistenciaAsync(
        this Pessoa pessoa, Guid problemaId, string valor, double? lat = null, double? lng = null) =>
        pessoa.Cliente.PutAsJsonAsync($"/api/v1/problemas/{problemaId}/votos/persistencia", new { valor, lat, lng }, ClienteApi.Json);

    /// <summary>Registra um problema e o leva a <c>ativa</c> com 5 confirmações sem localização.</summary>
    public static async Task<ObterDetalheProblema.Response> ProblemaAtivoAsync(this ApiFactory fabrica, double lat, double lng)
    {
        var autor = await fabrica.NovaPessoaAsync();
        var problema = await autor.RegistrarProblemaAsync(lat, lng);

        ObterDetalheProblema.Response? detalhe = null;
        for (var i = 0; i < 5; i++)
        {
            var votante = await fabrica.NovaPessoaAsync();
            var resposta = await votante.VotarExistenciaAsync(problema.Id, "confirma");
            resposta.EnsureSuccessStatusCode();
            detalhe = await resposta.LerAsync<ObterDetalheProblema.Response>();
        }

        detalhe!.Status.Should().Be(StatusProblema.Ativa);
        return detalhe;
    }
}
