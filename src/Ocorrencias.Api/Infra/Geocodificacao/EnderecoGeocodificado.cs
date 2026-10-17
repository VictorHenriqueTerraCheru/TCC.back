using System.Globalization;

namespace Ocorrencias.Api.Infra.Geocodificacao;

/// <summary>Endereço montado conforme docs/API.md (seção Geocodificação).</summary>
public sealed record EnderecoGeocodificado(
    bool Disponivel,
    string Endereco,
    string? Logradouro,
    string? Numero,
    string? Bairro,
    string? Cidade,
    string? Uf,
    string? RotuloArea)
{
    public const int TamanhoMaximoEndereco = 200;
    public const int TamanhoMaximoRotulo = 120;

    /// <summary>Resposta quando o Nominatim falha e não há cache: <c>"Próximo a -22.95210, -43.18680"</c>.</summary>
    public static EnderecoGeocodificado Indisponivel(double lat, double lng) =>
        new(false, TextoCoordenadas(lat, lng), null, null, null, null, null, null);

    /// <summary>
    /// <c>endereco</c> = <c>logradouro[, numero] — bairro</c> (omitindo partes ausentes);
    /// <c>rotuloArea</c> = <c>logradouro, bairro</c> ou, sem logradouro, <c>bairro, cidade</c>.
    /// Sem endereço utilizável na resposta, cai no texto de coordenadas.
    /// </summary>
    public static EnderecoGeocodificado Montar(NominatimResposta? resposta, double lat, double lng)
    {
        var a = resposta?.Address;
        if (a is null)
        {
            return Indisponivel(lat, lng);
        }

        var logradouro = Primeiro(a.Road, a.Pedestrian);
        var numero = Primeiro(a.HouseNumber);
        var bairro = Primeiro(a.Suburb, a.Neighbourhood, a.Quarter);
        var cidade = Primeiro(a.City, a.Town, a.Village, a.Municipality);
        var uf = ExtrairUf(a.Iso3166Nivel4);

        var rua = Juntar(", ", logradouro, logradouro is null ? null : numero);
        var endereco = Juntar(" — ", rua, bairro) ?? cidade;
        if (endereco is null)
        {
            return Indisponivel(lat, lng);
        }

        var rotulo = logradouro is not null
            ? Juntar(", ", logradouro, bairro)
            : Juntar(", ", bairro, cidade);

        return new EnderecoGeocodificado(
            true,
            Truncar(endereco, TamanhoMaximoEndereco),
            logradouro,
            numero,
            bairro,
            cidade,
            uf,
            rotulo is null ? null : Truncar(rotulo, TamanhoMaximoRotulo));
    }

    private static string TextoCoordenadas(double lat, double lng) =>
        string.Create(CultureInfo.InvariantCulture, $"Próximo a {lat:F5}, {lng:F5}");

    private static string? Primeiro(params string?[] valores) =>
        valores.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrEmpty(v));

    private static string? Juntar(string separador, params string?[] partes)
    {
        var presentes = partes.Where(p => !string.IsNullOrEmpty(p)).ToArray();
        return presentes.Length == 0 ? null : string.Join(separador, presentes);
    }

    /// <summary><c>"BR-RJ"</c> → <c>"RJ"</c>.</summary>
    private static string? ExtrairUf(string? iso) =>
        iso is { Length: > 3 } && iso.StartsWith("BR-", StringComparison.OrdinalIgnoreCase) ? iso[3..].ToUpperInvariant() : null;

    private static string Truncar(string texto, int maximo) => texto.Length <= maximo ? texto : texto[..maximo];
}
