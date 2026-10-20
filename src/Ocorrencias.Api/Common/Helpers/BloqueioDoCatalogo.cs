using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ocorrencias.Api.Common;

/// <summary>
/// Um lock só para tudo que muda categorias e grupos (criar, editar, apagar). Serializa o <c>max(id)+1</c>,
/// as checagens de nome/rótulo sem distinção de caixa (o índice do banco distingue) e a regra "categoria só em
/// grupo do mesmo tipo", que a FK por chave não garante contra apagar e recriar o grupo com outro tipo.
/// </summary>
public static class BloqueioDoCatalogo
{
    private const long Chave = 7_410_001;

    /// <summary>Exige transação aberta: o lock é liberado no commit/rollback.</summary>
    public static Task TomarAsync(this DatabaseFacade banco, CancellationToken ct) =>
        banco.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({Chave})", ct);
}
