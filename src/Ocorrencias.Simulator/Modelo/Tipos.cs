using UrbanReports.Api.Common;

namespace Ocorrencias.Simulator.Modelo;

/// <summary>Ponto no plano do cenário, em metros.</summary>
public readonly record struct Ponto(double X, double Y)
{
    public double DistanciaAte(Ponto outro) => Math.Sqrt((X - outro.X) * (X - outro.X) + (Y - outro.Y) * (Y - outro.Y));
}

public sealed record Usuario(Guid Id, Perfil Perfil, Ponto Centro);

/// <param name="ResolucaoRealHora">Hora em que o problema verdadeiro é de fato resolvido (nulo = falso ou além do horizonte).</param>
public sealed record ProblemaSimulado(int Indice, bool Verdadeiro, Ponto Local, double CriadoHora, double? ResolucaoRealHora, Guid AutorId);

/// <summary>
/// Intenção de voto: o que a pessoa tentaria registrar na API. A avaliação aplica as regras da API
/// (que dependem do estado, portanto da configuração) para decidir se o voto entra.
/// </summary>
public readonly record struct Intencao(double Hora, Guid UsuarioId, ValorVoto Valor, Perfil Perfil, double? DistanciaMetros);

public enum Variante
{
    /// <summary>Sem localização.</summary>
    S1,

    /// <summary>GPS falsificado: distância de 0 a 50 m.</summary>
    S2,

    /// <summary>S2 com uma conta verificada.</summary>
    S3,
}
