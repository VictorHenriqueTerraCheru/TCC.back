using Microsoft.AspNetCore.Http.HttpResults;

namespace UrbanReports.Api.Common;

public static class ApplicationErrorExtensions
{
    public static ProblemHttpResult ToProblem(this ApplicationError error) =>
        TypedResults.Problem(statusCode: error.Status, type: error.Type, title: error.Title);

    public static ValidationProblem ToValidation(this ApplicationError error) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]> { [error.Field!] = [error.Title!] });

    public static NotFound ToNotFound(this ApplicationError error) => TypedResults.NotFound();

    public static UnauthorizedHttpResult ToUnauthorized(this ApplicationError error) => TypedResults.Unauthorized();
}
