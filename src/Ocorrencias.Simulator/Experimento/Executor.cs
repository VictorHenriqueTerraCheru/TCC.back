using Ocorrencias.Api.Common;
using Ocorrencias.Api.Confianca.Motor;
using Ocorrencias.Simulator.Modelo;

namespace Ocorrencias.Simulator.Experimento;

public sealed record ResultadoExperimento(
    IReadOnlyList<LinhaDiaria> Diario,
    IReadOnlyList<LinhaResolucao> Resolucoes,
    IReadOnlyList<LinhaAtaque> Ataques);

/// <summary>
/// Para cada semente: gera o mundo honesto uma vez e o avalia em cada configuração (cenário base); depois,
/// para cada variante e k, avalia só o problema alvo com as mesmas intenções honestas + as do ataque.
/// Sementes rodam em paralelo, mas cada uma grava na sua posição: a saída é determinística.
/// </summary>
public static class Executor
{
    public static ResultadoExperimento Executar(
        Cenario cenario,
        IReadOnlyList<ConfiguracaoConfianca> configuracoes,
        IReadOnlyList<Variante> variantes,
        IReadOnlyList<int> sementes,
        int? paralelismo = null)
    {
        cenario.Validar();
        var porSemente = new ResultadoExperimento[sementes.Count];

        Parallel.For(
            0,
            sementes.Count,
            new ParallelOptions { MaxDegreeOfParallelism = paralelismo ?? Environment.ProcessorCount },
            i => porSemente[i] = ExecutarSemente(cenario, configuracoes, variantes, sementes[i]));

        // Ordem final: configuração → semente → (dia | problema | variante → k), na ordem pedida.
        var ordemConfig = configuracoes.Select((c, i) => (c.Nome, i)).ToDictionary(x => x.Nome, x => x.i);
        return new ResultadoExperimento(
            porSemente.SelectMany(r => r.Diario).OrderBy(l => ordemConfig[l.Configuracao]).ThenBy(l => l.Semente).ThenBy(l => l.Dia).ToList(),
            porSemente.SelectMany(r => r.Resolucoes).OrderBy(l => ordemConfig[l.Configuracao]).ThenBy(l => l.Semente).ThenBy(l => l.Problema).ToList(),
            porSemente.SelectMany(r => r.Ataques)
                .OrderBy(l => ordemConfig[l.Configuracao]).ThenBy(l => l.Variante, StringComparer.Ordinal).ThenBy(l => l.K).ThenBy(l => l.Semente)
                .ToList());
    }

    private static ResultadoExperimento ExecutarSemente(
        Cenario c, IReadOnlyList<ConfiguracaoConfianca> configuracoes, IReadOnlyList<Variante> variantes, int semente)
    {
        var mundo = Mundo.Gerar(c, semente);
        var diario = new List<LinhaDiaria>();
        var resolucoes = new List<LinhaResolucao>();
        var ataques = new List<LinhaAtaque>();

        var ataquesPorVariante = variantes.ToDictionary(
            v => v,
            v => Enumerable.Range(1, c.KMaximo)
                .Select(k => Mesclar(mundo.Intencoes[mundo.IndiceAlvo], Ataque.Gerar(c, mundo.Alvo, v, k, semente)))
                .ToList());

        foreach (var cfg in configuracoes)
        {
            var linhas = new LinhaDoTempo[mundo.IndiceAlvo];
            for (var p = 0; p < mundo.IndiceAlvo; p++)
            {
                linhas[p] = Avaliador.Avaliar(mundo.Problemas[p], mundo.Intencoes[p], cfg, c.Horas);
            }

            diario.AddRange(MedirDiario(c, cfg, semente, mundo, linhas));
            resolucoes.AddRange(MedirResolucoes(c, cfg, semente, mundo, linhas));

            foreach (var variante in variantes)
            {
                for (var k = 1; k <= c.KMaximo; k++)
                {
                    var linha = Avaliador.Avaliar(mundo.Alvo, ataquesPorVariante[variante][k - 1], cfg, c.Horas);
                    ataques.Add(MedirAtaque(c, cfg, variante, k, semente, mundo.Alvo, linha));
                }
            }
        }

        return new ResultadoExperimento(diario, resolucoes, ataques);
    }

    private static List<Intencao> Mesclar(IReadOnlyList<Intencao> honestas, List<Intencao> ataque)
    {
        var todas = new List<Intencao>(honestas.Count + ataque.Count);
        todas.AddRange(honestas);
        todas.AddRange(ataque);
        // Ordenação estável por hora (OrderBy é estável): empates mantêm honestas antes do ataque.
        return todas.OrderBy(i => i.Hora).ToList();
    }

