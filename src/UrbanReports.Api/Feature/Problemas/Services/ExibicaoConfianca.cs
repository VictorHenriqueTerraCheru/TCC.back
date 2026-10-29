using UrbanReports.Api.Common;
using UrbanReports.Api.Confianca.Motor;

namespace UrbanReports.Api.Problemas;

public static class ExibicaoConfianca
{
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
