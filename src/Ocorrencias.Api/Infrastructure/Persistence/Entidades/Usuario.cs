using Microsoft.AspNetCore.Identity;
using Ocorrencias.Api.Common;

namespace Ocorrencias.Api.Persistence;

/// <summary>Usuário do Identity. O perfil é coluna (não usamos roles do Identity).</summary>
public sealed class Usuario : IdentityUser<Guid>
{
    public required string Nome { get; set; }
    public Perfil Perfil { get; set; } = Perfil.Cidadao;
    public DateTimeOffset CriadoEm { get; set; }
    public DateTimeOffset? VerificadoEm { get; set; }
}
