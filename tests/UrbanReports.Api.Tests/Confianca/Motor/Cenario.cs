using UrbanReports.Api.Common;
using UrbanReports.Api.Confianca.Motor;

namespace UrbanReports.Api.Tests.Confianca.Motor;

/// <summary>Atalhos para montar votos e estados nos testes.</summary>
internal static class Cenario
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public static VotoFato Voto(
        ValorVoto valor,
        Perfil perfil = Perfil.Cidadao,
        double? distancia = 100,
        DateTimeOffset? em = null,
        Guid? usuario = null) =>
        new(usuario ?? Guid.NewGuid(), valor, perfil, distancia, em ?? T0);

    /// <summary>"Ainda está lá?" → Sim.</summary>
    public static VotoFato Sim(Perfil perfil = Perfil.Cidadao, double? distancia = 100, DateTimeOffset? em = null, Guid? usuario = null) =>
        Voto(ValorVoto.Positivo, perfil, distancia, em, usuario);

    /// <summary>"Ainda está lá?" → Não (foi resolvido ou não foi encontrado).</summary>
    public static VotoFato Nao(Perfil perfil = Perfil.Cidadao, double? distancia = 100, DateTimeOffset? em = null, Guid? usuario = null) =>
        Voto(ValorVoto.Negativo, perfil, distancia, em, usuario);

    public static List<VotoFato> Repetir(int vezes, Func<VotoFato> criar) =>
        Enumerable.Range(0, vezes).Select(_ => criar()).ToList();

    public static EstadoProblema Estado(
        StatusProblema status,
        DateTimeOffset? criadoEm = null,
        DateTimeOffset? statusAlteradoEm = null,
        DateTimeOffset? condicaoResolucaoDesde = null) =>
        new(status, criadoEm ?? T0, statusAlteradoEm ?? criadoEm ?? T0, condicaoResolucaoDesde);
}
