using Microsoft.AspNetCore.Http;
using UrbanReports.Api.Common;

namespace UrbanReports.Api.Tests.Common;

public sealed class ApplicationErrorTests
{
    [Fact]
    public void Rule_becomes_problem_details_with_status_type_and_title()
    {
        var result = ApplicationError.Rule(StatusCodes.Status422UnprocessableEntity, "issue-resolved", "O problema já foi resolvido.").ToProblem();

        result.StatusCode.Should().Be(422);
        result.ProblemDetails.Status.Should().Be(422);
        result.ProblemDetails.Type.Should().Be("issue-resolved");
        result.ProblemDetails.Title.Should().Be("O problema já foi resolvido.");
    }

    [Fact]
    public void Rule_without_type_equals_todays_problem()
    {
        var newLabel = ApplicationError.Rule(StatusCodes.Status401Unauthorized, null, "E-mail ou senha inválidos.").ToProblem();
        var old = TypedResults.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "E-mail ou senha inválidos.");

        newLabel.StatusCode.Should().Be(old.StatusCode);
        newLabel.ProblemDetails.Should().BeEquivalentTo(old.ProblemDetails);
    }

    [Fact]
    public void Field_becomes_validation_problem_with_the_field_in_errors()
    {
        var result = ApplicationError.FromField("categoryId", "Categoria inexistente.").ToValidation();

        result.StatusCode.Should().Be(400);
        result.ProblemDetails.Errors.Should().ContainKey("categoryId").WhoseValue.Should().Equal("Categoria inexistente.");
    }

    [Fact]
    public void Not_found_and_unauthorized_become_status_without_body()
    {
        ApplicationError.NotFound.ToNotFound().StatusCode.Should().Be(404);
        ApplicationError.Unauthorized.ToUnauthorized().StatusCode.Should().Be(401);
    }

    [Fact]
    public void Result_converts_from_value_and_from_error()
    {
        Result<int> success = 7;
        Result<int> failure = ApplicationError.NotFound;

        success.Success.Should().BeTrue();
        success.Value.Should().Be(7);
        success.Error.Should().BeNull();
        failure.Success.Should().BeFalse();
        failure.Error.Should().Be(ApplicationError.NotFound);
    }
}
