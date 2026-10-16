using Ocorrencias.Api.Categorias;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Votos;
using Ocorrencias.Domain;

namespace Ocorrencias.Api.Problemas;

public sealed record RegistrarProblemaRequest(short CategoriaId, double Lat, double Lng, string? Descricao);

public sealed record BuscarDuplicatasRequest(short CategoriaId, double Lat, double Lng);

public sealed record BuscarDuplicatasResponse(
    Guid Id,
    CategoriaResumo Categoria,
    string Endereco,
    DateTimeOffset CriadoEm,
    int DistanciaMetros,
    FaixaConfianca? Faixa,
    int Confirmacoes);

/// <param name="Grupos">Separados por vírgula (ex.: <c>viaPublica,eletrica</c>). Vazio = todos.</param>
/// <param name="Status">Separados por vírgula (ex.: <c>nova,ativa</c>). Vazio = todos os não resolvidos.</param>
public sealed record ListarProblemasNaAreaRequest(double MinLat, double MinLng, double MaxLat, double MaxLng, string? Grupos, string? Status);

public sealed record ListarProblemasNaAreaResponse(
    Guid Id, short CategoriaId, double Lat, double Lng, StatusProblema Status, double? Confianca, FaixaConfianca? Faixa);

public sealed record ListarProblemasRecentesRequest(
    double Lat,
    double Lng,
    double RaioKm = 10,
    string? Grupos = null,
    int Pagina = 1,
    int Tamanho = Paginacao.TamanhoPadrao);

public sealed record ListarProblemasRecentesItem(
    Guid Id,
    CategoriaResumo Categoria,
    string Endereco,
    DateTimeOffset CriadoEm,
    StatusProblema Status,
    FaixaConfianca? Faixa);

public sealed record ObterDetalheProblemaResponse(
    Guid Id,
    CategoriaResumo Categoria,
    string? Descricao,
    double Lat,
    double Lng,
    string Endereco,
    DateTimeOffset CriadoEm,
    StatusProblema Status,
    double? Confianca,
    FaixaConfianca? Faixa,
    ObterDetalheProblemaAvaliacao? Avaliacao,
    ObterDetalheProblemaContagemExistencia Existencia,
    ObterDetalheProblemaContagemPersistencia Persistencia,
    ObterDetalheProblemaMeusVotos? MeusVotos,
    bool SouAutor);

/// <summary>Só em <c>nova</c>: quantas pessoas já votaram e quando a avaliação encerra.</summary>
public sealed record ObterDetalheProblemaAvaliacao(int Votantes, int Necessarios, DateTimeOffset EncerraEm);

/// <summary>Contagens de pessoas (não pesos).</summary>
public sealed record ObterDetalheProblemaContagemExistencia(int Confirmacoes, int NaoVi);

public sealed record ObterDetalheProblemaContagemPersistencia(int Continua, int Resolvido, int ResolvidoUltimas24h);

public sealed record ObterDetalheProblemaMeusVotos(ValorExistencia? Existencia, ValorPersistencia? Persistencia);
