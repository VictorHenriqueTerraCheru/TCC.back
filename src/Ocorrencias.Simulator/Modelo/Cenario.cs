namespace Ocorrencias.Simulator.Modelo;

/// <summary>
/// Parâmetros do cenário sintético (docs/PLAN.md, Etapa 7). Todos ajustáveis por JSON (<c>cenarios/base.json</c>).
/// O espaço é um plano em metros (largura × altura), equivalente a ~10 × 6 km na Zona Sul do Rio.
/// </summary>
public sealed record Cenario
{
    // Área e tempo
    public double LarguraMetros { get; init; } = 10_000;
    public double AlturaMetros { get; init; } = 6_000;
    public int Dias { get; init; } = 30;

    // Usuários honestos
    public int Usuarios { get; init; } = 1_000;
    public double FracaoVerificados { get; init; } = 0.05;
    public double RaioCirculacaoMetros { get; init; } = 1_500;

    // Problemas
    public int Problemas { get; init; } = 200;
    public int DiasCriacao { get; init; } = 20;
    public double FracaoVerdadeiros { get; init; } = 0.7;
    public double MediaResolucaoDias { get; init; } = 10;

    // Passagens e votos honestos
    /// <summary>Passagens/dia por problema no centro da área; calibra <see cref="ProbabilidadePassagem"/> quando ela é nula.</summary>
    public double PassagensPorDia { get; init; } = 2;

    /// <summary>Probabilidade por hora de um usuário elegível passar pelo problema. Nula = calibrada por <see cref="PassagensPorDia"/>.</summary>
    public double? ProbabilidadePassagem { get; init; }

    /// <summary>A distância de cada passagem é sorteada uniformemente num disco com este raio.</summary>
    public double RaioPassagemMetros { get; init; } = 500;

    public double DistanciaMaximaVotoMetros { get; init; } = 500;
    public double ProbConfirmar { get; init; } = 0.30;
    public double ProbNaoVi { get; init; } = 0.20;
    public double ProbResolvido { get; init; } = 0.30;
    public double ProbContinua { get; init; } = 0.15;
    public double ProbRuido { get; init; } = 0.05;

    // Ataque
    public int KMaximo { get; init; } = 30;
    public double DiaAtaque { get; init; } = 3;
    public double InicioAtaqueAposCriacaoHoras { get; init; } = 1;
    public double DuracaoAtaqueHoras { get; init; } = 6;
    public double DistanciaMaximaGpsFalsoMetros { get; init; } = 50;

    public int Horas => Dias * 24;

    /// <summary>Usuários elegíveis esperados por problema (círculo de circulação inteiro dentro da área).</summary>
    public double ElegiveisMedios => Usuarios * Math.PI * RaioCirculacaoMetros * RaioCirculacaoMetros / (LarguraMetros * AlturaMetros);

    public double ProbabilidadePassagemEfetiva => ProbabilidadePassagem ?? PassagensPorDia / 24 / ElegiveisMedios;

    public void Validar()
    {
        static void Exigir(bool condicao, string mensagem)
        {
            if (!condicao)
            {
                throw new ArgumentException($"Cenário inválido: {mensagem}");
            }
        }

        Exigir(LarguraMetros > 0 && AlturaMetros > 0, "área deve ser positiva");
        Exigir(Dias > 0 && DiasCriacao > 0 && DiasCriacao <= Dias, "dias de criação devem estar em (0, dias]");
        Exigir(Usuarios > 0 && Problemas > 0, "usuários e problemas devem ser positivos");
        Exigir(ProbabilidadePassagemEfetiva is > 0 and <= 1, "probabilidade de passagem deve estar em (0, 1]");
        Exigir(KMaximo >= 1, "kMaximo deve ser ≥ 1");
        Exigir(DiaAtaque >= 0 && DiaAtaque * 24 + InicioAtaqueAposCriacaoHoras + DuracaoAtaqueHoras < Horas, "o ataque precisa terminar antes do horizonte");
        foreach (var p in new[] { FracaoVerificados, FracaoVerdadeiros, ProbConfirmar, ProbNaoVi, ProbResolvido, ProbContinua, ProbRuido })
        {
            Exigir(p is >= 0 and <= 1, "probabilidades e frações devem estar em [0, 1]");
        }
    }
}
