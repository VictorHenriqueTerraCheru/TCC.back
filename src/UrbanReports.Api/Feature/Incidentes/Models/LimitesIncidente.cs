namespace UrbanReports.Api.Incidentes;

public static class LimitesIncidente
{
    public const int CasasDecimais = 3;
    public const short RaioMetros = 100;
    public const int MaximoPor24h = 10;
    public static readonly TimeSpan Janela = TimeSpan.FromHours(24);

    public const int DiasCalorMaximo = 30;
    public const int MaximoCelulasCalor = 5000;

    public const int RaioAreaMetros = 400;
    public const int LimiarMedia = 5;
    public const int LimiarAlta = 15;
}
