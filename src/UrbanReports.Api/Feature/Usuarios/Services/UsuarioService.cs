using Microsoft.EntityFrameworkCore;
using UrbanReports.Api.Categorias;
using UrbanReports.Api.Common;
using UrbanReports.Api.Persistence;

namespace UrbanReports.Api.Usuarios;

public sealed class UsuarioService(AppDbContext db) : IUsuarioService
{
    public async Task<Resultado<ObterMeResponse>> ObterMeAsync(Guid usuarioId, CancellationToken ct)
    {
        var resposta = await ConsultarMeAsync(usuarioId, ct);
        if (resposta is null)
        {
            return ErroAplicacao.NaoAutorizado;
        }

        return resposta;
    }

    public async Task<Resultado<ObterMeResponse>> AtualizarMeAsync(Guid usuarioId, AtualizarMeRequest request, CancellationToken ct)
    {
        var alterados = await db.Users
            .Where(u => u.Id == usuarioId)
            .ExecuteUpdateAsync(s => s.SetProperty(u => u.Nome, request.Nome.Trim()), ct);

        if (alterados == 0)
        {
            return ErroAplicacao.NaoAutorizado;
        }

        return (await ConsultarMeAsync(usuarioId, ct))!;
    }

    public async Task<Paginado<ObterHistoricoItem>> ObterHistoricoAsync(Guid usuarioId, ObterHistoricoRequest request, CancellationToken ct)
    {
        var registros = request.Filtro is "tudo" or "registros";
        var votos = request.Filtro is "tudo" or "votos";

        var uniao = db.Database.SqlQuery<LinhaHistorico>($"""
            SELECT p.id AS chave, 1 AS tipo, p.id AS ocorrencia_id, c.id AS categoria_id, c.nome AS categoria_nome, c.grupo, c.icone,
                   p.endereco_aproximado AS endereco, NULL::smallint AS valor, p.status AS status_problema, p.criado_em AS data
            FROM problemas p JOIN categorias c ON c.id = p.categoria_id
            WHERE p.autor_id = {usuarioId} AND {registros}
            UNION ALL
            SELECT i.id, 2, NULL::uuid, c.id, c.nome, c.grupo, c.icone,
                   i.rotulo_area, NULL::smallint, NULL::smallint, i.registrado_em
            FROM incidentes i JOIN categorias c ON c.id = i.categoria_id
            WHERE i.autor_id = {usuarioId} AND {registros}
            UNION ALL
            SELECT v.id, 3, p.id, c.id, c.nome, c.grupo, c.icone,
                   p.endereco_aproximado, v.valor, p.status, v.registrado_em
            FROM votos v JOIN problemas p ON p.id = v.problema_id JOIN categorias c ON c.id = p.categoria_id
            WHERE v.usuario_id = {usuarioId} AND {votos}
            """);

        var total = await uniao.CountAsync(ct);
        var linhas = await uniao
            .OrderByDescending(l => l.Data)
            .ThenBy(l => l.Chave)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .ToListAsync(ct);

        return new Paginado<ObterHistoricoItem>(linhas.Select(Mapear).ToList(), request.Pagina, request.Tamanho, total);
    }

    private Task<ObterMeResponse?> ConsultarMeAsync(Guid usuarioId, CancellationToken ct) =>
        db.Users
            .Where(u => u.Id == usuarioId)
            .Select(u => new ObterMeResponse(
                u.Id,
                u.Nome,
                u.Email!,
                u.Perfil,
                u.CriadoEm,
                u.VerificadoEm,
                db.Problemas.Count(p => p.AutorId == u.Id) + db.Incidentes.Count(i => i.AutorId == u.Id),
                db.Votos.Count(v => v.UsuarioId == u.Id),
                db.SolicitacoesVerificacao
                    .Where(s => s.UsuarioId == u.Id)
                    .OrderByDescending(s => s.CriadaEm)
                    .Select(s => new ObterMeVerificacaoResumo(s.Status, s.TipoVinculo, s.MotivoRecusa, s.CriadaEm))
                    .FirstOrDefault()))
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

    private static ObterHistoricoItem Mapear(LinhaHistorico l)
    {
        var tipo = (ObterHistoricoTipoItem)l.Tipo;
        ObterHistoricoValorItem? valor = (ValorVoto?)l.Valor switch
        {
            ValorVoto.Positivo => ObterHistoricoValorItem.Sim,
            ValorVoto.Negativo => ObterHistoricoValorItem.Nao,
            _ => null,
        };

        return new ObterHistoricoItem(
            tipo,
            l.OcorrenciaId,
            new CategoriaResumo(l.CategoriaId, l.CategoriaNome, l.Grupo, l.Icone),
            l.Endereco,
            valor,
            (StatusProblema?)l.StatusProblema,
            l.Data);
    }
}
