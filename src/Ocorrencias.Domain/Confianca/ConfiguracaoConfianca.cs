namespace Ocorrencias.Domain.Confianca;

/// <summary>Parâmetros do motor (docs/MOTOR_CONFIANCA.md). A API usa <see cref="B"/>.</summary>
public sealed record ConfiguracaoConfianca
{
    public required string Nome { get; init; }

    // perfil
    public double PesoCidadao { get; init; } = 1;
    public double PesoVerificado { get; init; } = 10;
    public double PesoAdministrador { get; init; } = 1;

    // proximidade
    public bool UsarProximidade { get; init; } = true;
    public double RaioBonusTotalMetros { get; init; } = 200;
    public double RaioBonusZeroMetros { get; init; } = 2000;
    public double BonusMaximo { get; init; } = 2;

    // tempo
    public bool UsarDecaimento { get; init; } = true;
    public TimeSpan MeiaVidaExistencia { get; init; } = TimeSpan.FromDays(14);
    public TimeSpan MeiaVidaPersistencia { get; init; } = TimeSpan.FromDays(3);

    // índice
    public double K { get; init; } = 3;
    public double LimiarMedia { get; init; } = 40;
    public double LimiarAlta { get; init; } = 70;

    // ciclo de vida
    public int VotantesParaSairDeNova { get; init; } = 5;
    public TimeSpan TempoMaximoEmNova { get; init; } = TimeSpan.FromHours(72);
    public double PesoMinimoResolvido { get; init; } = 5;
    public double ProporcaoResolvido { get; init; } = 0.6;
    public TimeSpan TempoConfirmacaoResolvida { get; init; } = TimeSpan.FromHours(48);

    public static ConfiguracaoConfianca B { get; } = new() { Nome = "B" };

    public static ConfiguracaoConfianca A { get; } = new()
    {
        Nome = "A",
        PesoVerificado = 1,
        UsarProximidade = false,
        UsarDecaimento = false,
    };

    public ConfiguracaoConfianca ComPesoVerificado(double p) => this with { Nome = $"{Nome}-v{p}", PesoVerificado = p };
}
