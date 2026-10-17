namespace Ocorrencias.Api.Geocodificacao;

public sealed record ReversaRequest(double Lat, double Lng);

public sealed record ReversaResponse(
    bool Disponivel,
    string Endereco,
    string? Logradouro,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? RotuloArea);
