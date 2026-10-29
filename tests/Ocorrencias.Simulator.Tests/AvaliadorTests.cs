using UrbanReports.Api.Common;
using UrbanReports.Api.Trust.Engine;
using Ocorrencias.Simulator.Modelo;

namespace Ocorrencias.Simulator.Tests;

public sealed class AvaliadorTests
{
    private static readonly TrustConfiguration B = TrustConfiguration.B;
    private static readonly Guid Autor = Mundo.IdUsuario(9_000);
    private static readonly ProblemaSimulado Problema = new(0, true, new Ponto(0, 0), CriadoHora: 10, ResolucaoRealHora: null, Autor);

    private static Intencao Voto(double hora, int usuario, VoteValue valor, double? distancia = null, Role perfil = Role.Citizen) =>
        new(hora, Mundo.IdUsuario(usuario), valor, perfil, distancia);

    private static Intencao Sim(double hora, int usuario, double? distancia = null) => Voto(hora, usuario, VoteValue.Positive, distancia);

    private static Intencao Nao(double hora, int usuario, double? distancia = null) => Voto(hora, usuario, VoteValue.Negative, distancia);

    [Fact]
    public void Antes_de_existir_o_status_e_zero()
    {
        var linha = Avaliador.Avaliar(Problema, [], B, horas: 24);

        linha.Status[8].Should().Be((IssueStatus)0);
        linha.Status[9].Should().Be(IssueStatus.New); // fim da hora 9 = instante 10 (criação)
    }

    [Fact]
    public void Cinco_votantes_tiram_de_nova_e_o_indice_e_o_do_motor()
    {
        var intencoes = Enumerable.Range(1, 5).Select(u => Sim(10.5, u, distancia: 100)).ToList();

        var linha = Avaliador.Avaliar(Problema, intencoes, B, horas: 24);

        linha.Status[10].Should().Be(IssueStatus.Active);
        // 5 cidadãos a 100 m (peso 2), meia hora antes do fim da hora 10.
        var esperado = TrustEngine.CalculateTrust(
            intencoes.Select(i => new VoteFact(i.UsuarioId, i.Valor, i.Perfil, 100, Avaliador.Instante(10.5))).ToList(),
            Avaliador.Instante(11), B).Score;
        linha.Indice[10].Should().Be(Avaliador.ComoGravadoNaApi(esperado));
    }

    [Fact]
    public void Nao_dado_em_nova_tira_o_falso_do_mapa()
    {
        var intencoes = Enumerable.Range(1, 5).Select(u => Nao(10.5, u, 100)).ToList();

        var linha = Avaliador.Avaliar(Problema, intencoes, B, horas: 100);

        // O 5º voto tira de Nova; o job do fim da hora 10 (instante 11) já vê R⁻ ≥ 5 e p = 1.
        linha.Status[10].Should().Be(IssueStatus.PossiblyResolved);
        linha.Status[57].Should().Be(IssueStatus.PossiblyResolved);
        linha.Status[58].Should().Be(IssueStatus.Resolved, "48 h depois do instante 11");
    }

    [Fact]
    public void Autor_nao_vota()
    {
        var intencoes = new List<Intencao> { new(10.5, Autor, VoteValue.Positive, Role.Citizen, 10) };

        var linha = Avaliador.Avaliar(Problema, intencoes, B, horas: 24);

        linha.Indice[10].Should().Be(0);
    }

    [Fact]
    public void Votar_de_novo_substitui_o_voto()
    {
        var intencoes = new List<Intencao>
        {
            Sim(10.2, 1),
            Nao(10.8, 1),
        };

        var linha = Avaliador.Avaliar(Problema, intencoes, B, horas: 24);

        linha.Indice[10].Should().Be(0, "o voto final é \"não\"");
    }

    [Fact]
    public void Resolvida_nao_recebe_mais_votos()
    {
        var votos = new List<Intencao>();
        votos.AddRange(Enumerable.Range(1, 5).Select(u => Sim(10.5, u, 100)));
        // R⁺ = 10, R⁻ = 16 → p ≈ 0,615 ≥ 0,6.
        votos.AddRange(Enumerable.Range(6, 8).Select(u => Nao(11.5, u, 100)));
        votos.AddRange(Enumerable.Range(20, 10).Select(u => Sim(70.5, u, 100))); // depois de Resolvida (≥ 59,5 h)

        var linha = Avaliador.Avaliar(Problema, votos, B, horas: 80);

        linha.Status[11].Should().Be(IssueStatus.PossiblyResolved);
        linha.Status[60].Should().Be(IssueStatus.Resolved);
        linha.Indice[71].Should().BeLessThan(linha.Indice[69], "os 10 \"sim\" tardios foram recusados e o índice só decai");
    }

    [Fact]
    public void Faixa_exibida_so_fora_de_nova()
    {
        var umVerificado = new List<Intencao> { Voto(10.5, 1, VoteValue.Positive, 10, Role.VerifiedCitizen) };

        var linha = Avaliador.Avaliar(Problema, umVerificado, B, horas: 90);

        TrustEngine.ClassifyBand(linha.Indice[10], B).Should().Be(TrustBand.High);
        linha.FaixaExibida(10, B).Should().BeNull("ainda está em Nova");
        linha.FaixaExibida(82, B).Should().Be(TrustBand.High, "após 72 h sai de Nova");
    }

    [Theory]
    [InlineData(69.995, TrustBand.High)]
    [InlineData(69.994, TrustBand.Medium)]
    [InlineData(39.995, TrustBand.Medium)]
    [InlineData(39.994, TrustBand.Low)]
    public void Faixa_e_classificada_sobre_o_indice_arredondado_como_na_api(double bruto, TrustBand esperada) =>
        TrustEngine.ClassifyBand(Avaliador.ComoGravadoNaApi(bruto), B).Should().Be(esperada);
}