    /// <summary>
    /// FP: entre os falsos já criados, a fração com faixa Alta exibida (e quantos já saíram do mapa como Resolvida). FN: entre os verdadeiros ainda não
    /// resolvidos de fato e fora de Nova (Ativa ou PossivelmenteResolvida), a fração com faixa Baixa.
    /// Medido no fim de cada dia.
    /// </summary>
    private static IEnumerable<LinhaDiaria> MedirDiario(Cenario c, ConfiguracaoConfianca cfg, int semente, Mundo mundo, LinhaDoTempo[] linhas)
    {
        for (var dia = 1; dia <= c.Dias; dia++)
        {
            var h = dia * 24 - 1; // fim do dia = instante dia × 24
            var instante = h + 1.0;
            int falsos = 0, falsosAlta = 0, falsosResolvidos = 0, verdadeiros = 0, verdadeirosBaixa = 0, indevidos = 0;

            for (var p = 0; p < linhas.Length; p++)
            {
                var problema = mundo.Problemas[p];
                if (problema.CriadoHora > instante)
                {
                    continue;
                }

                var faixa = linhas[p].FaixaExibida(h, cfg);
                if (!problema.Verdadeiro)
                {
                    falsos++;
                    falsosAlta += faixa == FaixaConfianca.Alta ? 1 : 0;
                    falsosResolvidos += linhas[p].Status[h] == StatusProblema.Resolvida ? 1 : 0;
                    continue;
                }

                var resolvidoDeFato = problema.ResolucaoRealHora is { } r && r <= instante;
                if (resolvidoDeFato)
                {
                    continue;
                }

                if (linhas[p].Status[h] == StatusProblema.Resolvida)
                {
                    indevidos++;
                }
                else if (faixa is not null)
                {
                    verdadeiros++;
                    verdadeirosBaixa += faixa == FaixaConfianca.Baixa ? 1 : 0;
                }
            }

            yield return new LinhaDiaria(cfg.Nome, semente, dia, falsos, falsosAlta, falsosResolvidos, verdadeiros, verdadeirosBaixa, indevidos);
        }
    }

    private static IEnumerable<LinhaResolucao> MedirResolucoes(Cenario c, ConfiguracaoConfianca cfg, int semente, Mundo mundo, LinhaDoTempo[] linhas)
    {
        for (var p = 0; p < linhas.Length; p++)
        {
            if (mundo.Problemas[p].ResolucaoRealHora is not { } real)
            {
                continue;
            }

            var h = Array.IndexOf(linhas[p].Status, StatusProblema.Resolvida);
            double? resolvida = h >= 0 ? h + 1.0 : null;
            yield return new LinhaResolucao(
                cfg.Nome, semente, p, real, resolvida, (resolvida ?? c.Horas) - real, Censurado: resolvida is null);
        }
    }

    /// <summary>
    /// Atingir Alta = faixa Alta exibida (fora de Nova) em algum fim de hora. Recuperação = horas do fim do
    /// ataque até o falso deixar de enganar: índice em Baixa (C &lt; 40) ou status Resolvida (fora do mapa, sem
    /// receber mais votos, então o índice para de cair), censurado no horizonte.
    /// </summary>
    private static LinhaAtaque MedirAtaque(
        Cenario c, ConfiguracaoConfianca cfg, Variante variante, int k, int semente, ProblemaSimulado alvo, LinhaDoTempo linha)
    {
        double? primeiraAlta = null;
        for (var h = 0; h < c.Horas && primeiraAlta is null; h++)
        {
            if (linha.FaixaExibida(h, cfg) == FaixaConfianca.Alta)
            {
                primeiraAlta = h + 1.0;
            }
        }

        var fim = Ataque.Fim(c, alvo);
        var hFim = (int)Math.Ceiling(fim) - 1; // primeiro fim de hora ≥ fim do ataque
        double? recuperacao = null;
        for (var h = hFim; h < c.Horas && recuperacao is null; h++)
        {
            if (MotorConfianca.ClassificarFaixa(linha.Indice[h], cfg) == FaixaConfianca.Baixa
                || linha.Status[h] == StatusProblema.Resolvida)
            {
                recuperacao = Math.Max(0, h + 1.0 - fim);
            }
        }

        return new LinhaAtaque(
            cfg.Nome,
            variante.ToString(),
            k,
            semente,
            primeiraAlta is not null,
            primeiraAlta,
            linha.Status[hFim],
            linha.Indice[hFim],
            recuperacao ?? c.Horas - fim,
            Censurado: recuperacao is null);
    }
}
