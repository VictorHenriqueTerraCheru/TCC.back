using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Categorias;

public sealed class CategoriaService(AppDbContext db) : ICategoriaService
{
    public Task<List<ListarCategoriasResponse>> ListarAsync(CancellationToken ct) =>
        db.Categorias
            .Where(c => c.Ativa)
            .OrderBy(c => c.Id)
            .Select(c => new ListarCategoriasResponse(c.Id, c.Nome, c.Grupo, c.Tipo, c.Icone))
            .ToListAsync(ct);

    public Task<List<CategoriaAdminResponse>> ListarTodasAsync(CancellationToken ct) =>
        db.Categorias
            .OrderBy(c => c.Id)
            .Select(c => new CategoriaAdminResponse(c.Id, c.Nome, c.Grupo, c.Tipo, c.Icone, c.Ativa))
            .ToListAsync(ct);

    public async Task<Resultado<CategoriaAdminResponse>> CriarAsync(CriarCategoriaRequest request, CancellationToken ct)
    {
        var nome = request.Nome.Trim();
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await db.Database.TomarAsync(ct);

        var grupo = await db.Grupos.FindAsync([request.Grupo], ct);
        if (grupo is null)
        {
            return ErrosCategoria.GrupoInexistente;
        }

        if (grupo.Tipo != request.Tipo)
        {
            return ErrosCategoria.GrupoTipoIncompativel;
        }

        if (await NomeEmUsoAsync(nome, null, ct))
        {
            return ErrosCategoria.NomeDuplicado;
        }

        var maior = await db.Categorias.MaxAsync(c => (short?)c.Id, ct) ?? 0;
        if (maior >= short.MaxValue)
        {
            return ErrosCategoria.LimiteDeCategorias;
        }

        var categoria = new Categoria
        {
            Id = (short)(maior + 1),
            Nome = nome,
            Grupo = request.Grupo,
            Tipo = request.Tipo,
            Icone = request.Icone,
        };
        db.Categorias.Add(categoria);
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.ViolouUnicidade())
        {
            return ErrosCategoria.NomeDuplicado;
        }
        catch (DbUpdateException e) when (e.ViolouChaveEstrangeira())
        {
            return ErrosCategoria.GrupoInexistente;
        }

        await transacao.CommitAsync(ct);

        return ParaAdmin(categoria);
    }

    public async Task<Resultado<CategoriaAdminResponse>> EditarAsync(short id, EditarCategoriaRequest request, CancellationToken ct)
    {
        var nome = request.Nome.Trim();
        await using var transacao = await db.Database.BeginTransactionAsync(ct);
        await db.Database.TomarAsync(ct);

        var categoria = await db.Categorias.FindAsync([id], ct);
        if (categoria is null)
        {
            return ErroAplicacao.NaoEncontrado;
        }

        var grupo = await db.Grupos.FindAsync([request.Grupo], ct);
        if (grupo is null)
        {
            return ErrosCategoria.GrupoInexistente;
        }

        if (grupo.Tipo != categoria.Tipo)
        {
            return ErrosCategoria.GrupoTipoIncompativel;
        }

        if (await NomeEmUsoAsync(nome, id, ct))
        {
            return ErrosCategoria.NomeDuplicado;
        }

        categoria.Nome = nome;
        categoria.Grupo = request.Grupo;
        categoria.Icone = request.Icone;
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.ViolouUnicidade())
        {
            return ErrosCategoria.NomeDuplicado;
        }
        catch (DbUpdateException e) when (e.ViolouChaveEstrangeira())
        {
            return ErrosCategoria.GrupoInexistente;
        }

        await transacao.CommitAsync(ct);

        return ParaAdmin(categoria);
    }

    public async Task<Resultado<Vazio>> AlterarAtivaAsync(short id, bool ativa, CancellationToken ct)
    {
        var linhas = await db.Categorias
            .Where(c => c.Id == id)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.Ativa, ativa), ct);

        return linhas == 0 ? ErroAplicacao.NaoEncontrado : new Vazio();
    }

    private Task<bool> NomeEmUsoAsync(string nome, short? ignorarId, CancellationToken ct)
    {
        var minusculo = nome.ToLower();
        return db.Categorias.AnyAsync(c => c.Nome.ToLower() == minusculo && c.Id != ignorarId, ct);
    }

    private static CategoriaAdminResponse ParaAdmin(Categoria c) => new(c.Id, c.Nome, c.Grupo, c.Tipo, c.Icone, c.Ativa);
}
