namespace Ocorrencias.Api.Infra.Confianca;

public sealed class CicloDeVidaOptions
{
    public const string Secao = "CicloDeVida";

    /// <summary>Desligado nos testes, que chamam o <see cref="ProcessadorCicloDeVida"/> diretamente.</summary>
    public bool Habilitado { get; set; } = true;

    public TimeSpan Intervalo { get; set; } = TimeSpan.FromMinutes(15);
}
