using System.Net;
using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Problemas;
using Ocorrencias.Api.Votos;
using Ocorrencias.Api.Tests.Infra;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Tests.Votos;

[Collection(ApiCollection.Nome)]
public sealed class VotosTests(ApiFactory fabrica)
{
    private async Task<(Pessoa Autor, ObterDetalheProblemaResponse Problema, double Lat, double Lng)> ProblemaNovoAsync()
    {
        var autor = await fabrica.NovaPessoaAsync();
        var (lat, lng) = ApiFactory.PontoUnico();
        return (autor, await autor.RegistrarProblemaAsync(lat, lng), lat, lng);
    }

    private static async Task<string?> TipoErroAsync(HttpResponseMessage resposta) =>
        (await resposta.LerJsonAsync()).GetProperty("type").GetString();

    [Theory]
    [InlineData("sim")]
    [InlineData("nao")]
    public async Task Autor_nao_vota_no_proprio_problema(string valor)
    {
        var (autor, problema, _, _) = await ProblemaNovoAsync();

        var resposta = await autor.VotarAsync(problema.Id, valor);

        resposta.StatusCode.Should().Be(HttpStatusCode.UnprocessableEntity);
        (await TipoErroAsync(resposta)).Should().Be("autor-nao-vota");
    }

    [Fact]
    public async Task Nao_em_nova_e_aceito_e_conta_como_votante()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();

        var resposta = await votante.VotarAsync(problema.Id, "nao");
        var detalhe = await resposta.LerAsync<ObterDetalheProblemaResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        detalhe.Status.Should().Be(StatusProblema.Nova);
        detalhe.Avaliacao!.Votantes.Should().Be(1);
        detalhe.Votos.Should().Be(new ObterDetalheProblemaVotos(0, 1, 1));
        detalhe.MeuVoto.Should().Be(ValorVotoContrato.Nao);
    }

    [Fact]
    public async Task Cinco_votantes_tiram_o_problema_de_nova()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        ObterDetalheProblemaResponse? detalhe = null;

        for (var i = 1; i <= 5; i++)
        {
            var votante = await fabrica.NovaPessoaAsync();
            detalhe = await (await votante.VotarAsync(problema.Id, "sim")).LerAsync<ObterDetalheProblemaResponse>();

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
        detalhe.Votos.Should().Be(new ObterDetalheProblemaVotos(5, 0, 0));
        detalhe.MeuVoto.Should().Be(ValorVotoContrato.Sim);
    }

    [Fact]
    public async Task Voto_grava_so_a_distancia_calculada_no_postgis()
    {
        var (_, problema, lat, lng) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();

        await votante.VotarAsync(problema.Id, "sim", Cenarios.Norte(lat, 100), lng);

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

        await votante.VotarAsync(problema.Id, "nao");

        var voto = await fabrica.NoBancoAsync(db => db.Votos.SingleAsync(v => v.ProblemaId == problema.Id));
        voto.DistanciaMetros.Should().BeNull();
        voto.Valor.Should().Be(ValorVoto.Negativo);
    }

    [Fact]
    public async Task Votar_de_novo_substitui_o_voto()
    {
        var (_, problema, lat, lng) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();
        await votante.VotarAsync(problema.Id, "sim", Cenarios.Norte(lat, 100), lng);
        fabrica.Relogio.Advance(TimeSpan.FromMinutes(5));

        var resposta = await votante.VotarAsync(problema.Id, "nao");
        var detalhe = await resposta.LerAsync<ObterDetalheProblemaResponse>();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        detalhe.Votos.Should().Be(new ObterDetalheProblemaVotos(0, 1, 1));
        detalhe.MeuVoto.Should().Be(ValorVotoContrato.Nao);

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

        await votante.VotarAsync(problema.Id, "sim");

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

        var resposta = await votante.VotarAsync(problema.Id, "sim");

        (await TipoErroAsync(resposta)).Should().Be("problema-resolvido");
    }

    [Fact]
    public async Task Quatro_nao_perto_levam_a_possivelmente_resolvida()
    {
        var (lat, lng) = ApiFactory.PontoUnico();
        var problema = await fabrica.ProblemaAtivoAsync(lat, lng);
        ObterDetalheProblemaResponse? detalhe = null;

        // Os 5 "sim" sem GPS de ProblemaAtivoAsync também contam no ciclo de vida (R⁺ = 5).
        // 4 "não" a ~100 m: R⁻ = 4 × 2 = 8 ≥ 5 e p = 8 / 13 ≈ 0,62 ≥ 0,6.
        for (var i = 0; i < 4; i++)
        {
            var votante = await fabrica.NovaPessoaAsync();
            detalhe = await (await votante.VotarAsync(problema.Id, "nao", Cenarios.Norte(lat, 100), lng))
                .LerAsync<ObterDetalheProblemaResponse>();
        }

        detalhe!.Status.Should().Be(StatusProblema.PossivelmenteResolvida);
        detalhe.Votos.Should().Be(new ObterDetalheProblemaVotos(5, 4, 4));
    }

    [Fact]
    public async Task Problema_inexistente_devolve_404()
    {
        var votante = await fabrica.NovaPessoaAsync();

        var resposta = await votante.VotarAsync(Guid.NewGuid(), "sim");

        resposta.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("talvez", -22.9, -43.1)]
    [InlineData("confirma", -22.9, -43.1)]
    [InlineData("sim", -22.9, null)]
    [InlineData("sim", -95.0, -43.1)]
    public async Task Voto_invalido_devolve_400(string valor, double? lat, double? lng)
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var votante = await fabrica.NovaPessoaAsync();

        var resposta = await votante.VotarAsync(problema.Id, valor, lat, lng);

        resposta.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Votos_simultaneos_no_mesmo_problema_nao_perdem_recalculo()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var votantes = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => fabrica.NovaPessoaAsync()));

        var respostas = await Task.WhenAll(votantes.Select(v => v.VotarAsync(problema.Id, "sim")));

        respostas.Should().AllSatisfy(r => r.StatusCode.Should().Be(HttpStatusCode.OK));
        var detalhe = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{problema.Id}"))
            .LerAsync<ObterDetalheProblemaResponse>();
        detalhe.Status.Should().Be(StatusProblema.Ativa);
        detalhe.Confianca.Should().Be(62.5);
    }

    [Fact]
    public async Task Nao_ultimas_24h_inclui_exatamente_24h_e_descarta_o_mais_antigo()
    {
        var (_, problema, _, _) = await ProblemaNovoAsync();
        var primeiro = await fabrica.NovaPessoaAsync();
        var segundo = await fabrica.NovaPessoaAsync();
        await primeiro.VotarAsync(problema.Id, "nao");
        fabrica.Relogio.Advance(TimeSpan.FromHours(24));
        await segundo.VotarAsync(problema.Id, "nao");

        var noLimite = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{problema.Id}"))
            .LerAsync<ObterDetalheProblemaResponse>();
        fabrica.Relogio.Advance(TimeSpan.FromMinutes(1));
        var depois = await (await fabrica.CreateClient().GetAsync($"/api/v1/problemas/{problema.Id}"))
            .LerAsync<ObterDetalheProblemaResponse>();

        noLimite.Votos.Should().Be(new ObterDetalheProblemaVotos(0, 2, 2));
        depois.Votos.Should().Be(new ObterDetalheProblemaVotos(0, 2, 1));
    }
}
