using UrbanReports.Api.Common;

namespace UrbanReports.Api.Votes;

public static class VoteErrors
{
    public static readonly ApplicationError AuthorCannotVote = ApplicationError.Rule(
        StatusCodes.Status422UnprocessableEntity, "author-cannot-vote", "Quem registrou o problema não vota nele.");

    public static readonly ApplicationError IssueResolved = ApplicationError.Rule(
        StatusCodes.Status422UnprocessableEntity, "issue-resolved", "O problema já foi resolvido e não recebe mais votos.");
}
