using System.Net;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Features.Problemas;
using Ocorrencias.Api.Features.Votos;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Tests.Votos;

[Collection(ApiCollection.Nome)]
public sealed class VotosTests(ApiFactory fabrica)
{
    private async Task<(Pessoa Autor, ObterDetalheProblema.Response Problema, double Lat, double Lng)> ProblemaNovoAsync()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        return (autor, await autor.RegistrarProblemaAsync(lat, lng), lat, lng);
    }

    private static async Task<string?> TipoErroAsync(HttpResponseMessage resposta) =>
        (await resposta.LerJsonAsync()).GetProperty("type").GetString();

    [Fact]
    public async Task Autor_nao_vota_existencia_no_proprio_problema()
    {
        var (autor, problema, _, _) = await ProblemaNovoAsync();

        var resposta = await autor.VotarExistenciaAsync(problema.Id, "confirma");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TipoErroAsync(resposta)).Should().Be("autor-nao-vota-existencia");
    }

    [Fact]
    public async Task Persistencia_em_nova_e_recusada()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();

        var resposta = await votante.VotarPersistenciaAsync(problema.Id, "continua");

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TipoErroAsync(resposta)).Should().Be("persistencia-indisponivel-em-nova");
    }

    [Fact]
    public async Task Cinco_votantes_tiram_o_problema_de_nova()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        ObterDetalheProblema.Response? detalhe = null;

        for (var i = 1; i <= 5; i++)
        {
            var votante = await fabrica.NovaPessoaAsync();
            detalhe = await (await votante.VotarExistenciaAsync(problema.Id, "confirma")).LerAsync<ObterDetalheProblema.Response>();

            if (i < 5)
            {
                detalhe.Status.Should().Be(StatusProblema.Nova);
                detalhe.Avaliacao!.Votantes.Should().Be(i);
                detalhe.Confianca.Should().BeNull();
            }
        }

        // 5 cidadãos sem GPS, votos agora: C = 100 × 5 / (5 + 3) = 62,5.
        detalhe!.Status.Should().Be(StatusProblema.Ativa);
        detalhe.Avaliacao.Should().BeNull();
        detalhe.Confianca.Should().Be(62.5);
        detalhe.Faixa.Should().Be(FaixaConfianca.Media);
        detalhe.Existencia.Should().Be(new ObterDetalheProblema.ContagemExistencia(5, 0));
        detalhe.MeusVotos!.Existencia.Should().Be(ValorExistencia.Confirma);
    }

    [Fact]
    public async Task Voto_grava_so_a_distancia_calculada_no_postgis()
    {
        var (_, problema, lat, lng) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();

        await votante.VotarExistenciaAsync(problema.Id, "confirma", Cenarios.Norte(lat, 100), lng);

        var voto = await fabrica.NoBancoAsync(db => db.Votos.SingleAsync(v => v.ProblemaId == problema.Id));
        voto.DistanciaMetros!.Value.Should().BeCloseTo(100, 2);
        voto.PerfilNoMomento.Should().Be(Perfil.Cidadao);
        voto.RegistradoEm.Should().Be(fabrica.Relogio.GetUtcNow());
    }

    [Fact]
    public async Task Voto_sem_localizacao_grava_distancia_nula()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();

        await votante.VotarExistenciaAsync(problema.Id, "naoVi");

        var voto = await fabrica.NoBancoAsync(db => db.Votos.SingleAsync(v => v.ProblemaId == problema.Id));
        voto.DistanciaMetros.Should().BeNull();
        voto.Valor.Should().Be(ValorVoto.Negativo);
    }

    [Fact]
    public async Task Votar_de_novo_substitui_o_voto()
    {
        var (_, problema, lat, lng) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();
        await votante.VotarExistenciaAsync(problema.Id, "confirma", Cenarios.Norte(lat, 100), lng);
        fabrica.Relogio.Advance(TimeSpan.FromMinutes(5));

        var resposta = await votante.VotarExistenciaAsync(problema.Id, "naoVi");
        var detalhe = await resposta.LerAsync<ObterDetalheProblema.Response>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        detalhe.Existencia.Should().Be(new ObterDetalheProblema.ContagemExistencia(0, 1));
        detalhe.MeusVotos!.Existencia.Should().Be(ValorExistencia.NaoVi);

        var votos = await fabrica.NoBancoAsync(db => db.Votos.Where(v => v.ProblemaId == problema.Id).ToListAsync());
        votos.Should().ContainSingle();
        votos[0].DistanciaMetros.Should().BeNull();
        votos[0].RegistradoEm.Should().Be(fabrica.Relogio.GetUtcNow());
    }

    [Fact]
    public async Task Perfil_do_voto_vem_do_banco_e_nao_do_token()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync(); // token emitido como cidadão
        await fabrica.NoBancoAsync(db => db.Users.Where(u => u.Id == votante.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Perfil, Perfil.CidadaoVerificado)));

        await votante.VotarExistenciaAsync(problema.Id, "confirma");

        var voto = await fabrica.NoBancoAsync(db => db.Votos.SingleAsync(v => v.ProblemaId == problema.Id));
        voto.PerfilNoMomento.Should().Be(Perfil.CidadaoVerificado);
    }

    [Fact]
    public async Task Problema_resolvido_nao_recebe_votos()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        await fabrica.NoBancoAsync(db => db.Problemas.Where(p => p.Id == problema.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusProblema.Resolvida)));
        var votante = await fabrica.NovaPessoaAsync();

        var existencia = await votante.VotarExistenciaAsync(problema.Id, "confirma");
        var persistencia = await votante.VotarPersistenciaAsync(problema.Id, "continua");

        (await TipoErroAsync(existencia)).Should().Be("problema-resolvido");
        (await TipoErroAsync(persistencia)).Should().Be("problema-resolvido");
    }

    [Fact]
    public async Task Autor_pode_votar_persistencia()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await autor.RegistrarProblemaAsync(lat, lng);
        await fabrica.NoBancoAsync(db => db.Problemas.Where(p => p.Id == problema.Id)
            .ExecuteUpdateAsync(s => s.SetProperty(p => p.Status, StatusProblema.Ativa)));

        var resposta = await autor.VotarPersistenciaAsync(problema.Id, "resolvido");

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Tres_resolvido_perto_levam_a_possivelmente_resolvida()
    {
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await fabrica.ProblemaAtivoAsync(lat, lng);
        ObterDetalheProblema.Response? detalhe = null;

        // 3 "resolvido" a ~100 m: R⁻ = 3 × 2 = 6 ≥ 5 e p = 1 ≥ 0,6.
        for (var i = 0; i < 3; i++)
        {
            var votante = await fabrica.NovaPessoaAsync();
            detalhe = await (await votante.VotarPersistenciaAsync(problema.Id, "resolvido", Cenarios.Norte(lat, 100), lng))
                .LerAsync<ObterDetalheProblema.Response>();
        }

        detalhe!.Status.Should().Be(StatusProblema.PossivelmenteResolvida);
        detalhe.Persistencia.Should().Be(new ObterDetalheProblema.ContagemPersistencia(0, 3, 3));
    }

    [Fact]
    public async Task Problema_inexistente_devolve_404()
    {
        var votante = await fabrica.NovaPessoaAsync();

        var resposta = await votante.VotarExistenciaAsync(Guid.NewGuid(), "confirma");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("talvez", -22.9, -43.1)]
    [InlineData("confirma", -22.9, null)]
    [InlineData("confirma", -95.0, -43.1)]
    public async Task Voto_invalido_devolve_400(string valor, double? lat, double? lng)
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();

        var resposta = await votante.VotarExistenciaAsync(problema.Id, valor, lat, lng);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Votos_simultaneos_no_mesmo_problema_nao_perdem_recalculo()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var votantes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => fabrica.NovaPessoaAsync()));

        var respostas = await Task.WhenAll(votantes.Select(v => v.VotarExistenciaAsync(problema.Id, "confirma")));

        respostas.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        var detalhe = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{problema.Id}"))
            .LerAsync<ObterDetalheProblema.Response>();
        detalhe.Status.Should().Be(StatusProblema.Ativa);
        detalhe.Confianca.Should().Be(62.5);
    }
}
