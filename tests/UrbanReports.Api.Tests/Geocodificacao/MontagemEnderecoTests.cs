using System.Text.Json;
using UrbanReports.Api.Geocodificacao;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Geocodificacao;

public sealed class MontagemEnderecoTests
{
    private const double Lat = -22.9521;
    private const double Lng = -43.1868;

    [Fact]
    public void Endereco_completo()
    {
        var e = EnderecoGeocodificado.Montar(NominatimFalso.Endereco(), Lat, Lng);

        e.Should().Be(new EnderecoGeocodificado(
            true,
            "Rua Voluntários da Pátria, 170 — Botafogo",
            "Rua Voluntários da Pátria",
            "170",
            "Botafogo",
            "Rio de Janeiro",
            "RJ",
            "Rua Voluntários da Pátria, Botafogo"));
    }

    [Fact]
    public void Sem_numero_omite_a_parte()
    {
        var e = EnderecoGeocodificado.Montar(NominatimFalso.Endereco(numero: null), Lat, Lng);

        e.Endereco.Should().Be("Rua Voluntários da Pátria — Botafogo");
    }

    [Fact]
    public void Sem_logradouro_usa_bairro_e_cidade_no_rotulo_e_ignora_o_numero()
    {
        var e = EnderecoGeocodificado.Montar(NominatimFalso.Endereco(rua: null), Lat, Lng);

        e.Endereco.Should().Be("Botafogo");
        e.RotuloArea.Should().Be("Botafogo, Rio de Janeiro");
    }

    [Fact]
    public void Sem_bairro()
    {
        var e = EnderecoGeocodificado.Montar(NominatimFalso.Endereco(bairro: null), Lat, Lng);

        e.Endereco.Should().Be("Rua Voluntários da Pátria, 170");
        e.RotuloArea.Should().Be("Rua Voluntários da Pátria");
    }

    [Fact]
    public void So_cidade()
    {
        var e = EnderecoGeocodificado.Montar(NominatimFalso.Endereco(rua: null, bairro: null), Lat, Lng);

        e.Disponivel.Should().BeTrue();
        e.Endereco.Should().Be("Rio de Janeiro");
        e.RotuloArea.Should().Be("Rio de Janeiro");
    }

    [Fact]
    public void Usa_alternativas_do_osm_para_bairro_e_cidade()
    {
        var resposta = new NominatimResposta(
            new NominatimResposta.Endereco { Pedestrian = "Calçadão", Neighbourhood = "Leme", Town = "Niterói", Iso3166Nivel4 = "BR-RJ" },
            null);

        var e = EnderecoGeocodificado.Montar(resposta, Lat, Lng);

        e.Logradouro.Should().Be("Calçadão");
        e.Bairro.Should().Be("Leme");
        e.Cidade.Should().Be("Niterói");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Sem_endereco_cai_no_texto_de_coordenadas(bool respostaNula)
    {
        var resposta = respostaNula ? null : new NominatimResposta(null, "Unable to geocode");

        var e = EnderecoGeocodificado.Montar(resposta, Lat, Lng);

        e.Should().Be(new EnderecoGeocodificado(false, "Próximo a -22.95210, -43.18680", null, null, null, null, null, null));
    }

    [Fact]
    public void Uf_so_para_codigo_brasileiro()
    {
        EnderecoGeocodificado.Montar(NominatimFalso.Endereco(iso: "AR-B"), Lat, Lng).Uf.Should().BeNull();
        EnderecoGeocodificado.Montar(NominatimFalso.Endereco(iso: "BR-SP"), Lat, Lng).Uf.Should().Be("SP");
    }

    [Fact]
    public void Trunca_nos_tamanhos_das_colunas()
    {
        var e = EnderecoGeocodificado.Montar(NominatimFalso.Endereco(rua: new string('R', 300)), Lat, Lng);

        e.Endereco.Should().HaveLength(EnderecoGeocodificado.TamanhoMaximoEndereco);
        e.RotuloArea.Should().HaveLength(EnderecoGeocodificado.TamanhoMaximoRotulo);
    }

    [Fact]
    public void Resposta_real_do_nominatim_em_botafogo()
    {
        // Capturada de /reverse?format=jsonv2&lat=-22.9519&lon=-43.1866&zoom=18&addressdetails=1 (2026-10-16).
        const string json = """
            {"place_id":10515445,"name":"Santander","address":{"amenity":"Santander","road":"Rua Voluntários da Pátria",
             "quarter":"Santa Marta","suburb":"Botafogo","city":"Rio de Janeiro","state":"Rio de Janeiro",
             "ISO3166-2-lvl4":"BR-RJ","region":"Região Sudeste","postcode":"22270-000","country":"Brasil","country_code":"br"}}
            """;

        var e = EnderecoGeocodificado.Montar(JsonSerializer.Deserialize<NominatimResposta>(json), -22.9519, -43.1866);

        e.Should().Be(new EnderecoGeocodificado(
            true,
            "Rua Voluntários da Pátria — Botafogo",
            "Rua Voluntários da Pátria",
            null,
            "Botafogo",
            "Rio de Janeiro",
            "RJ",
            "Rua Voluntários da Pátria, Botafogo"));
    }
}
