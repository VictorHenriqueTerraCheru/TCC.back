using Microsoft.AspNetCore.Http;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Common;

public sealed class ErroAplicacaoTests
{
    [Fact]
    public void Regra_vira_problem_details_com_status_type_e_title()
    {
        var resultado = ErroAplicacao.Regra(StatusCodes.Status422UnprocessableEntity, "problema-resolvido", "O problema já foi resolvido.").ParaProblema();

        resultado.StatusCode.Should().Be(422);
        resultado.ProblemDetails.Status.Should().Be(422);
        resultado.ProblemDetails.Type.Should().Be("problema-resolvido");
        resultado.ProblemDetails.Title.Should().Be("O problema já foi resolvido.");
    }

    [Fact]
    public void Regra_sem_type_equivale_ao_problem_de_hoje()
    {
        var novo = ErroAplicacao.Regra(StatusCodes.Status401Unauthorized, null, "E-mail ou senha inválidos.").ParaProblema();
        var antigo = TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha inválidos.");

        novo.StatusCode.Should().Be(antigo.StatusCode);
        novo.ProblemDetails.Should().BeEquivalentTo(antigo.ProblemDetails);
    }

    [Fact]
    public void Campo_vira_validation_problem_com_o_campo_em_errors()
    {
        var resultado = ErroAplicacao.DeCampo("categoriaId", "Categoria inexistente.").ParaValidacao();

        resultado.StatusCode.Should().Be(400);
        resultado.ProblemDetails.Errors.Should().ContainKey("categoriaId").WhoseValue.Should().Equal("Categoria inexistente.");
    }

    [Fact]
    public void Nao_encontrado_e_nao_autorizado_viram_status_sem_corpo()
    {
        ErroAplicacao.NaoEncontrado.ParaNaoEncontrado().StatusCode.Should().Be(404);
        ErroAplicacao.NaoAutorizado.ParaNaoAutorizado().StatusCode.Should().Be(401);
    }

    [Fact]
    public void Resultado_converte_de_valor_e_de_erro()
    {
        Resultado<int> sucesso = 7;
        Resultado<int> falha = ErroAplicacao.NaoEncontrado;

        sucesso.Sucesso.Should().BeTrue();
        sucesso.Valor.Should().Be(7);
        sucesso.Erro.Should().BeNull();
        falha.Sucesso.Should().BeFalse();
        falha.Erro.Should().Be(ErroAplicacao.NaoEncontrado);
    }
}
