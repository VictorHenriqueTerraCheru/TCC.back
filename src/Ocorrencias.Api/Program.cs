using FluentValidation;
using Ocorrencias.Api.Common;
using Ocorrencias.Api.Features;
using Ocorrencias.Api.Infra.Auth;
using Ocorrencias.Api.Infra.Data;
using Ocorrencias.Api.Infra.Geocodificacao;
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
