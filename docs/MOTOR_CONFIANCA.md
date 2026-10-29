# Motor de confiança — especificação normativa

Este arquivo é a **fonte da verdade** do mecanismo de confiança (TCC, Seção 5.2). O motor em `src/UrbanReports.Api/Feature/Trust/Engine/` implementa exatamente isto. Se o código e este arquivo divergirem, o código está errado — ou este arquivo precisa ser atualizado *e* o TCC também.

O código está em inglês; o texto do TCC, em português. Correspondência dos termos:

| TCC (português) | Código (inglês) |
|---|---|
| Motor de confiança / `MotorConfianca` | `TrustEngine` |
| Configuração (A, B, B-v3, B-v5) / `ConfiguracaoConfianca` | `TrustConfiguration` |
| Voto (fato) / `VotoFato` | `VoteFact` |
| Perfil no momento do voto / `perfil_no_momento` | `RoleAtVote` / `role_at_vote` |
| Distância em metros / `distancia_metros` | `DistanceMeters` / `distance_meters` |
| Perfil: cidadão, cidadão verificado, administrador | `Role`: `Citizen`, `VerifiedCitizen`, `Administrator` |
| Valor do voto: Sim, Não | `VoteValue`: `Positive`, `Negative` (no contrato, `yes`/`no`) |
| Status: Nova, Ativa, Possivelmente resolvida, Resolvida | `IssueStatus`: `New`, `Active`, `PossiblyResolved`, `Resolved` |
| Índice de confiança `C` / `confianca` | `TrustResult.Score` / `issues.trust_score` |
| Faixa: baixa, média, alta | `TrustBand`: `Low`, `Medium`, `High` |
| Peso de perfil, de proximidade, fator temporal | `RoleWeight`, `ProximityWeight`, `TimeFactor` |
| Ciclo de vida / `AvaliarCicloDeVida` | Lifecycle / `EvaluateLifecycle` |
| Problema | Issue |

## Princípios

1. **Funções puras.** Nada de `DateTime.Now`, banco, DI ou logging. O instante atual (`now`) e a configuração entram como parâmetros.
2. **O voto guarda fatos, não pesos.** Cada voto guarda `RoleAtVote` e `DistanceMeters` (nulo = sem localização). Os pesos são derivados na hora do cálculo, a partir da configuração. Assim, os mesmos votos podem ser avaliados nas configurações A e B.
3. **A API e o Simulator chamam as mesmas funções.** É isso que garante que o experimento mede o sistema real.
4. **Um voto só: "Ainda está lá?".** Cada usuário tem no máximo um voto por problema (Sim/Não), e o mesmo voto alimenta o índice de confiança e o ciclo de vida. "Sim" = viu o problema no local; "Não" = foi resolvido ou não foi encontrado. O autor não vota no próprio problema.

## Tipos

```csharp
public enum Role { Citizen = 1, VerifiedCitizen = 2, Administrator = 3 }
public enum VoteValue { Positive = 1, Negative = 2 } // "Ainda está lá?": Sim / Não
public enum IssueStatus { New = 1, Active = 2, PossiblyResolved = 3, Resolved = 4 }
public enum TrustBand { Low = 1, Medium = 2, High = 3 }

public sealed record VoteFact(
    Guid UserId, VoteValue Value,
    Role RoleAtVote, double? DistanceMeters, DateTimeOffset RegisteredAt);

public sealed record IssueState(
    IssueStatus Status, DateTimeOffset CreatedAt, DateTimeOffset StatusChangedAt,
    DateTimeOffset? ResolutionConditionSince);
```

## Configuração

