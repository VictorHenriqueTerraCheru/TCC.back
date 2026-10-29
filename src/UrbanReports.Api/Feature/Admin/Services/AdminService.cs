using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Admin;

public sealed class AdminService(AppDbContext db, TimeProvider relogio) : IAdminService
{
    public async Task<Paginado<ListarVerificacoesItem>> ListarVerificacoesAsync(ListarVerificacoesRequest request, CancellationToken ct)
    {
        var consulta = db.SolicitacoesVerificacao.AsQueryable();
        StatusVerificacao? status = request.Status is null ? null : Enum.Parse<StatusVerificacao>(request.Status, ignoreCase: true);
        if (status is not null)
        {
            consulta = consulta.Where(s => s.Status == status);
        }

        var total = await consulta.CountAsync(ct);
        var ordenada = status == StatusVerificacao.Pendente
            ? consulta.OrderBy(s => s.CriadaEm)
            : consulta.OrderByDescending(s => s.CriadaEm);

        var linhas = await ordenada
            .ThenBy(s => s.Id)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .Select(s => new
            {
                s.Id,
                s.UsuarioId,
                s.Usuario.Nome,
                s.TipoVinculo,
                s.Justificativa,
                s.Status,
                s.CriadaEm,
                s.MotivoRecusa,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var itens = linhas
            .Select(l => new ListarVerificacoesItem(
                l.Id, new ListarVerificacoesUsuarioResumo(l.UsuarioId, l.Nome, Iniciais(l.Nome)),
                l.TipoVinculo, l.Justificativa, l.Status, l.CriadaEm, l.MotivoRecusa))
            .ToList();

        return new Paginado<ListarVerificacoesItem>(itens, request.Pagina, request.Tamanho, total);
    }

    public async Task<Resultado<Vazio>> AprovarAsync(Guid id, Guid adminId, CancellationToken ct)
    {
        var agora = relogio.GetUtcNow();
        await using var transacao = await db.Database.BeginTransactionAsync(ct);

        var conclusao = await ConcluirAsync(id, adminId, StatusVerificacao.Aprovada, null, agora, ct);
        if (conclusao.Erro is { } erro)
        {
            return erro;
        }

        await db.Users
            .Where(u => u.Id == conclusao.Valor && u.Perfil == Perfil.Cidadao)
            .ExecuteUpdateAsync(s => s
                .SetProperty(u => u.Perfil, Perfil.CidadaoVerificado)
                .SetProperty(u => u.VerificadoEm, agora), ct);

        await transacao.CommitAsync(ct);
        return new Vazio();
    }

    public async Task<Resultado<Vazio>> RecusarAsync(Guid id, Guid adminId, RecusarVerificacaoRequest request, CancellationToken ct)
    {
        var conclusao = await ConcluirAsync(id, adminId, StatusVerificacao.Recusada, request.Motivo.Trim(), relogio.GetUtcNow(), ct);
        if (conclusao.Erro is { } erro)
        {
            return erro;
        }

        return new Vazio();
    }

    public async Task<Paginado<ListarUsuariosItem>> ListarUsuariosAsync(ListarUsuariosRequest request, CancellationToken ct)
    {
        var consulta = db.Users.AsQueryable();
        if (request.Perfil is not null)
        {
            var perfil = Enum.Parse<Perfil>(request.Perfil, ignoreCase: true);
            consulta = consulta.Where(u => u.Perfil == perfil);
        }

        if (request.Busca?.Trim() is { Length: > 0 } busca)
        {
            var padrao = $"%{busca.Replace("\\", "\\\\").Replace("%", "\\%").Replace("_", "\\_")}%";
            consulta = consulta.Where(u => EF.Functions.ILike(u.Nome, padrao) || EF.Functions.ILike(u.Email!, padrao));
        }

        var total = await consulta.CountAsync(ct);
        var itens = await consulta
            .OrderByDescending(u => u.CriadoEm)
            .ThenBy(u => u.Id)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .Select(u => new ListarUsuariosItem(u.Id, u.Nome, u.Email!, u.Perfil, u.CriadoEm, u.VerificadoEm))
            .AsNoTracking()
            .ToListAsync(ct);

        return new Paginado<ListarUsuariosItem>(itens, request.Pagina, request.Tamanho, total);
    }

    public async Task<Resultado<Vazio>> AlterarPerfilAsync(Guid id, Guid adminId, AlterarPerfilRequest request, CancellationToken ct)
    {
        if (id == adminId)
        {
            return ErrosAdmin.ProprioPerfil;
        }

        var usuario = await db.Users.SingleOrDefaultAsync(u => u.Id == id, ct);
        if (usuario is null)
        {
            return ErroAplicacao.NaoEncontrado;
        }

        usuario.Perfil = request.Perfil;
        switch (request.Perfil)
        {
            case Perfil.CidadaoVerificado:
                usuario.VerificadoEm ??= relogio.GetUtcNow();
                break;
            case Perfil.Cidadao:
                usuario.VerificadoEm = null;
                break;
        }

        await db.SaveChangesAsync(ct);
        return new Vazio();
    }

    public static string Iniciais(string nome)
    {
        var partes = nome.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return partes.Length switch
        {
            0 => "",
            1 => char.ToUpperInvariant(partes[0][0]).ToString(),
            _ => string.Concat(char.ToUpperInvariant(partes[0][0]), char.ToUpperInvariant(partes[^1][0])),
        };
    }

    private async Task<Resultado<Guid>> ConcluirAsync(
        Guid id, Guid adminId, StatusVerificacao resultado, string? motivo, DateTimeOffset agora, CancellationToken ct)
    {
        var alteradas = await db.SolicitacoesVerificacao
            .Where(s => s.Id == id && s.Status == StatusVerificacao.Pendente)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, resultado)
                .SetProperty(x => x.MotivoRecusa, motivo)
                .SetProperty(x => x.AnalisadaPorId, adminId)
                .SetProperty(x => x.AnalisadaEm, agora), ct);

        if (alteradas == 1)
        {
            return await db.SolicitacoesVerificacao.Where(s => s.Id == id).Select(s => s.UsuarioId).SingleAsync(ct);
        }

        if (await db.SolicitacoesVerificacao.AnyAsync(s => s.Id == id, ct))
        {
            return ErrosAdmin.VerificacaoJaAnalisada;
        }

        return ErroAplicacao.NaoEncontrado;
    }
}
