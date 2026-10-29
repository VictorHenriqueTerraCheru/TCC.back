using System.Net;
using UrbanReports.Api.Tests.Infra;

namespace UrbanReports.Api.Tests.Categorias;

[Collection(ApiCollection.Nome)]
public sealed class CategoriasTests(ApiFactory fabrica)
{
    [Fact]
    public async Task Visitante_lista_as_categorias_ativas_com_o_seed_inicial()
    {
        var resposta = await fabrica.CreateClient().GetAsync("/api/v1/categorias");
        var categorias = (await resposta.LerJsonAsync()).EnumerateArray().ToList();

        resposta.StatusCode.Should().Be(HttpStatusCode.OK);
        categorias.Select(c => c.GetProperty("id").GetInt32()).Take(8).Should().Equal(1, 2, 3, 4, 5, 6, 7, 8);

        var buraco = categorias[0];
        buraco.GetProperty("nome").GetString().Should().Be("Buraco na via");
        buraco.GetProperty("grupo").GetString().Should().Be("viaPublica");
        buraco.GetProperty("tipo").GetString().Should().Be("problema");
        buraco.GetProperty("icone").GetString().Should().Be("buraco");

        categorias[6].GetProperty("tipo").GetString().Should().Be("incidente");
    }
}
