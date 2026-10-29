using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Common;

public sealed class RelogioPostgresTests
{
    [Fact]
    public void Trunca_ticks_abaixo_de_um_microssegundo()
    {
        var instante = new DateTimeOffset(2026, 10, 16, 3, 36, 51, TimeSpan.Zero).AddTicks(4_987_082);

        RelogioPostgres.Truncar(instante).Should().Be(instante.AddTicks(-2));
    }

    [Fact]
    public void Relogio_do_sistema_sai_alinhado_em_microssegundos() =>
        (new RelogioPostgres().GetUtcNow().Ticks % 10).Should().Be(0);
}
