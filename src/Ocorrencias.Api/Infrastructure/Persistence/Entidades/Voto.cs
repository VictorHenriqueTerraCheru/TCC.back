using Ocorrencias.Domain;

namespace Ocorrencias.Api.Persistence;

/// <summary>
/// O voto guarda fatos, não pesos. A posição do votante NUNCA é persistida: só a distância
/// calculada no PostGIS (nula = sem localização).
/// </summary>
public sealed class Voto
{
    public Guid Id { get; set; }
    public Guid ProblemaId { get; set; }
    public Problema Problema { get; set; } = null!;
    public Guid UsuarioId { get; set; }
    public Usuario Usuario { get; set; } = null!;
    public TipoVoto Tipo { get; set; }
    public ValorVoto Valor { get; set; }
    public Perfil PerfilNoMomento { get; set; }
    public int? DistanciaMetros { get; set; }
    public DateTimeOffset RegistradoEm { get; set; }
}
