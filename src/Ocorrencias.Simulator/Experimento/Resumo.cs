namespace Ocorrencias.Simulator.Experimento;

public sealed record ResumoConfiguracao(
    string Configuracao,
    int Sementes,
    double FpMedio,
    double FnMedio,
    double IndevidosMedio,
    double? ResolucaoMedianaHoras,
    double ResolucaoCensurados);

public sealed record ResumoAtaque(
    string Configuracao, string Variante, int K, double TaxaAlta, double RecuperacaoMedianaHoras, double RecuperacaoCensurados);

/// <summary>Menor k que leva o falso a Alta em ≥ 50% das sementes (nulo = nenhum k até o máximo).</summary>
public sealed record KMinimo(string Configuracao, string Variante, int? K);

public static class Resumo
{
    public const double LimiarKMinimo = 0.5;

    /// <summary>FP/FN: média, entre sementes, da média diária (dias sem denominador são ignorados).</summary>
    public static List<ResumoConfiguracao> PorConfiguracao(ResultadoExperimento r) =>
        r.Diario
            .GroupBy(l => l.Configuracao)
            .Select(g =>
            {
                var porSemente = g.GroupBy(l => l.Semente).ToList();
                var resolucoes = r.Resolucoes.Where(x => x.Configuracao == g.Key).ToList();
                var concluidas = resolucoes.Where(x => !x.Censurado).Select(x => x.TempoHoras).ToList();

                return new ResumoConfiguracao(
                    g.Key,
                    porSemente.Count,
                    porSemente.Average(s => MediaOuZero(s.Select(l => l.Fp))),
                    porSemente.Average(s => MediaOuZero(s.Select(l => l.Fn))),
                    porSemente.Average(s => MediaOuZero(s.Select(l => l.VerdadeirosResolvidosIndevidamente == 0 && l.VerdadeirosAvaliados == 0
                        ? (double?)null
                        : (double)l.VerdadeirosResolvidosIndevidamente / (l.VerdadeirosAvaliados + l.VerdadeirosResolvidosIndevidamente)))),
                    concluidas.Count == 0 ? null : Mediana(concluidas),
                    resolucoes.Count == 0 ? 0 : (double)resolucoes.Count(x => x.Censurado) / resolucoes.Count);
            })
            .ToList();

    public static List<ResumoAtaque> PorAtaque(ResultadoExperimento r) =>
        r.Ataques
            .GroupBy(l => (l.Configuracao, l.Variante, l.K))
            .Select(g => new ResumoAtaque(
                g.Key.Configuracao,
                g.Key.Variante,
                g.Key.K,
                (double)g.Count(l => l.AtingiuAlta) / g.Count(),
                Mediana(g.Select(l => l.TempoRecuperacaoHoras).ToList()),
                (double)g.Count(l => l.Censurado) / g.Count()))
            .ToList();

    public static List<KMinimo> KMinimos(IReadOnlyList<ResumoAtaque> ataques) =>
        ataques
            .GroupBy(a => (a.Configuracao, a.Variante))
            .Select(g => new KMinimo(
                g.Key.Configuracao,
                g.Key.Variante,
                g.OrderBy(a => a.K).FirstOrDefault(a => a.TaxaAlta >= LimiarKMinimo)?.K))
            .ToList();

    private static double MediaOuZero(IEnumerable<double?> valores)
    {
        var presentes = valores.Where(v => v.HasValue).Select(v => v!.Value).ToList();
        return presentes.Count == 0 ? 0 : presentes.Average();
    }

    public static double Mediana(IReadOnlyList<double> valores)
    {
        var ordenados = valores.Order().ToList();
        var meio = ordenados.Count / 2;
        return ordenados.Count % 2 == 1 ? ordenados[meio] : (ordenados[meio - 1] + ordenados[meio]) / 2;
    }
}
