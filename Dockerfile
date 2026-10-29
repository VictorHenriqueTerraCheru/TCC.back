# ---------- Etapa 1: compilar ----------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Primeiro só os arquivos de projeto: se eles não mudarem, o Docker reaproveita o restore (cache)
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY src/Ocorrencias.Api/Ocorrencias.Api.csproj src/Ocorrencias.Api/
RUN dotnet restore src/Ocorrencias.Api/Ocorrencias.Api.csproj

# Agora o código e a compilação
COPY src/ src/
RUN dotnet publish src/Ocorrencias.Api/Ocorrencias.Api.csproj -c Release -o /app --no-restore

# ---------- Etapa 2: rodar ----------
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .

ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Ocorrencias.Api.dll"]
