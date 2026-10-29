using UrbanReports.Api.Common;

namespace UrbanReports.Api.Trust.Engine;

public sealed record TrustResult(double Score, TrustBand Band, double PositiveEvidence, double NegativeEvidence);