```csharp
public sealed record TrustConfiguration
{
    public required string Name { get; init; }
    // perfil
    public double CitizenWeight { get; init; } = 1;
    public double VerifiedWeight { get; init; } = 10;
    public double AdministratorWeight { get; init; } = 1;
    // proximidade
    public bool UseProximity { get; init; } = true;
    public double FullBonusRadiusMeters { get; init; } = 200;
    public double ZeroBonusRadiusMeters { get; init; } = 2000;
    public double BonusMax { get; init; } = 2;
    // tempo: o mesmo voto envelhece devagar no índice e rápido no ciclo de vida
    public bool UseDecay { get; init; } = true;
    public TimeSpan TrustHalfLife { get; init; } = TimeSpan.FromDays(14);
    public TimeSpan LifecycleHalfLife { get; init; } = TimeSpan.FromDays(3);
    // índice
    public double K { get; init; } = 3;
    public double MediumThreshold { get; init; } = 40;
    public double HighThreshold { get; init; } = 70;
    // ciclo de vida
    public int VotersToLeaveNew { get; init; } = 5;
    public TimeSpan MaxTimeInNew { get; init; } = TimeSpan.FromHours(72);
    public double MinResolvedWeight { get; init; } = 5;
    public double ResolvedRatio { get; init; } = 0.6;
    public TimeSpan ResolvedConfirmationTime { get; init; } = TimeSpan.FromHours(48);

    public static TrustConfiguration B { get; } = new() { Name = "B" };
    public static TrustConfiguration A { get; } = new()
    {
        Name = "A", VerifiedWeight = 1, UseProximity = false, UseDecay = false
    };
    public TrustConfiguration WithVerifiedWeight(double p) => this with { Name = $"{Name}-v{p}", VerifiedWeight = p };
}
```

## Fórmulas

### Peso de perfil
`Citizen → CitizenWeight`, `VerifiedCitizen → VerifiedWeight`, `Administrator → AdministratorWeight`.

### Peso de proximidade `w_d(d)`
Se `!UseProximity` ou `d == null` → `1`.
- `d ≤ 200` → `2`
- `200 < d ≤ 2000` → `2 − (d − 200) / 1800 × (BonusMax − 1)` (com os padrões: `2 − (d − 200)/1800`)
- `d > 2000` → `1`

Nunca menor que 1 (decisão de projeto: penalizar distância só faria o atacante desligar o GPS).

### Fator temporal `w_t`
Se `!UseDecay` → `1`. Senão `2^(−age / halfLife)`, com `age = max(0, now − RegisteredAt)`. A meia-vida depende do cálculo, não do voto: `TrustHalfLife` (14 dias) no índice — "o problema é real?" muda devagar — e `LifecycleHalfLife` (3 dias) no ciclo de vida — "ainda está lá agora?" precisa refletir os votos recentes.

### Peso do voto
`w = w_p × w_d × w_t` (com a meia-vida do cálculo em que o voto entra)

### Índice de confiança (todos os votos, meia-vida da confiança)
`E⁺ = Σ w (Positive)`, `E⁻ = Σ w (Negative)`
`C = 100 × E⁺ / (E⁺ + E⁻ + K)` → arredondar para 1 casa decimal **só na exibição**.
Faixa: `C < 40 → Low`, `40 ≤ C < 70 → Medium`, `C ≥ 70 → High`.

### Ciclo de vida (todos os votos, meia-vida do ciclo de vida)
`R⁺ = Σ w (Positive)`, `R⁻ = Σ w (Negative)`, `p = R⁻ / (R⁺ + R⁻)` (0 se não há votos).
`voters` = nº de `UserId` distintos entre TODOS os votos.

| De | Para | Condição |
|---|---|---|
| New | Active | `voters ≥ 5` **ou** `now − CreatedAt ≥ 72 h` |
| Active | PossiblyResolved | `R⁻ ≥ 5` e `p ≥ 0,6` → grava `ResolutionConditionSince = now` |
| PossiblyResolved | Active | `p < 0,6` → `ResolutionConditionSince = null` |
| PossiblyResolved | Resolved | `p ≥ 0,6` e `now − ResolutionConditionSince ≥ 48 h` |
| Resolved | — | estado final |

Votos chegam também em Nova (contam no índice e em `voters`). Ao sair de Nova, os "Não" já dados contam no ciclo de vida: um registro falso que quem passa não encontra segue Ativa → PossivelmenteResolvida → Resolvida e sai do mapa.

