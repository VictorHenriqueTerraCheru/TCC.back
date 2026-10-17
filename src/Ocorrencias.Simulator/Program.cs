using System.Diagnostics;
using System.Globalization;
using Ocorrencias.Simulator;
using Ocorrencias.Simulator.Experimento;
using Ocorrencias.Simulator.Saida;

if (args is ["--ajuda"] or ["-h"] or ["--help"])
{
    Console.WriteLine(Opcoes.Uso);
    return 0;
}

Opcoes opcoes;
try
{
    opcoes = Opcoes.Ler(args);
    opcoes.Cenario.Validar();
}
catch (Exception e) when (e is ArgumentException or IOException or System.Text.Json.JsonException)
{
    Console.Error.WriteLine(e.Message);
    Console.Error.WriteLine();
    Console.Error.WriteLine(Opcoes.Uso);
    return 1;
}

var c = opcoes.Cenario;
Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
    $"Cenário: {c.Usuarios} usuários, {c.Problemas} problemas, {c.Dias} dias; p_passagem = {c.ProbabilidadePassagemEfetiva:G4}/h ({c.ElegiveisMedios:F0} elegíveis médios)."));
Console.WriteLine($"Configurações: {string.Join(", ", opcoes.Configuracoes.Select(x => x.Nome))} | variantes: {string.Join(", ", opcoes.Variantes)} | sementes: {opcoes.Sementes.Count} | k: 1..{c.KMaximo}");

var cronometro = Stopwatch.StartNew();
var resultado = Executor.Executar(c, opcoes.Configuracoes, opcoes.Variantes, opcoes.Sementes);
var arquivos = Csv.Escrever(opcoes.Saida, resultado);
cronometro.Stop();

Console.WriteLine();
Console.WriteLine("configuracao  fp_medio  fn_medio  resolucao_mediana_h");
foreach (var r in Resumo.PorConfiguracao(resultado))
{
    Console.WriteLine(string.Create(CultureInfo.InvariantCulture,
        $"{r.Configuracao,-12}  {Texto(r.FpMedio, "F4"),8}  {Texto(r.FnMedio, "F4"),8}  {Texto(r.ResolucaoMedianaHoras, "F1"),19}"));
}

Console.WriteLine();
Console.WriteLine("k mínimo para levar o falso a Alta em ≥ 50% das sementes:");
foreach (var k in Resumo.KMinimos(Resumo.PorAtaque(resultado)))
{
    Console.WriteLine($"  {k.Configuracao,-12} {k.Variante}: {(k.K?.ToString(CultureInfo.InvariantCulture) ?? $"> {c.KMaximo}")}");
}

Console.WriteLine();
Console.WriteLine($"{arquivos.Count} arquivos em {Path.GetFullPath(opcoes.Saida)} ({cronometro.Elapsed.TotalSeconds:F1} s).");
return 0;

static string Texto(double? valor, string formato) => valor?.ToString(formato, CultureInfo.InvariantCulture) ?? "-";
