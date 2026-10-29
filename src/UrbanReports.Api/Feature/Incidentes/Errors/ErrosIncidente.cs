using UrbanReports.Api.Common;

namespace UrbanReports.Api.Incidents;

public static class IncidentErrors
{
    public static readonly ApplicationError MissingCategory = ApplicationError.FromField("categoryId", "Categoria inexistente.");

    public static readonly ApplicationError CategoryNotIncident = ApplicationError.Rule(
        StatusCodes.Status422UnprocessableEntity, "category-not-incident", "A categoria escolhida é de problema, não de incidente.");

    public static readonly ApplicationError TimeOutsideWindow = ApplicationError.Rule(
        StatusCodes.Status422UnprocessableEntity, "time-outside-window", "O horário do incidente precisa estar nas últimas 24 horas.");

    public static readonly ApplicationError DailyIncidentLimit = ApplicationError.Rule(
        StatusCodes.Status429TooManyRequests, "daily-incident-limit", "Limite de 10 incidentes em 24 horas atingido.");
}
