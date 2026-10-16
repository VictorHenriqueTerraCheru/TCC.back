using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Features.Problemas;

public static class ExibicaoConfianca
{
    /// <summary>
    /// Confiança arredondada em 1 casa (só na exibição) e a faixa correspondente. Em <c>nova</c> não é exibida:
    /// ambos vêm nulos, embora o valor seja calculado e gravado.
    /// </summary>
    public static (double? Confianca, FaixaConfianca? Faixa) Calcular(StatusProblema status, decimal? confianca, ConfiguracaoConfianca cfg)
    {
        if (status == StatusProblema.Nova || confianca is not { } valor)
        {
            return (null, null);
        }

        var indice = (double)valor;
        return (Math.Round(indice, 1, MidpointRounding.AwayFromZero), MotorConfianca.ClassificarFaixa(indice, cfg));
    }
}
