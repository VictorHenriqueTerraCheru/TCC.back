using Ocorrencias.Domain;
using Ocorrencias.Simulator.Modelo;

namespace Ocorrencias.Simulator.Tests;

public sealed class GeracaoTests
{
    private static readonly Cenario C = CenarioPequeno.Valor;

    [Fact]
    public void Mundo_tem_o_alvo_falso_criado_no_dia_do_ataque()
    {
        var mundo = Mundo.Gerar(C, 7);

        mundo.Problemas.Should().HaveCount(C.Problemas + 1);
        mundo.Alvo.Verdadeiro.Should().BeFalse();
        mundo.Alvo.CriadoHora.Should().Be(C.DiaAtaque * 24);
        mundo.Alvo.AutorId.Should().Be(Mundo.AutorAlvo);
    }

    [Fact]
    public void Intencoes_honestas_respeitam_o_modelo()
    {
        var mundo = Mundo.Gerar(C with { Problemas = 60, Usuarios = 400 }, 3);

        for (var p = 0; p < mundo.Problemas.Count; p++)
        {
            var problema = mundo.Problemas[p];
            var intencoes = mundo.Intencoes[p];

            intencoes.Select(i => i.Hora).Should().BeInAscendingOrder();
            intencoes.Should().OnlyContain(i => i.Hora >= problema.CriadoHora && i.DistanciaMetros <= C.DistanciaMaximaVotoMetros);
            intencoes.Should().NotContain(i => i.UsuarioId == problema.AutorId);
        }

        // Falso e verdadeiro já resolvido recebem "não"; verdadeiro ativo, "sim" (só o ruído de 5% inverte).
        var esperados = mundo.Problemas.SelectMany((problema, p) => mundo.Intencoes[p].Select(i =>
                !problema.Verdadeiro || problema.ResolucaoRealHora is { } r && i.Hora >= r ? ValorVoto.Negativo : ValorVoto.Positivo)
            .Zip(mundo.Intencoes[p], (esperado, i) => esperado == i.Valor))
            .ToList();
        esperados.Should().NotBeEmpty();
        esperados.Count(acertou => acertou).Should().BeGreaterThan((int)(0.9 * esperados.Count));
    }

    [Fact]
    public void Calibracao_da_passagem_mira_2_passagens_por_dia_no_centro()
    {
        var c = new Cenario();

        (c.ProbabilidadePassagemEfetiva * c.ElegiveisMedios * 24).Should().BeApproximately(2, 1e-9);
        (c with { ProbabilidadePassagem = 0.01 }).ProbabilidadePassagemEfetiva.Should().Be(0.01);
    }

    [Theory]
    [InlineData(Variante.S1)]
    [InlineData(Variante.S2)]
    [InlineData(Variante.S3)]
    public void Ataque_por_variante(Variante variante)
    {
        var alvo = Mundo.Gerar(C, 1).Alvo;
        const int k = 6;

        var intencoes = Ataque.Gerar(C, alvo, variante, k, semente: 1);

        intencoes.Should().HaveCount(k);
        intencoes.Should().OnlyContain(i => i.Hora >= Ataque.Inicio(C, alvo) && i.Hora <= Ataque.Fim(C, alvo));
        intencoes.Should().OnlyContain(i => i.Valor == ValorVoto.Positivo);
        intencoes.Select(i => i.UsuarioId).Distinct().Should().HaveCount(k);

        switch (variante)
        {
            case Variante.S1:
                intencoes.Should().OnlyContain(i => i.DistanciaMetros == null && i.Perfil == Perfil.Cidadao);
                break;
            case Variante.S2:
                intencoes.Should().OnlyContain(i => i.DistanciaMetros >= 0 && i.DistanciaMetros <= 50 && i.Perfil == Perfil.Cidadao);
                break;
            case Variante.S3:
                intencoes.Select(i => (i.UsuarioId, i.Perfil)).Distinct().Count(x => x.Perfil == Perfil.CidadaoVerificado).Should().Be(1);
                intencoes.Should().OnlyContain(i => i.DistanciaMetros >= 0 && i.DistanciaMetros <= 50);
                break;
        }
    }

    [Fact]
    public void Binomial_tem_a_media_esperada()
    {
        var rnd = new Random(1);

        var media = Enumerable.Range(0, 200_000).Average(_ => rnd.Binomial(118, 0.0007));

        media.Should().BeApproximately(118 * 0.0007, 0.005);
    }

    [Fact]
    public void Sementes_derivadas_sao_estaveis_e_distintas()
    {
        Aleatorio.Derivar(1, 2, 3).Should().Be(Aleatorio.Derivar(1, 2, 3));
        Aleatorio.Derivar(1, 2, 3).Should().NotBe(Aleatorio.Derivar(1, 3, 2));
        Aleatorio.Derivar(1, 2, 3).Should().NotBe(Aleatorio.Derivar(2, 2, 3));
    }
}
