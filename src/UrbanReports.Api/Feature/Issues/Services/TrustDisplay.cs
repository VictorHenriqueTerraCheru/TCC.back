using UrbanReports.Api.Common;
using UrbanReports.Api.Trust.Engine;

namespace UrbanReports.Api.Issues;

public static class TrustDisplay
{
    public static (double? TrustScore, TrustBand? Band) Calculate(IssueStatus status, decimal? trust, TrustConfiguration cfg)
    {
        if (status == IssueStatus.New || trust is not { } value)
        {
            return (null, null);
        }

        var score = (double)value;
        return (Math.Round(score, 1, MidpointRounding.AwayFromZero), TrustEngine.ClassifyBand(score, cfg));
    }
}
