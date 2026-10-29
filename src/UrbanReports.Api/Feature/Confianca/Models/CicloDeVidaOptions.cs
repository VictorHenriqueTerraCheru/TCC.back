namespace UrbanReports.Api.Confianca;

public sealed class CicloDeVidaOptions
{
    public const string Secao = "CicloDeVida";

    public bool Habilitado { get; set; } = true;

    public TimeSpan Intervalo { get; set; } = TimeSpan.FromMinutes(15);
}
