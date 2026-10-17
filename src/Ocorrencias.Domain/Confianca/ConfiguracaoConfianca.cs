using System.Globalization;

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

    public ConfiguracaoConfianca ComPesoVerificado(double p) =>
        this with { Nome = string.Create(CultureInfo.InvariantCulture, $"{Nome}-v{p}"), PesoVerificado = p };

    /// <summary>Resolve <c>"A"</c>, <c>"B"</c> ou variantes de peso do verificado como <c>"B-v3"</c>.</summary>
    /// <exception cref="ArgumentException">Nome desconhecido.</exception>
    public static ConfiguracaoConfianca PorNome(string nome)
    {
        var partes = nome.Trim().Split("-v", 2);
        var baseCfg = partes[0].ToUpperInvariant() switch
        {
            "A" => A,
            "B" => B,
            _ => throw new ArgumentException($"Configuração de confiança desconhecida: '{nome}'.", nameof(nome)),
        };

        if (partes.Length == 1)
        {
            return baseCfg;
        }

        return double.TryParse(partes[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var peso) && double.IsFinite(peso) && peso > 0
            ? baseCfg.ComPesoVerificado(peso)
            : throw new ArgumentException($"Peso de verificado inválido em '{nome}'.", nameof(nome));
    }
}
