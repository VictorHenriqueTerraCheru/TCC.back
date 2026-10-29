using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class Voto
{
    public Guid Id { get; set; }
    public Guid ProblemaId { get; set; }
    public Problema Problema { get; set; } = null!;
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public ValorVoto Valor { get; set; }
    public Perfil PerfilNoMomento { get; set; }
    public int? DistanciaMetros { get; set; }
    public DateTimeOffset RegistradoEm { get; set; }
}
