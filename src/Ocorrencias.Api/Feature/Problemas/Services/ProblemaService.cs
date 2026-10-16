using Microsoft.EntityFrameworkCore;
using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Votos;
using Ocorrencias.Api.Geocodificacao;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Api.Problemas;

public sealed class ProblemaService(
    AppDbContext db, IServicoGeocodificacao geocodificacao, TimeProvider relogio, ConfiguracaoConfianca cfg) : IProblemaService
{
    /// <summary>O endereço vem da geocodificação reversa no servidor (nunca do cliente) e nunca impede o registro.</summary>
    public async Task<Resultado<ObterDetalheProblemaResponse>> RegistrarAsync(RegistrarProblemaRequest request, Guid autorId, CancellationToken ct)
    {
        var categoria = await db.Categorias.FindAsync([request.CategoriaId], ct);
        if (categoria is null)
        {
            return ErrosProblema.CategoriaInexistente;
        }

        if (categoria.Tipo != TipoOcorrencia.Problema)
        {
            return ErrosProblema.CategoriaNaoEProblema;
        }

        var endereco = await geocodificacao.ReversaAsync(request.Lat, request.Lng, ct);
        var agora = relogio.GetUtcNow();

        var problema = new Problema
        {
            Id = Guid.CreateVersion7(),
            CategoriaId = categoria.Id,
            AutorId = autorId,
            Descricao = string.IsNullOrWhiteSpace(request.Descricao) ? null : request.Descricao.Trim(),
            Localizacao = Geo.Ponto(request.Lat, request.Lng),
            EnderecoAproximado = endereco.Endereco,
            Status = StatusProblema.Nova,
            CriadoEm = agora,
            StatusAlteradoEm = agora,
        };
        db.Problemas.Add(problema);
        await db.SaveChangesAsync(ct);

        return (await ConsultarDetalheAsync(problema.Id, autorId, agora, ct))!;
    }

    public async Task<Resultado<ObterDetalheProblemaResponse>> ObterDetalheAsync(Guid id, Guid? usuarioId, CancellationToken ct)
    {
        var detalhe = await ConsultarDetalheAsync(id, usuarioId, relogio.GetUtcNow(), ct);
        if (detalhe is null)
        {
            return ErroAplicacao.NaoEncontrado;
        }

        return detalhe;
    }

    /// <param name="usuarioId">Nulo para visitante: <c>meusVotos</c> = null e <c>souAutor</c> = false.</param>
    public async Task<ObterDetalheProblemaResponse?> ConsultarDetalheAsync(Guid id, Guid? usuarioId, DateTimeOffset agora, CancellationToken ct)
    {
        var limite24h = agora.AddHours(-24);

        var linha = await db.Problemas
            .Where(p => p.Id == id)
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.Descricao,
                p.Localizacao,
                p.EnderecoAproximado,
                p.CriadoEm,
                p.Status,
                p.Confianca,
                p.AutorId,
                Votantes = p.Votos.Select(v => v.UsuarioId).Distinct().Count(),
                Confirmacoes = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Positivo),
                NaoVi = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Negativo),
                Continua = p.Votos.Count(v => v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Positivo),
                Resolvido = p.Votos.Count(v => v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Negativo),
                ResolvidoUltimas24h = p.Votos.Count(v =>
                    v.Tipo == TipoVoto.Persistencia && v.Valor == ValorVoto.Negativo && v.RegistradoEm >= limite24h),
                MeuExistencia = p.Votos
                    .Where(v => v.UsuarioId == usuarioId && v.Tipo == TipoVoto.Existencia)
                    .Select(v => (ValorVoto?)v.Valor)
                    .FirstOrDefault(),
                MeuPersistencia = p.Votos
                    .Where(v => v.UsuarioId == usuarioId && v.Tipo == TipoVoto.Persistencia)
                    .Select(v => (ValorVoto?)v.Valor)
                    .FirstOrDefault(),
            })
            .AsNoTracking()
            .FirstOrDefaultAsync(ct);

        if (linha is null)
        {
            return null;
        }

        var (confianca, faixa) = ExibicaoConfianca.Calcular(linha.Status, linha.Confianca, cfg);

        return new ObterDetalheProblemaResponse(
            linha.Id,
            linha.Categoria,
            linha.Descricao,
            linha.Localizacao.Y,
            linha.Localizacao.X,
            linha.EnderecoAproximado,
            linha.CriadoEm,
            linha.Status,
            confianca,
            faixa,
            linha.Status == StatusProblema.Nova
                ? new ObterDetalheProblemaAvaliacao(linha.Votantes, cfg.VotantesParaSairDeNova, linha.CriadoEm + cfg.TempoMaximoEmNova)
                : null,
            new ObterDetalheProblemaContagemExistencia(linha.Confirmacoes, linha.NaoVi),
            new ObterDetalheProblemaContagemPersistencia(linha.Continua, linha.Resolvido, linha.ResolvidoUltimas24h),
            usuarioId is null
                ? null
                : new ObterDetalheProblemaMeusVotos((ValorExistencia?)linha.MeuExistencia, (ValorPersistencia?)linha.MeuPersistencia),
            usuarioId is not null && linha.AutorId == usuarioId);
    }

    /// <summary>Mesma categoria, não resolvidos, a até 50 m (<c>ST_DWithin</c>), do mais próximo ao mais distante.</summary>
    public async Task<List<BuscarDuplicatasResponse>> BuscarDuplicatasAsync(BuscarDuplicatasRequest request, CancellationToken ct)
    {
        var ponto = Geo.Ponto(request.Lat, request.Lng);

        var linhas = await db.Problemas
            .Where(p => p.CategoriaId == request.CategoriaId
                && p.Status != StatusProblema.Resolvida
                && p.Localizacao.IsWithinDistance(ponto, LimitesProblema.RaioDuplicatasMetros))
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.EnderecoAproximado,
                p.CriadoEm,
                Distancia = p.Localizacao.Distance(ponto),
                p.Status,
                p.Confianca,
                Confirmacoes = p.Votos.Count(v => v.Tipo == TipoVoto.Existencia && v.Valor == ValorVoto.Positivo),
            })
            .OrderBy(p => p.Distancia)
            .Take(LimitesProblema.MaximoDuplicatas)
            .AsNoTracking()
            .ToListAsync(ct);

        return linhas
            .Select(l => new BuscarDuplicatasResponse(
                l.Id,
                l.Categoria,
                l.EnderecoAproximado,
                l.CriadoEm,
                (int)Math.Round(l.Distancia, MidpointRounding.AwayFromZero),
                ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg).Faixa,
                l.Confirmacoes))
            .ToList();
    }

    /// <summary>Marcadores da área visível. Nunca retorna <c>resolvida</c>; até 500, os de maior confiança primeiro.</summary>
    public async Task<List<ListarProblemasNaAreaResponse>> ListarNaAreaAsync(ListarProblemasNaAreaRequest request, CancellationToken ct)
    {
        var grupos = Listas.Separar(request.Grupos);
        var pedidos = Listas.Separar(request.Status).Select(s => Enum.Parse<StatusProblema>(s, ignoreCase: true)).ToArray();
        StatusProblema[] status = pedidos.Length == 0
            ? [StatusProblema.Nova, StatusProblema.Ativa, StatusProblema.PossivelmenteResolvida]
            : pedidos.Where(s => s != StatusProblema.Resolvida).Distinct().ToArray();
        if (status.Length == 0)
        {
            return []; // só "resolvida" foi pedido, e o mapa nunca mostra resolvidos
        }

        // Recorte planar em lat/lng (geometry), não geodésico: em geography as arestas do retângulo seriam
        // geodésicas e, em viewport largo, incluiriam pontos de fora e excluiriam pontos de dentro.
        // Usa o índice GIST de expressão ix_problemas_localizacao_geometry.
        var consulta = db.Problemas
            .FromSql($"""
                SELECT * FROM problemas
                WHERE localizacao::geometry && ST_MakeEnvelope({request.MinLng}, {request.MinLat}, {request.MaxLng}, {request.MaxLat}, 4326)
                """)
            .Where(p => status.Contains(p.Status));
        if (grupos.Length > 0)
        {
            consulta = consulta.Where(p => grupos.Contains(p.Categoria.Grupo));
        }

        var linhas = await consulta
            .OrderByDescending(p => p.Confianca.HasValue && p.Status != StatusProblema.Nova)
            .ThenByDescending(p => p.Confianca)
            .ThenByDescending(p => p.CriadoEm)
            .Take(LimitesProblema.MaximoNaArea)
            .Select(p => new { p.Id, p.CategoriaId, p.Localizacao, p.Status, p.Confianca })
            .AsNoTracking()
            .ToListAsync(ct);

        return linhas
            .Select(l =>
            {
                var (confianca, faixa) = ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg);
                return new ListarProblemasNaAreaResponse(l.Id, l.CategoriaId, l.Localizacao.Y, l.Localizacao.X, l.Status, confianca, faixa);
            })
            .ToList();
    }

    /// <summary>Problemas não resolvidos no raio, mais recentes primeiro.</summary>
    public async Task<Paginado<ListarProblemasRecentesItem>> ListarRecentesAsync(ListarProblemasRecentesRequest request, CancellationToken ct)
    {
        var ponto = Geo.Ponto(request.Lat, request.Lng);
        var grupos = Listas.Separar(request.Grupos);

        var consulta = db.Problemas.Where(p =>
            p.Status != StatusProblema.Resolvida && p.Localizacao.IsWithinDistance(ponto, request.RaioKm * 1000));
        if (grupos.Length > 0)
        {
            consulta = consulta.Where(p => grupos.Contains(p.Categoria.Grupo));
        }

        var total = await consulta.CountAsync(ct);
        var linhas = await consulta
            .OrderByDescending(p => p.CriadoEm)
            .ThenBy(p => p.Id)
            .Skip(Paginacao.Pular(request.Pagina, request.Tamanho))
            .Take(request.Tamanho)
            .Select(p => new
            {
                p.Id,
                Categoria = new CategoriaResumo(p.Categoria.Id, p.Categoria.Nome, p.Categoria.Grupo, p.Categoria.Icone),
                p.EnderecoAproximado,
                p.CriadoEm,
                p.Status,
                p.Confianca,
            })
            .AsNoTracking()
            .ToListAsync(ct);

        var itens = linhas
            .Select(l => new ListarProblemasRecentesItem(
                l.Id, l.Categoria, l.EnderecoAproximado, l.CriadoEm, l.Status,
                ExibicaoConfianca.Calcular(l.Status, l.Confianca, cfg).Faixa))
            .ToList();

        return new Paginado<ListarProblemasRecentesItem>(itens, request.Pagina, request.Tamanho, total);
    }
}
