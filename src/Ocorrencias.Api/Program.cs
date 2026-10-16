using FluentValidation;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features;
using Ocorrencias.Api.Infrastructure;
using Ocorrencias.Api.Confianca;
using Ocorrencias.Api.Persistence;
using Ocorrencias.Api.Geocodificacao;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

Validacao.ConfigurarGlobal();

builder.Services.AddProblemDetails();
builder.Services.AddOpenApiConfigurado();
builder.Services.AddSingleton<TimeProvider>(new RelogioPostgres());
builder.Services.ConfigureHttpJsonOptions(o => JsonConfig.Configurar(o.SerializerOptions));
builder.Services.AddCorsPorConfiguracao(builder.Configuration);
builder.Services.AddDados(builder.Configuration);
builder.Services.AddAutenticacao();
builder.Services.AddGeocodificacao();
builder.Services.AddConfianca(builder.Configuration);
builder.Services.AddLimitesDeRequisicao(builder.Configuration);
builder.Services.AddValidatorsFromAssemblyContaining<Program>();

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsConfig.Politica);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");
app.MapFeatures();

if (app.Environment.IsDevelopment())
{
    await app.InicializarBancoDesenvolvimentoAsync();
}

await app.RunAsync();
