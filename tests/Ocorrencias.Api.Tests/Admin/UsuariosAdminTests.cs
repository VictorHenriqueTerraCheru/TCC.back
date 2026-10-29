using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Tests.Admin;

[Collection(ApiCollection.Nome)]
public sealed class UsuariosAdminTests(ApiFactory fabrica)
{
    private static Task<HttpResponseMessage> AlterarPerfilAsync(Pessoa admin, Guid id, string perfil) =>
        admin.Cliente.PatchAsJsonAsync($"/api/v1/admin/usuarios/{id}/perfil", new { perfil }, ClienteApi.Json);

    private async Task<string> EmailDoUsuarioAsync(Guid id) =>
        (await fabrica.NoBancoAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == id))).Email!;

    [Fact]
    public async Task Rotas_de_usuarios_exigem_perfil_administrador()
    {
        var cidadao = await fabrica.NovaPessoaAsync();
        var verificado = await fabrica.NovaPessoaAsync(Perfil.CidadaoVerificado);

        (await cidadao.Cliente.GetAsync("/api/v1/admin/usuarios")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await AlterarPerfilAsync(verificado, Guid.NewGuid(), "cidadao")).StatusCode.Should().Be(HttpStatusCode.Forbidden);
        (await fabrica.CreateClient().GetAsync("/api/v1/admin/usuarios")).StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Listar_devolve_os_dados_do_usuario_sem_expor_senha()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var pessoa = await fabrica.NovaPessoaAsync();
        var email = await EmailDoUsuarioAsync(pessoa.Id);

        var resposta = await admin.Cliente.GetAsync($"/api/v1/admin/usuarios?busca={email}");
        var json = await resposta.LerJsonAsync();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        json.GetProperty("total").GetInt32().Should().Be(1);
        var item = json.GetProperty("itens")[0];
        item.GetProperty("id").GetGuid().Should().Be(pessoa.Id);
        item.GetProperty("nome").GetString().Should().Be("Pessoa Teste");
        item.GetProperty("email").GetString().Should().Be(email);
        item.GetProperty("perfil").GetString().Should().Be("cidadao");
        item.GetProperty("criadoEm").GetDateTimeOffset().Should().Be(fabrica.Relogio.GetUtcNow());
        item.GetProperty("verificadoEm").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
        item.EnumerateObject().Select(p => p.Name).Should().BeEquivalentTo("id", "nome", "email", "perfil", "criadoEm", "verificadoEm");
    }

    [Fact]
    public async Task Listar_busca_por_trecho_do_email_sem_diferenciar_caixa()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var pessoa = await fabrica.NovaPessoaAsync();
        var trecho = (await EmailDoUsuarioAsync(pessoa.Id))[..12].ToUpperInvariant();

        var json = await (await admin.Cliente.GetAsync($"/api/v1/admin/usuarios?busca={trecho}")).LerJsonAsync();

        json.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).Should().Contain(pessoa.Id);
    }

    [Fact]
    public async Task Listar_filtra_por_perfil()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var verificado = await fabrica.NovaPessoaAsync(Perfil.CidadaoVerificado);
        var cidadao = await fabrica.NovaPessoaAsync();

        var r = await admin.Cliente.GetAsync("/api/v1/admin/usuarios?perfil=cidadaoVerificado&tamanho=100");
        var json = await r.LerJsonAsync();
        var ids = json.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetGuid()).ToList();
        var perfis = json.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("perfil").GetString()).Distinct();

        ids.Should().Contain(verificado.Id).And.NotContain(cidadao.Id);
        perfis.Should().BeEquivalentTo("cidadaoVerificado");
    }

    [Fact]
    public async Task Listar_pagina_e_valida_parametros()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);

        var json = await (await admin.Cliente.GetAsync("/api/v1/admin/usuarios?tamanho=1&pagina=1")).LerJsonAsync();

        json.GetProperty("itens").GetArrayLength().Should().Be(1);
        json.GetProperty("tamanho").GetInt32().Should().Be(1);
        (await admin.Cliente.GetAsync("/api/v1/admin/usuarios?tamanho=0")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await admin.Cliente.GetAsync("/api/v1/admin/usuarios?perfil=rei")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Alterar_para_verificado_grava_verificado_em_e_voltar_zera()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var pessoa = await fabrica.NovaPessoaAsync();

        (await AlterarPerfilAsync(admin, pessoa.Id, "cidadaoVerificado")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var verificado = await fabrica.NoBancoAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == pessoa.Id));

        (await AlterarPerfilAsync(admin, pessoa.Id, "cidadao")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var rebaixado = await fabrica.NoBancoAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == pessoa.Id));

        verificado.Perfil.Should().Be(Perfil.CidadaoVerificado);
        verificado.VerificadoEm.Should().Be(fabrica.Relogio.GetUtcNow());
        rebaixado.Perfil.Should().Be(Perfil.Cidadao);
        rebaixado.VerificadoEm.Should().BeNull();
    }

    [Fact]
    public async Task Alterar_para_administrador_nao_mexe_em_verificado_em()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var pessoa = await fabrica.NovaPessoaAsync(Perfil.CidadaoVerificado);
        await fabrica.NoBancoAsync(db => db.Users.Where(u => u.Id == pessoa.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.VerificadoEm, fabrica.Relogio.GetUtcNow())));

        (await AlterarPerfilAsync(admin, pessoa.Id, "administrador")).StatusCode.Should().Be(HttpStatusCode.NoContent);
        var usuario = await fabrica.NoBancoAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == pessoa.Id));

        usuario.Perfil.Should().Be(Perfil.Administrador);
        usuario.VerificadoEm.Should().Be(fabrica.Relogio.GetUtcNow());
    }

    [Fact]
    public async Task Admin_nao_altera_o_proprio_perfil()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);

        var resposta = await AlterarPerfilAsync(admin, admin.Id, "cidadao");
        var json = await resposta.LerJsonAsync();
        var usuario = await fabrica.NoBancoAsync(db => db.Users.AsNoTracking().SingleAsync(u => u.Id == admin.Id));

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        json.GetProperty("type").GetString().Should().Be("proprio-perfil");
        usuario.Perfil.Should().Be(Perfil.Administrador);
    }

    [Fact]
    public async Task Alterar_perfil_de_usuario_inexistente_devolve_404_e_perfil_invalido_400()
    {
        var admin = await fabrica.NovaPessoaAsync(Perfil.Administrador);
        var pessoa = await fabrica.NovaPessoaAsync();

        (await AlterarPerfilAsync(admin, Guid.NewGuid(), "cidadao")).StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await AlterarPerfilAsync(admin, pessoa.Id, "rei")).StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}
