using Microsoft.EntityFrameworkCore;
using Npgsql;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Verificacao;

public sealed class VerificacaoService(AppDbContext db, TimeProvider relogio) : IVerificacaoService
{
    public async Task<Resultado<SolicitarVerificacaoResponse>> SolicitarAsync(Guid usuarioId, SolicitarVerificacaoRequest request, CancellationToken ct)
    {
        var perfil = await db.Users.Where(u => u.Id == usuarioId).Select(u => (Perfil?)u.Perfil).SingleOrDefaultAsync(ct);
        if (perfil is null)
        {
            return ErroAplicacao.NaoAutorizado;
        }

        if (perfil != Perfil.Cidadao)
        {
            return ErrosVerificacao.JaVerificado;
        }

        if (await db.SolicitacoesVerificacao.AnyAsync(s => s.UsuarioId == usuarioId && s.Status == StatusVerificacao.Pendente, ct))
        {
            return ErrosVerificacao.VerificacaoPendenteExistente;
        }

        var solicitacao = new SolicitacaoVerificacao
        {
            Id = Guid.CreateVersion7(),
            UsuarioId = usuarioId,
            TipoVinculo = request.TipoVinculo,
            Justificativa = request.Justificativa.Trim(),
            Status = StatusVerificacao.Pendente,
            CriadaEm = relogio.GetUtcNow(),
        };
        db.SolicitacoesVerificacao.Add(solicitacao);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException e) when (e.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            // Corrida entre a checagem e o INSERT: o índice único parcial barra a segunda pendente.
            return ErrosVerificacao.VerificacaoPendenteExistente;
        }

        return new SolicitarVerificacaoResponse(
            solicitacao.Id, solicitacao.TipoVinculo, solicitacao.Justificativa, solicitacao.Status, solicitacao.CriadaEm);
    }
}
