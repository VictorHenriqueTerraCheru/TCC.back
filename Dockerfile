FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/UrbanReports.Api/UrbanReports.Api.csproj src/UrbanReports.Api/
RUN dotnet restore src/UrbanReports.Api/UrbanReports.Api.csproj

COPY src/ src/
RUN dotnet publish src/UrbanReports.Api/UrbanReports.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "UrbanReports.Api.dll"]
