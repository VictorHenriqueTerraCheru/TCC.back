using Ocorrencias.Domain;
using Ocorrencias.Domain.Confianca;

namespace Ocorrencias.Domain.Tests.Confianca;

/// <summary>Atalhos para montar votos e estados nos testes.</summary>
internal static class Cenario
{
    public static readonly DateTimeOffset T0 = new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    public static VotoFato Voto(
        TipoVoto tipo,
        ValorVoto valor,
        Perfil perfil = Perfil.Cidadao,
        double? distancia = 100,
        DateTimeOffset? em = null,
        Guid? usuario = null) =>
        new(usuario ?? Guid.NewGuid(), tipo, valor, perfil, distancia, em ?? T0);

    public static VotoFato Confirma(Perfil perfil = Perfil.Cidadao, double? distancia = 100, DateTimeOffset? em = null, Guid? usuario = null) =>
        Voto(TipoVoto.Existencia, ValorVoto.Positivo, perfil, distancia, em, usuario);

    public static VotoFato NaoVi(Perfil perfil = Perfil.Cidadao, double? distancia = 100, DateTimeOffset? em = null, Guid? usuario = null) =>
        Voto(TipoVoto.Existencia, ValorVoto.Negativo, perfil, distancia, em, usuario);

    public static VotoFato Continua(Perfil perfil = Perfil.Cidadao, double? distancia = 100, DateTimeOffset? em = null, Guid? usuario = null) =>
        Voto(TipoVoto.Persistencia, ValorVoto.Positivo, perfil, distancia, em, usuario);

    public static VotoFato Resolvido(Perfil perfil = Perfil.Cidadao, double? distancia = 100, DateTimeOffset? em = null, Guid? usuario = null) =>
        Voto(TipoVoto.Persistencia, ValorVoto.Negativo, perfil, distancia, em, usuario);

    public static List<VotoFato> Repetir(int vezes, Func<VotoFato> criar) =>
        Enumerable.Range(0, vezes).Select(_ => criar()).ToList();

    public static EstadoProblema Estado(
        StatusProblema status,
        DateTimeOffset? criadoEm = null,
        DateTimeOffset? statusAlteradoEm = null,
        DateTimeOffset? condicaoResolucaoDesde = null) =>
        new(status, criadoEm ?? T0, statusAlteradoEm ?? criadoEm ?? T0, condicaoResolucaoDesde);
}