O mínimo `R⁻ ≥ 5` vale **só para entrar** em PossivelmenteResolvida. Para permanecer, basta a proporção: sem votos novos, R⁺ e R⁻ decaem juntos e `p` não muda, então um problema sem movimento conclui a resolução em 48 h em vez de voltar para Ativa só porque os votos envelheceram.

Avaliar **uma transição por chamada** é suficiente, porque o job roda a cada 15 min; `New → Active → PossiblyResolved` nunca acontece na mesma chamada.

## API pública do motor

```csharp
public static class TrustEngine
{
    public static double RoleWeight(Role role, TrustConfiguration cfg);
    public static double ProximityWeight(double? distanceMeters, TrustConfiguration cfg);
    public static double TimeFactor(TimeSpan age, TimeSpan halfLife, TrustConfiguration cfg);
    public static double VoteWeight(VoteFact vote, DateTimeOffset now, TimeSpan halfLife, TrustConfiguration cfg);
    public static TrustResult CalculateTrust(IReadOnlyCollection<VoteFact> votes, DateTimeOffset now, TrustConfiguration cfg);
    public static IssueState EvaluateLifecycle(IssueState state, IReadOnlyCollection<VoteFact> votes, DateTimeOffset now, TrustConfiguration cfg);
}
public sealed record TrustResult(double Score, TrustBand Band, double PositiveEvidence, double NegativeEvidence);
```

## Casos de teste obrigatórios (xUnit, `tests/UrbanReports.Api.Tests/Trust/Engine/`)

Tolerância `1e-6` salvo indicação. `t0 = 2026-10-01T12:00:00Z`.

| # | Cenário | Esperado |
|---|---|---|
| 1 | `ProximityWeight(150, B)` | 2 |
| 2 | `ProximityWeight(1100, B)` | 1,5 |
| 3 | `ProximityWeight(5000, B)` e `(null, B)` | 1 |
| 4 | `ProximityWeight(50, A)` | 1 |
| 5 | `TimeFactor(14 days, TrustHalfLife, B)` | 0,5 |
| 6 | `TimeFactor(3 days, LifecycleHalfLife, B)` | 0,5 |
| 7 | 15 "sim" de cidadãos a 100 m, todos em `t0`, `now = t0`, B | `C ≈ 90,909` (High) |
| 8 | Mesmo do 7 com `now = t0 + 28 days` | `C ≈ 71,429` (High) |
| 9 | Mesmo do 7 com `now = t0 + 56 days` | `C ≈ 38,462` (Low) |
| 10 | Mesmo do 7 na config A, qualquer `now` | `C ≈ 83,333` |
| 11 | 1 verificado a 100 m, B | `C ≈ 86,957` (High) |
| 12 | 1 verificado a 100 m, `B.WithVerifiedWeight(3)` | `E⁺ = 6`, `C ≈ 66,667` (Medium) |
| 13 | Sem votos | `C = 0`, Low |
| 14 | New com 4 votantes, `now − CreatedAt = 71 h` | continua New |
| 15 | New com 5 votantes distintos | Active |
| 16 | New com 1 votante, `now − CreatedAt = 72 h` | Active |
| 17 | Active, 3 "não" de cidadãos a 100 m (R⁻ = 6), 1 "sim" (R⁺ = 2) | PossiblyResolved, `ResolutionConditionSince = now` |
| 18 | Do 17, `now + 47 h` sem novos votos | continua PossiblyResolved |
| 19 | Do 17, `now + 48 h` sem novos votos | Resolved |
| 20 | PossiblyResolved, chegam 3 "sim" a 100 m (p < 0,6) | Active, `ResolutionConditionSince = null` |
| 21 | Mesmo usuário com 2 votos na lista | **não pode acontecer** — a API garante unicidade; o motor pode assumir isso, mas o teste documenta |
| 22 | New com 5 "não" de cidadãos a 100 m | Low; Active → PossiblyResolved na avaliação seguinte → Resolved 48 h depois |

Se algum caso parecer contraintuitivo, **não altere a regra para fazer o teste passar**: registre e discuta com os autores, porque a regra está no texto do TCC.
