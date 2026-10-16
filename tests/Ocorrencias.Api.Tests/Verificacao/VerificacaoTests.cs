using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Admin;
using Ocorrencias.Api.Usuarios;
using Ocorrencias.Api.Verificacao;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Tests.Verificacao;

[Collection(ApiCollection.Nome)]
public sealed class VerificacaoTests(ApiFactory fabrica)
{
    private const string Justificativa = "Moro em Botafogo há dez anos e circulo pelo bairro todos os dias.";

    private static Task<HttpResponseMessage> SolicitarAsync(Pessoa pessoa, string justificativa = Justificativa, string tipoVinculo = "moradorRegiao") =>
        pessoa.Cliente.PostAsJsonAsync("/api/v1/verificacoes", new { tipoVinculo, justificativa }, ClienteApi.Json);

    private static async Task<Guid> SolicitacaoIdAsync(Pessoa pessoa)
    {
        var resposta = await SolicitarAsync(pessoa);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.LerAsync<SolicitarVerificacaoResponse>()).Id;
    }

    private static async Task<string?> TipoErroAsync(HttpResponseMessage resposta) =>
        (await resposta.LerJsonAsync()).GetProperty("type").GetString();

    [Fact]
    public async Task Solicitar_cria_pendente_e_aparece_no_me()
    {
        var pessoa = await fabrica.NovaPessoaAsync();

        var resposta = await SolicitarAsync(pessoa);
        var corpo = await resposta.LerAsync<SolicitarVerificacaoResponse>();
        var me = await (await pessoa.Cliente.GetAsync("/api/v1/me")).LerAsync<ObterMeResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
        corpo.Status.Should().Be(StatusVerificacao.Pendente);
        corpo.TipoVinculo.Should().Be(TipoVinculo.MoradorRegiao);
        me.Verificacao.Should().Be(new ObterMeVerificacaoResumo(StatusVerificacao.Pendente, TipoVinculo.MoradorRegiao, null, corpo.CriadaEm));
    }

    [Theory]
    [InlineData(29)]
    [InlineData(501)]
    public async Task Justificativa_fora_de_30_a_500_caracteres_devolve_400(int tamanho)
    {
        var pessoa = await fabrica.NovaPessoaAsync();

        var resposta = await SolicitarAsync(pessoa, new string('a', tamanho));

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Justificativa_nula_devolve_400()
    {
        var pessoa = await fabrica.NovaPessoaAsync();

        var resposta = await pessoa.Cliente.PostAsJsonAsync("/api/v1/verificacoes",
            new { tipoVinculo = "moradorRegiao", justificativa = (string?)null }, ClienteApi.Json);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Segunda_pendente_devolve_409()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        await SolicitacaoIdAsync(pessoa);

        var resposta = await SolicitarAsync(pessoa);

        resposta.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await TipoErroAsync(resposta)).Should().Be("verificacao-pendente-existente");
    }

    [Fact]
    public async Task Pendentes_simultaneas_nao_passam_do_indice_unico()
    {
        var pessoa = await fabrica.NovaPessoaAsync();

        var respostas = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => SolicitarAsync(pessoa)));

        respostas.Count(r => r.StatusCode == HttpStatusCode.Created).Should().Be(1);
        respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(4);
    }

    [Theory]
    [InlineData(Perfil.CidadaoVerificado)]
    [InlineData(Perfil.Administrador)]
    public async Task Ja_verificado_devolve_422(Perfil perfil)
    {
        var pessoa = await fabrica.NovaPessoaAsync(perfil);

        var resposta = await SolicitarAsync(pessoa);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TipoErroAsync(resposta)).Should().Be("ja-verificado");
    }

    [Fact]
    public async Task Recusada_permite_nova_solicitacao()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var id = await SolicitacaoIdAsync(pessoa);
        await admin.Cliente.PostAsJsonAsync($"/api/v1/admin/verificacoes/{id}/recusar", new { motivo = "Faltam detalhes." }, ClienteApi.Json);

        var resposta = await SolicitarAsync(pessoa);

        resposta.StatusCode.Should().Be(HttpStatusCode.Created);
    }
}
