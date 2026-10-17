using Ocorrencias.Api.Common;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddProblemDetails();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.ConfigureHttpJsonOptions(o => JsonConfig.Configurar(o.SerializerOptions));
builder.Services.AddCorsPorConfiguracao(builder.Configuration);

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();
app.UseCors(CorsConfig.Politica);

app.MapOpenApi();
app.MapScalarApiReference();
app.MapHealthChecks("/health");

app.Run();
