using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Admin;
using Ocorrencias.Api.Usuarios;
using Ocorrencias.Api.Verificacao;
using Ocorrencias.Api.Tests.Infra;

namespace Ocorrencias.Api.Tests.Admin;

[Collection(ApiCollection.Nome)]
public sealed class AdminTests(ApiFactory fabrica)
{
    private static async Task<Guid> SolicitarAsync(Pessoa pessoa)
    {
        var resposta = await pessoa.Cliente.PostAsJsonAsync("/api/v1/verificacoes",
            new { tipoVinculo = "trabalhaRegiao", justificativa = "Trabalho no comércio da região há mais de cinco anos." }, ClienteApi.Json);
        resposta.EnsureSuccessStatusCode();
        return (await resposta.LerAsync<SolicitarVerificacaoResponse>()).Id;
    }

    private static Task<HttpResponseMessage> AprovarAsync(Pessoa admin, Guid id) =>
        admin.Cliente.PostAsync($"/api/v1/admin/verificacoes/{id}/aprovar", null);

    private static Task<HttpResponseMessage> RecusarAsync(Pessoa admin, Guid id, string motivo = "Justificativa insuficiente.") =>
        admin.Cliente.PostAsJsonAsync($"/api/v1/admin/verificacoes/{id}/recusar", new { motivo }, ClienteApi.Json);

    [Fact]
    public async Task Rotas_de_admin_exigem_perfil_administrador()
    {
        var cidadao = await fabrica.NovaPessoaAsync();
        var verificado = await fabrica.NovaPessoaAsync(Perfil.CidadaoVerificado);

        (await cidadao.Cliente.GetAsync("/api/v1/admin/verificacoes")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await verificado.Cliente.PostAsync($"/api/v1/admin/verificacoes/{Guid.NewGuid()}/aprovar", null)).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fabrica.CreateClient().GetAsync("/api/v1/admin/verificacoes")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Aprovar_torna_o_usuario_verificado()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var id = await SolicitarAsync(pessoa);

        var resposta = await AprovarAsync(admin, id);
        var me = await (await pessoa.Cliente.GetAsync("/api/v1/me")).LerAsync<ObterMeResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        me.Perfil.Should().Be(Perfil.CidadaoVerificado);
        me.VerificadoEm.Should().Be(fabrica.Relogio.GetUtcNow());
        me.Verificacao!.Status.Should().Be(StatusVerificacao.Aprovada);

        var solicitacao = await fabrica.NoBancoAsync(db => db.SolicitacoesVerificacao.AsNoTracking().SingleAsync(s => s.Id == id));
        solicitacao.AnalisadaPorId.Should().Be(admin.Id);
        solicitacao.AnalisadaEm.Should().Be(fabrica.Relogio.GetUtcNow());
    }

