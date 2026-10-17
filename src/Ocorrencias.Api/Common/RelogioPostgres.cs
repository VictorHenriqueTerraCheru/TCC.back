namespace Ocorrencias.Api.Common;

/// <summary>
/// Relógio do sistema truncado em microssegundos, a precisão do <c>timestamptz</c> do PostgreSQL.
/// Sem isso, no Linux (ticks de 100 ns) o instante devolvido logo após um INSERT difere do que é lido
/// depois do banco.
/// </summary>
public sealed class RelogioPostgres : TimeProvider
{
    public static DateTimeOffset Truncar(DateTimeOffset instante) =>
        new(instante.Ticks - instante.Ticks % TicksPorMicrossegundo, instante.Offset);

    private const long TicksPorMicrossegundo = TimeSpan.TicksPerMillisecond / 1000;

    public override DateTimeOffset GetUtcNow() => Truncar(System.GetUtcNow());
}
