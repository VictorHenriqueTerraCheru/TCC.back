namespace UrbanReports.Api.Common;

public enum ErrorType
{
    NotFound,
    Unauthorized,
    Field,
    Rule,
}

public sealed record ApplicationError(ErrorType Kind, int Status, string? Type, string? Title, string? Field = null)
{
    public static ApplicationError NotFound { get; } = new(ErrorType.NotFound, StatusCodes.Status404NotFound, null, null);

    public static ApplicationError Unauthorized { get; } = new(ErrorType.Unauthorized, StatusCodes.Status401Unauthorized, null, null);

    public static ApplicationError FromField(string field, string message) =>
        new(ErrorType.Field, StatusCodes.Status400BadRequest, null, message, field);

    public static ApplicationError Rule(int status, string? type, string title) => new(ErrorType.Rule, status, type, title);
}
