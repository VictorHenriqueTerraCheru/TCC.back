namespace UrbanReports.Api.Common;

public enum Role
{
    Citizen = 1,
    VerifiedCitizen = 2,
    Administrator = 3,
}

public enum VoteValue
{
    Positive = 1,
    Negative = 2,
}

public enum IssueStatus
{
    New = 1,
    Active = 2,
    PossiblyResolved = 3,
    Resolved = 4,
}

public enum TrustBand
{
    Low = 1,
    Medium = 2,
    High = 3,
}

public enum ReportType
{
    Issue = 1,
    Incident = 2,
}

public enum GroupColor
{
    Orange = 1,
    Ochre = 2,
    Brown = 3,
    Red = 4,
    Terracotta = 5,
    Blue = 6,
    Teal = 7,
    Purple = 8,
    Pink = 9,
    Gray = 10,
}

public enum AffiliationType
{
    LivesInArea = 1,
    WorksInArea = 2,
    PublicServant = 3,
    Other = 4,
}

public enum VerificationStatus
{
    Pending = 1,
    Approved = 2,
    Rejected = 3,
}

public enum ConcentrationLevel
{
    Low = 1,
    Medium = 2,
    High = 3,
}
