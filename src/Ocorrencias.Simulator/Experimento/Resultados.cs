using UrbanReports.Api.Common;

namespace Ocorrencias.Simulator.Experimento;

/// <summary>Medição diária (fim do dia) do cenário base, sem ataque.</summary>
public sealed record LinhaDiaria(
    string Configuracao,
    int Semente,
    int Dia,
    int FalsosExistentes,
    int FalsosAlta,
    int FalsosResolvidos,
    int VerdadeirosAvaliados,
    int VerdadeirosBaixa,
    int VerdadeirosResolvidosIndevidamente)
{
    public double? Fp => FalsosExistentes == 0 ? null : (double)FalsosAlta / FalsosExistentes;
    public double? Fn => VerdadeirosAvaliados == 0 ? null : (double)VerdadeirosBaixa / VerdadeirosAvaliados;
}

/// <summary>Tempo entre a resolução real de um verdadeiro e o status Resolvida (censurado no horizonte).</summary>
public sealed record LinhaResolucao(
    string Configuracao, int Semente, int Problema, double ResolucaoRealHora, double? ResolvidaHora, double TempoHoras, bool Censurado);

/// <summary>Resultado de um ataque ao falso alvo.</summary>
public sealed record LinhaAtaque(
    string Configuracao,
    string Variante,
    int K,
    int Semente,
    bool AtingiuAlta,
    double? PrimeiraAltaHora,
    StatusProblema StatusFimAtaque,
    double IndiceFimAtaque,
    double TempoRecuperacaoHoras,
    bool Censurado);
