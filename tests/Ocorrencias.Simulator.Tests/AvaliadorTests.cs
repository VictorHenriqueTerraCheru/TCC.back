using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;
using Ocorrencias.Simulator.Modelo;

namespace Ocorrencias.Simulator.Tests;

public sealed class AvaliadorTests
{
    private static readonly ConfiguracaoConfianca B = ConfiguracaoConfianca.B;
    private static readonly Guid Autor = Mundo.IdUsuario(9_000);
    private static readonly ProblemaSimulado Problema = new(0, true, new Ponto(0, 0), CriadoHora: 10, ResolucaoRealHora: null, Autor);

    private static Intencao Voto(double hora, int usuario, TipoVoto tipo, ValorVoto valor, double? distancia = null, Perfil perfil = Perfil.Cidadao) =>
        new(hora, Mundo.IdUsuario(usuario), tipo, valor, perfil, distancia);

    private static Intencao Confirma(double hora, int usuario, double? distancia = null) =>
        Voto(hora, usuario, TipoVoto.Existencia, ValorVoto.Positivo, distancia);

    [Fact]
    public void Antes_de_existir_o_status_e_zero()
    {
        var linha = Avaliador.Avaliar(Problema, [], B, horas: 24);

        linha.Status[8].Should().Be((StatusProblema)0);
        linha.Status[9].Should().Be(StatusProblema.Nova); // fim da hora 9 = instante 10 (criação)
    }

    [Fact]
    public void Cinco_votantes_tiram_de_nova_e_o_indice_e_o_do_motor()
    {
        var intencoes = Enumerable.Range(1, 5).Select(u => Confirma(10.5, u, distancia: 100)).ToList();

        var linha = Avaliador.Avaliar(Problema, intencoes, B, horas: 24);

        linha.Status[10].Should().Be(StatusProblema.Ativa);
        // 5 cidadãos a 100 m (peso 2), meia hora antes do fim da hora 10.
        var esperado = MotorConfianca.CalcularConfianca(
            intencoes.Select(i => new VotoFato(i.UsuarioId, i.Tipo, i.Valor, i.Perfil, 100, Avaliador.Instante(10.5))).ToList(),
            Avaliador.Instante(11), B).Indice;
        linha.Indice[10].Should().BeApproximately(esperado, 1e-9);
    }

    [Fact]
    public void Persistencia_em_nova_e_recusada_como_na_api()
    {
        var intencoes = new List<Intencao>
        {
            Voto(10.5, 1, TipoVoto.Persistencia, ValorVoto.Negativo, 100),
            Voto(10.6, 2, TipoVoto.Persistencia, ValorVoto.Negativo, 100),
            Voto(10.7, 3, TipoVoto.Persistencia, ValorVoto.Negativo, 100),
        };

        var linha = Avaliador.Avaliar(Problema, intencoes, B, horas: 100);

        // Os votos foram recusados: em 72 h vira Ativa, mas não chega a PossivelmenteResolvida.
        linha.Status[81].Should().Be(StatusProblema.Ativa);
        linha.Status.Should().NotContain(StatusProblema.PossivelmenteResolvida);
    }

    [Fact]
    public void Autor_nao_vota_existencia()
    {
        var intencoes = new List<Intencao> { new(10.5, Autor, TipoVoto.Existencia, ValorVoto.Positivo, Perfil.Cidadao, 10) };

        var linha = Avaliador.Avaliar(Problema, intencoes, B, horas: 24);

        linha.Indice[10].Should().Be(0);
    }

    [Fact]
    public void Votar_de_novo_substitui_o_voto()
    {
        var intencoes = new List<Intencao>
        {
            Confirma(10.2, 1),
            Voto(10.8, 1, TipoVoto.Existencia, ValorVoto.Negativo),
        };

        var linha = Avaliador.Avaliar(Problema, intencoes, B, horas: 24);

        linha.Indice[10].Should().Be(0, "o voto final é \"não vi\"");
    }

    [Fact]
    public void Resolvida_nao_recebe_mais_votos()
    {
        var votos = new List<Intencao>();
        votos.AddRange(Enumerable.Range(1, 5).Select(u => Confirma(10.5, u, 100)));
        votos.AddRange(Enumerable.Range(6, 3).Select(u => Voto(11.5, u, TipoVoto.Persistencia, ValorVoto.Negativo, 100)));
        votos.AddRange(Enumerable.Range(20, 10).Select(u => Confirma(70.5, u, 100))); // depois de Resolvida (≥ 59,5 h)

        var linha = Avaliador.Avaliar(Problema, votos, B, horas: 80);

        linha.Status[11].Should().Be(StatusProblema.PossivelmenteResolvida);
        linha.Status[60].Should().Be(StatusProblema.Resolvida);
        linha.Indice[71].Should().BeLessThan(linha.Indice[69], "as 10 confirmações tardias foram recusadas e o índice só decai");
    }

    [Fact]
    public void Faixa_exibida_so_fora_de_nova()
    {
        var umVerificado = new List<Intencao> { Voto(10.5, 1, TipoVoto.Existencia, ValorVoto.Positivo, 10, Perfil.CidadaoVerificado) };

        var linha = Avaliador.Avaliar(Problema, umVerificado, B, horas: 90);

        MotorConfianca.ClassificarFaixa(linha.Indice[10], B).Should().Be(FaixaConfianca.Alta);
        linha.FaixaExibida(10, B).Should().BeNull("ainda está em Nova");
        linha.FaixaExibida(82, B).Should().Be(FaixaConfianca.Alta, "após 72 h sai de Nova");
    }
}
