using Ocorrencias.Simulator.Experimento;
using Ocorrencias.Simulator.Modelo;

namespace Ocorrencias.Simulator.Tests;

public sealed class OpcoesTests
{
    [Fact]
    public void Padroes_do_plano()
    {
        var o = Opcoes.Ler([]);

        o.Configuracoes.Select(c => c.Name).Should().Equal("A", "B", "B-v3", "B-v5");
        o.Variantes.Should().Equal(Variante.S1, Variante.S2, Variante.S3);
        o.Sementes.Should().Equal(Enumerable.Range(1, 30));
        o.Saida.Should().Be("resultados/");
        o.Cenario.Should().Be(new Cenario());
    }

    [Fact]
    public void Le_a_linha_de_comando_do_plano()
    {
        var o = Opcoes.Ler(["--config", "A,B-v3", "--variantes", "S2", "--sementes", "5", "--saida", "saida/"]);

        o.Configuracoes.Select(c => c.Name).Should().Equal("A", "B-v3");
        o.Variantes.Should().Equal(Variante.S2);
        o.Sementes.Should().Equal(1, 2, 3, 4, 5);
        o.Saida.Should().Be("saida/");
    }

    [Theory]
    [InlineData("--config", "C")]
    [InlineData("--variantes", "S9")]
    [InlineData("--variantes", "7")]
    [InlineData("--sementes", "0")]
    [InlineData("--sementes", "x")]
    [InlineData("--outra", "1")]
    public void Rejeita_argumentos_invalidos(string opcao, string valor)
    {
        var acao = () => Opcoes.Ler([opcao, valor]);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Cenario_base_do_repositorio_e_igual_aos_padroes_do_codigo()
    {
        var caminho = Path.Combine(RaizDoRepositorio(), "cenarios", "base.json");

        Opcoes.Ler(["--cenario", caminho]).Cenario.Should().Be(new Cenario());
    }

    [Fact]
    public void Cenario_com_campo_desconhecido_e_erro()
    {
        var arquivo = Path.GetTempFileName();
        File.WriteAllText(arquivo, """{ "usuarioz": 10 }""");
        try
        {
            var acao = () => Opcoes.Ler(["--cenario", arquivo]);

            acao.Should().Throw<System.Text.Json.JsonException>();
        }
        finally
        {
            File.Delete(arquivo);
        }
    }

    public static TheoryData<Cenario> CenariosInvalidos =>
    [
        new Cenario { DiasCriacao = 40 },
        new Cenario { InicioAtaqueAposCriacaoHoras = -100, DuracaoAtaqueHoras = 1 },
        new Cenario { DuracaoAtaqueHoras = 0 },
        new Cenario { DiaAtaque = -1 },
    ];

    [Theory]
    [MemberData(nameof(CenariosInvalidos))]
    public void Cenario_invalido_e_recusado(Cenario cenario) =>
        ((Action)cenario.Validar).Should().Throw<ArgumentException>();

    [Fact]
    public void Sem_falsos_o_fp_medio_e_vazio_e_nao_zero()
    {
        var cenario = CenarioPequeno.Valor with { FracaoVerdadeiros = 1 };

        var resultado = Executor.Executar(cenario, [UrbanReports.Api.Trust.Engine.TrustConfiguration.B], [Variante.S1], [1, 2]);

        Resumo.PorConfiguracao(resultado).Single().FpMedio.Should().BeNull();
    }

    [Fact]
    public void Media_ignora_ausentes()
    {
        Resumo.Media([null, 0.2, 0.4]).Should().BeApproximately(0.3, 1e-12);
        Resumo.Media([null, null]).Should().BeNull();
    }

    [Fact]
    public void K_minimo_e_o_menor_k_com_taxa_de_pelo_menos_50_porcento()
    {
        List<ResumoAtaque> ataques =
        [
            new("B", "S1", 1, 0.0, 0, 0), new("B", "S1", 2, 0.5, 0, 0), new("B", "S1", 3, 0.9, 0, 0),
            new("B", "S2", 1, 0.1, 0, 0), new("B", "S2", 2, 0.4, 0, 0),
        ];

        Resumo.KMinimos(ataques).Should().Equal(new KMinimo("B", "S1", 2), new KMinimo("B", "S2", null));
    }

    [Theory]
    [InlineData(new[] { 3.0, 1.0, 2.0 }, 2.0)]
    [InlineData(new[] { 4.0, 1.0, 2.0, 3.0 }, 2.5)]
    public void Mediana(double[] valores, double esperado) =>
        Resumo.Mediana(valores).Should().Be(esperado);

    private static string RaizDoRepositorio()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "UrbanReports.slnx")))
        {
            dir = dir.Parent;
        }

        return dir?.FullName ?? throw new InvalidOperationException("Raiz do repositório não encontrada.");
    }
}
