using UrbanReports.Api.Common;

namespace UrbanReports.Api.Votes;

public enum VoteAnswer
{
    Yes = VoteValue.Positive,
    No = VoteValue.Negative,
}

public sealed record VoteRequest(VoteAnswer Value, double? Lat, double? Lng);
