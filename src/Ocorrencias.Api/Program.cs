using Ocorrencias.Api.Common;
using Ocorrencias.Api.Infrastructure;
using Ocorrencias.Api.Persistence;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddAplicacao(builder.Configuration)
    .AddCorsConfigurado(builder.Configuration)
    .AddOpenApiConfigurado()
    .AddAutenticacao()
    .AddLimitesDeRequisicao(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(PoliticasCors.Front);
app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");
app.MapEndpoints();

if (app.Environment.IsDevelopment())
{
    await app.InicializarBancoDesenvolvimentoAsync();
}

await app.RunAsync();
