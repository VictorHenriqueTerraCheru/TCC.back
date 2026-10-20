using System.Globalization;
using Ocorrencias.Api.Problemas;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Tests.Problemas;

public sealed class ExibicaoConfiancaTests
{
    [Theory]
    [InlineData("0", 0.0, FaixaConfianca.Baixa)]
    [InlineData("39.9", 39.9, FaixaConfianca.Baixa)]
    [InlineData("40", 40.0, FaixaConfianca.Media)]
    [InlineData("69.9", 69.9, FaixaConfianca.Media)]
    [InlineData("70", 70.0, FaixaConfianca.Alta)]
    [InlineData("100", 100.0, FaixaConfianca.Alta)]
    public void Faixa_muda_exatamente_em_40_e_70(string gravado, double exibido, FaixaConfianca faixa)
    {
        var (confianca, faixaCalculada) = ExibicaoConfianca.Calcular(
            StatusProblema.Ativa, decimal.Parse(gravado, CultureInfo.InvariantCulture), ConfiguracaoConfianca.B);

        confianca.Should().Be(exibido);
        faixaCalculada.Should().Be(faixa);
    }

    [Theory]
    [InlineData("45.46", 45.5)]
    [InlineData("45.44", 45.4)]
    [InlineData("62.50", 62.5)]
    public void Exibe_com_uma_casa_decimal(string gravado, double esperado)
    {
        var (confianca, _) = ExibicaoConfianca.Calcular(
            StatusProblema.Ativa, decimal.Parse(gravado, CultureInfo.InvariantCulture), ConfiguracaoConfianca.B);

        confianca.Should().Be(esperado);
    }

    [Fact]
    public void Nova_nao_exibe_confianca_mesmo_com_valor_gravado()
    {
        var (confianca, faixa) = ExibicaoConfianca.Calcular(StatusProblema.Nova, 62.5m, ConfiguracaoConfianca.B);

        confianca.Should().BeNull();
        faixa.Should().BeNull();
    }

    [Theory]
    [InlineData(StatusProblema.Ativa)]
    [InlineData(StatusProblema.PossivelmenteResolvida)]
    public void Sem_confianca_gravada_nao_exibe_nada(StatusProblema status)
    {
        var (confianca, faixa) = ExibicaoConfianca.Calcular(status, null, ConfiguracaoConfianca.B);

        confianca.Should().BeNull();
        faixa.Should().BeNull();
    }
}
