using System.Globalization;
using System.Text;
using Ocorrencias.Simulator.Experimento;

namespace Ocorrencias.Simulator.Saida;

/// <summary>CSVs em cultura invariante, separador vírgula, fim de linha \n e UTF-8 sem BOM (bytes idênticos entre execuções).</summary>
public static class Csv
{
    private static readonly UTF8Encoding Utf8SemBom = new(encoderShouldEmitUTF8Identifier: false);

    public static IReadOnlyList<string> Escrever(string diretorio, ResultadoExperimento r)
    {
        Directory.CreateDirectory(diretorio);
        var resumoAtaque = Resumo.PorAtaque(r);

        var arquivos = new Dictionary<string, string>
        {
            ["base_diario.csv"] = Tabela(
                "configuracao,semente,dia,falsos_existentes,falsos_alta,fp,verdadeiros_avaliados,verdadeiros_baixa,fn,verdadeiros_resolvidos_indevidamente",
                r.Diario.Select(l => Linha(l.Configuracao, l.Semente, l.Dia, l.FalsosExistentes, l.FalsosAlta, Fracao(l.Fp),
                    l.VerdadeirosAvaliados, l.VerdadeirosBaixa, Fracao(l.Fn), l.VerdadeirosResolvidosIndevidamente))),
            ["resolucao.csv"] = Tabela(
                "configuracao,semente,problema,resolucao_real_h,resolvida_h,tempo_h,censurado",
                r.Resolucoes.Select(l => Linha(l.Configuracao, l.Semente, l.Problema, Horas(l.ResolucaoRealHora), Horas(l.ResolvidaHora),
                    Horas(l.TempoHoras), Bool(l.Censurado)))),
            ["ataque.csv"] = Tabela(
                "configuracao,variante,k,semente,atingiu_alta,primeira_alta_h,status_fim_ataque,indice_fim_ataque,recuperacao_h,censurado",
                r.Ataques.Select(l => Linha(l.Configuracao, l.Variante, l.K, l.Semente, Bool(l.AtingiuAlta), Horas(l.PrimeiraAltaHora),
                    Camel(l.StatusFimAtaque.ToString()), Indice(l.IndiceFimAtaque), Horas(l.TempoRecuperacaoHoras), Bool(l.Censurado)))),
            ["resumo_configuracoes.csv"] = Tabela(
                "configuracao,sementes,fp_medio,fn_medio,resolvidos_indevidamente_medio,resolucao_mediana_h,resolucao_censurados",
                Resumo.PorConfiguracao(r).Select(l => Linha(l.Configuracao, l.Sementes, Fracao(l.FpMedio), Fracao(l.FnMedio),
                    Fracao(l.IndevidosMedio), Horas(l.ResolucaoMedianaHoras), Fracao(l.ResolucaoCensurados)))),
            ["resumo_ataque.csv"] = Tabela(
                "configuracao,variante,k,taxa_alta,recuperacao_mediana_h,recuperacao_censurados",
                resumoAtaque.Select(l => Linha(l.Configuracao, l.Variante, l.K, Fracao(l.TaxaAlta), Horas(l.RecuperacaoMedianaHoras),
                    Fracao(l.RecuperacaoCensurados)))),
            ["k_minimo.csv"] = Tabela(
                "configuracao,variante,k_minimo_alta",
                Resumo.KMinimos(resumoAtaque).Select(l => Linha(l.Configuracao, l.Variante, l.K?.ToString(CultureInfo.InvariantCulture) ?? ""))),
        };

        foreach (var (nome, conteudo) in arquivos)
        {
            File.WriteAllText(Path.Combine(diretorio, nome), conteudo, Utf8SemBom);
        }

        return arquivos.Keys.ToList();
    }

    private static string Tabela(string cabecalho, IEnumerable<string> linhas)
    {
        var sb = new StringBuilder(cabecalho).Append('\n');
        foreach (var linha in linhas)
        {
            sb.Append(linha).Append('\n');
        }

        return sb.ToString();
    }

    private static string Linha(params object?[] valores) =>
        string.Join(',', valores.Select(v => Convert.ToString(v, CultureInfo.InvariantCulture) ?? ""));

    private static string Fracao(double? v) => v?.ToString("F6", CultureInfo.InvariantCulture) ?? "";

    private static string Horas(double? v) => v?.ToString("F2", CultureInfo.InvariantCulture) ?? "";

    private static string Indice(double v) => v.ToString("F3", CultureInfo.InvariantCulture);

    private static string Bool(bool v) => v ? "1" : "0";

    private static string Camel(string s) => char.ToLowerInvariant(s[0]) + s[1..];
}
