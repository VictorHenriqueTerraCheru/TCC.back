using System.ComponentModel.DataAnnotations.Schema;

namespace UrbanReports.Api.Usuarios;

public sealed class LinhaHistorico
{
    [Column("chave")] public Guid Chave { get; init; }
    [Column("tipo")] public int Tipo { get; init; }
    [Column("ocorrencia_id")] public Guid? OcorrenciaId { get; init; }
    [Column("categoria_id")] public short CategoriaId { get; init; }
    [Column("categoria_nome")] public string CategoriaNome { get; init; } = "";
    [Column("grupo")] public string Grupo { get; init; } = "";
    [Column("icone")] public string Icone { get; init; } = "";
    [Column("endereco")] public string? Endereco { get; init; }
    [Column("valor")] public short? Valor { get; init; }
    [Column("status_problema")] public short? StatusProblema { get; init; }
    [Column("data")] public DateTimeOffset Data { get; init; }
}
