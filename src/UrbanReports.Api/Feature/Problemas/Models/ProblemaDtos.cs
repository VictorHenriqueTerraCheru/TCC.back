using UrbanReports.Api.Categorias;
using UrbanReports.Api.Common;
using UrbanReports.Api.Votos;

namespace UrbanReports.Api.Problemas;

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
    ObterDetalheProblemaVotos Votos,
    ValorVotoContrato? MeuVoto,
    bool SouAutor);

public sealed record ObterDetalheProblemaAvaliacao(int Votantes, int Necessarios, DateTimeOffset EncerraEm);

public sealed record ObterDetalheProblemaVotos(int Sim, int Nao, int NaoUltimas24h);
