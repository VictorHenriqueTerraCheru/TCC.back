namespace UrbanReports.Api.Saude;

public sealed record StatusSaudeResponse(string Status)
{
    public static StatusSaudeResponse Saudavel { get; } = new("saudavel");
}
