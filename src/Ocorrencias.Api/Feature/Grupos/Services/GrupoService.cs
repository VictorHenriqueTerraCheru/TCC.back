using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;

namespace Ocorrencias.Api.Grupos;

public sealed class GrupoService(AppDbContext db) : IGrupoService
{
    /// <summary>
    /// Serializa criação e edição: a chave única e a ordem seguinte saem do estado atual, e a checagem de rótulo
    /// (sem distinguir maiúsculas) não pode correr junto com outra, pois o índice do banco distingue.
    /// </summary>
    private const long ChaveLockCriacao = 7_410_002;

    private const int TamanhoMaximoChave = 30;

    public Task<List<GrupoResponse>> ListarAsync(CancellationToken ct) =>
        db.Grupos
            .OrderBy(g => g.Ordem)
            .ThenBy(g => g.Rotulo)
            .Select(g => new GrupoResponse(g.Chave, g.Rotulo, g.Tipo, g.Cor, g.Ordem))
            .ToListAsync(ct);

    public async Task<Resultado<GrupoResponse>> CriarAsync(CriarGrupoRequest request, CancellationToken ct)
    {
        var rotulo = request.Rotulo.Trim();
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({ChaveLockCriacao})", ct);

        if (await RotuloEmUsoAsync(rotulo, null, ct))
        {
            return ErrosGrupo.RotuloDuplicado;
        }

        var chave = await ChaveUnicaAsync(rotulo, ct);
        var ordem = (await db.Grupos.MaxAsync(g => (int?)g.Ordem, ct) ?? 0) + 1;
        var grupo = new GrupoCategoria { Chave = chave, Rotulo = rotulo, Tipo = request.Tipo, Cor = request.Cor, Ordem = ordem };
        db.Grupos.Add(grupo);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.ViolouUnicidade())
        {
            // Um PUT concorrente (que não toma o lock) deu este rótulo a outro grupo.
            return ErrosGrupo.RotuloDuplicado;
        }

        await transacao.CommitAsync(ct);

        return ParaResposta(grupo);
    }

    public async Task<Resultado<GrupoResponse>> EditarAsync(string chave, EditarGrupoRequest request, CancellationToken ct)
    {
        var rotulo = request.Rotulo.Trim();
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlAsync($"SELECT pg_advisory_xact_lock({ChaveLockCriacao})", ct);

        var grupo = await db.Grupos.FindAsync([chave], ct);
        if (grupo is null)
        {
            return ErroAplicacao.NaoEncontrado;
        }

        if (await RotuloEmUsoAsync(rotulo, chave, ct))
        {
            return ErrosGrupo.RotuloDuplicado;
        }

        grupo.Rotulo = rotulo;
        grupo.Cor = request.Cor;
        grupo.Ordem = request.Ordem;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.ViolouUnicidade())
        {
            return ErrosGrupo.RotuloDuplicado;
        }

        await transacao.CommitAsync(ct);

        return ParaResposta(grupo);
    }

    public async Task<Resultado<Vazio>> ApagarAsync(string chave, CancellationToken ct)
    {
        if (await db.Categorias.AnyAsync(c => c.Grupo == chave, ct))
        {
            return ErrosGrupo.EmUso;
        }

        try
        {
            var linhas = await db.Grupos.Where(g => g.Chave == chave).ExecuteDeleteAsync(ct);
            return linhas == 0 ? ErroAplicacao.NaoEncontrado : new Vazio();
        }
        catch (Exception e) when (e is DbUpdateException || e is PostgresException)
        {
            // ExecuteDelete não passa pelo SaveChanges: a violação sai como PostgresException.
            if (!e.ViolouChaveEstrangeira()) throw;
            // Categoria criada neste grupo entre a checagem e o delete: a FK (Restrict) barrou.
            return ErrosGrupo.EmUso;
        }
    }

    private Task<bool> RotuloEmUsoAsync(string rotulo, string? ignorarChave, CancellationToken ct)
    {
        var minusculo = rotulo.ToLower();
        return db.Grupos.AnyAsync(g => g.Rotulo.ToLower() == minusculo && g.Chave != ignorarChave, ct);
    }

    private async Task<string> ChaveUnicaAsync(string rotulo, CancellationToken ct)
    {
        var baseChave = ChaveDe(rotulo);
        var chave = baseChave;
        for (var n = 2; await db.Grupos.AnyAsync(g => g.Chave == chave, ct); n++)
        {
            var sufixo = n.ToString(CultureInfo.InvariantCulture);
            chave = baseChave[..Math.Min(baseChave.Length, TamanhoMaximoChave - sufixo.Length)] + sufixo;
        }

        return chave;
    }

    /// <summary>"Água e esgoto" → <c>aguaEEsgoto</c>: sem acento, camelCase, até 30 caracteres, começando por letra.</summary>
    public static string ChaveDe(string rotulo)
    {
        var semAcento = new StringBuilder();
        foreach (var c in rotulo.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                semAcento.Append(c);
            }
        }

        var palavras = new string(semAcento.ToString().Select(c => char.IsAsciiLetterOrDigit(c) ? c : ' ').ToArray())
            .Split(' ', StringSplitOptions.RemoveEmptyEntries);

        var chave = new StringBuilder();
        foreach (var palavra in palavras)
        {
            var minuscula = palavra.ToLowerInvariant();
            chave.Append(chave.Length == 0 ? minuscula : char.ToUpperInvariant(minuscula[0]) + minuscula[1..]);
        }

        var texto = chave.ToString();
        // O prefixo entra antes de cortar: a chave nunca passa de 30 caracteres.
        if (texto.Length == 0 || !char.IsAsciiLetter(texto[0]))
        {
            texto = "grupo" + texto;
        }

        return texto.Length > TamanhoMaximoChave ? texto[..TamanhoMaximoChave] : texto;
    }

    private static GrupoResponse ParaResposta(GrupoCategoria g) => new(g.Chave, g.Rotulo, g.Tipo, g.Cor, g.Ordem);
}
