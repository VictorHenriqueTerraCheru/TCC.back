using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class SolicitacaoVerificacao
{
    public Guid Id { get; set; }
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public TipoVinculo TipoVinculo { get; set; }
    public required string Justificativa { get; set; }
    public StatusVerificacao Status { get; set; } = StatusVerificacao.Pendente;
    public string? MotivoRecusa { get; set; }
    public Guid? AnalisadaPorId { get; set; }
    public Usuario? AnalisadaPor { get; set; }
    public DateTimeOffset CriadaEm { get; set; }
    public DateTimeOffset? AnalisadaEm { get; set; }
}
