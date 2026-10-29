using Microsoft.AspNetCore.Identity;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Persistence;

public sealed class Usuario : IdentityUser<Guid>
{
    public required string Nome { get; set; }
    public Perfil Perfil { get; set; } = Perfil.Cidadao;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? VerificadoEm { get; set; }
}
