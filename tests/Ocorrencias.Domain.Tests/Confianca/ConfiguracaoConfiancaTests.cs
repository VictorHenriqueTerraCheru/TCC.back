using System.Globalization;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Domain.Tests.Confianca;

public sealed class ConfiguracaoConfiancaTests
{
    [Theory]
    [InlineData("A", "A", 1)]
    [InlineData("B", "B", 10)]
    [InlineData(" b ", "B", 10)]
    [InlineData("B-v3", "B-v3", 3)]
    [InlineData("B-v5", "B-v5", 5)]
    [InlineData("A-v2.5", "A-v2.5", 2.5)]
    public void PorNome_resolve_as_configuracoes(string nome, string esperado, double pesoVerificado)
    {
        var cfg = ConfiguracaoConfianca.PorNome(nome);

        cfg.Nome.Should().Be(esperado);
        cfg.PesoVerificado.Should().Be(pesoVerificado);
    }

    [Fact]
    public void PorNome_B_e_a_propria_instancia_B() =>
        ConfiguracaoConfianca.PorNome("B").Should().BeSameAs(ConfiguracaoConfianca.B);

    [Theory]
    [InlineData("C")]
    [InlineData("B-v")]
    [InlineData("B-vx")]
    [InlineData("B-v0")]
    [InlineData("B-vInfinity")]
    [InlineData("B-v1e400")]
    [InlineData("B-vNaN")]
    public void PorNome_rejeita_nomes_invalidos(string nome)
    {
        var acao = () => ConfiguracaoConfianca.PorNome(nome);

        acao.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void Nome_da_variante_independe_da_cultura()
    {
        var anterior = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo("pt-BR");
        try
        {
            ConfiguracaoConfianca.B.ComPesoVerificado(2.5).Nome.Should().Be("B-v2.5");
        }
        finally
        {
            CultureInfo.CurrentCulture = anterior;
        }
    }
}