    [Fact]
    public async Task Recusar_guarda_o_motivo_e_mantem_cidadao()
    {
        var pessoa = await fabrica.NovaPessoaAsync();
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var id = await SolicitarAsync(pessoa);

        var resposta = await RecusarAsync(admin, id, "  Não comprovou vínculo.  ");
        var me = await (await pessoa.Cliente.GetAsync("/api/v1/me")).LerAsync<ObterMeResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.NoContent);
        me.Perfil.Should().Be(Perfil.Cidadao);
        me.Verificacao!.Status.Should().Be(StatusVerificacao.Recusada);
        me.Verificacao.MotivoRecusa.Should().Be("Não comprovou vínculo.");
    }

    [Fact]
    public async Task Recusar_sem_motivo_devolve_400()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var id = await SolicitarAsync(await fabrica.NovaPessoaAsync());

        (await RecusarAsync(admin, id, "")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Analisar_de_novo_devolve_409_e_inexistente_404()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var id = await SolicitarAsync(await fabrica.NovaPessoaAsync());
        await AprovarAsync(admin, id);

        var aprovarDeNovo = await AprovarAsync(admin, id);
        var recusarDepois = await RecusarAsync(admin, id);
        var inexistente = await AprovarAsync(admin, Guid.NewGuid());

        aprovarDeNovo.StatusCode.Should().Be(HttpStatusCode.Conflict);
        (await aprovarDeNovo.LerJsonAsync()).GetProperty("type").GetString().Should().Be("verificacao-ja-analisada");
        recusarDepois.StatusCode.Should().Be(HttpStatusCode.Conflict);
        inexistente.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Aprovar_e_recusar_simultaneos_so_um_vence()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var id = await SolicitarAsync(await fabrica.NovaPessoaAsync());

        var respostas = await Task.WhenAll(AprovarAsync(admin, id), RecusarAsync(admin, id), AprovarAsync(admin, id));

        respostas.Count(r => r.StatusCode == HttpStatusCode.NoContent).Should().Be(1);
        respostas.Count(r => r.StatusCode == HttpStatusCode.Conflict).Should().Be(2);
    }

    [Fact]
    public async Task Listar_filtra_por_status_com_iniciais_e_pendentes_mais_antigas_primeiro()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var a = await fabrica.NovaPessoaAsync();
        await fabrica.NoBancoAsync(db => db.Users.Where(u => u.Id == a.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Nome, "carlos de menezes")));
        var primeira = await SolicitarAsync(a);
        fabrica.Relogio.Advance(TimeSpan.FromMinutes(1));
        var segunda = await SolicitarAsync(await fabrica.NovaPessoaAsync());

        var pagina = await (await admin.Cliente.GetAsync($"/api/v1/admin/verificacoes?status=pendente&tamanho={Paginacao.TamanhoMaximo}"))
            .LerAsync<Paginado<ListarVerificacoesItem>>();
        var minhas = pagina.Itens.Where(i => i.Id == primeira || i.Id == segunda).ToList();

        pagina.Itens.Should().AllSatisfy(i => i.Status.Should().Be(StatusVerificacao.Pendente));
        minhas.Select(i => i.Id).Should().Equal(primeira, segunda);
        minhas[0].Usuario.Should().Be(new ListarVerificacoesUsuarioResumo(a.Id, "carlos de menezes", "CM"));
        minhas[0].TipoVinculo.Should().Be(TipoVinculo.TrabalhaRegiao);
    }

    [Theory]
    [InlineData("Carlos Menezes", "CM")]
    [InlineData("Ana", "A")]
    [InlineData("  maria  da  silva ", "MS")]
    public void Iniciais(string nome, string esperado) =>
        AdminService.Iniciais(nome).Should().Be(esperado);

    /// <summary>Aceite da Etapa 6: a aprovação vale só para os votos SEGUINTES.</summary>
    [Fact]
    public async Task Aprovado_vota_com_peso_10_so_nos_votos_seguintes()
    {
        var votante = await fabrica.NovaPessoaAsync();
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var antes = await autor.RegistrarProblemaAsync(lat, lng);
        var depois = await autor.RegistrarProblemaAsync(Cenarios.Norte(lat, 30), lng, categoriaId: 2);

        await votante.VotarAsync(antes.Id, "sim");
        await AprovarAsync(admin, await SolicitarAsync(votante));
        await votante.VotarAsync(depois.Id, "sim"); // mesmo token antigo (claim "cidadao")

        var votos = await fabrica.NoBancoAsync(db => db.Votos.AsNoTracking()
            .Where(v => v.UsuarioId == votante.Id)
            .ToDictionaryAsync(v => v.ProblemaId, v => v.PerfilNoMomento));
        votos[antes.Id].Should().Be(Perfil.Cidadao);
        votos[depois.Id].Should().Be(Perfil.CidadaoVerificado);

        // Sem GPS: cidadão → C = 100 × 1/4 = 25; verificado → C = 100 × 10/13 ≈ 76,92.
        var confiancas = await fabrica.NoBancoAsync(db => db.Problemas.AsNoTracking()
            .Where(p => p.Id == antes.Id || p.Id == depois.Id)
            .ToDictionaryAsync(p => p.Id, p => p.Confianca));
        confiancas[antes.Id].Should().Be(25.00m);
        confiancas[depois.Id].Should().Be(76.92m);
    }
}
