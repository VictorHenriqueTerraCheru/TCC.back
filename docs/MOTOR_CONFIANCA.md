# Motor de confiança — especificação normativa

Este arquivo é a **fonte da verdade** do mecanismo de confiança (TCC, Seção 5.2). O projeto `Ocorrencias.Domain` implementa exatamente isto. Se o código e este arquivo divergirem, o código está errado — ou este arquivo precisa ser atualizado *e* o TCC também.

## Princípios

1. **Funções puras.** Nada de `DateTime.Now`, banco, DI ou logging. O instante atual (`agora`) e a configuração entram como parâmetros.
2. **O voto guarda fatos, não pesos.** Cada voto guarda `PerfilNoMomento` e `DistanciaMetros` (nulo = sem localização). Os pesos são derivados na hora do cálculo, a partir da configuração. Assim, os mesmos votos podem ser avaliados nas configurações A e B.
3. **A API e o Simulator chamam as mesmas funções.** É isso que garante que o experimento mede o sistema real.

## Tipos

```csharp
public enum Perfil { Cidadao = 1, CidadaoVerificado = 2, Administrador = 3 }
public enum TipoVoto { Existencia = 1, Persistencia = 2 }
public enum ValorVoto { Positivo = 1, Negativo = 2 } // existência: Confirma/NaoVi · persistência: Continua/Resolvido
public enum StatusProblema { Nova = 1, Ativa = 2, PossivelmenteResolvida = 3, Resolvida = 4 }
public enum FaixaConfianca { Baixa = 1, Media = 2, Alta = 3 }

public sealed record VotoFato(
    Guid UsuarioId, TipoVoto Tipo, ValorVoto Valor,
    Perfil PerfilNoMomento, double? DistanciaMetros, DateTimeOffset RegistradoEm);

public sealed record EstadoProblema(
    StatusProblema Status, DateTimeOffset CriadoEm, DateTimeOffset StatusAlteradoEm,
    DateTimeOffset? CondicaoResolucaoDesde);
```

## Configuração

```csharp
public sealed record ConfiguracaoConfianca
{
    public required string Nome { get; init; }
    // perfil
    public double PesoCidadao { get; init; } = 1;
    public double PesoVerificado { get; init; } = 10;
    public double PesoAdministrador { get; init; } = 1;
    // proximidade
    public bool UsarProximidade { get; init; } = true;
    public double RaioBonusTotalMetros { get; init; } = 200;
    public double RaioBonusZeroMetros { get; init; } = 2000;
    public double BonusMaximo { get; init; } = 2;
    // tempo
    public bool UsarDecaimento { get; init; } = true;
    public TimeSpan MeiaVidaExistencia { get; init; } = TimeSpan.FromDays(14);
    public TimeSpan MeiaVidaPersistencia { get; init; } = TimeSpan.FromDays(3);
    // índice
    public double K { get; init; } = 3;
    public double LimiarMedia { get; init; } = 40;
    public double LimiarAlta { get; init; } = 70;
    // ciclo de vida
    public int VotantesParaSairDeNova { get; init; } = 5;
    public TimeSpan TempoMaximoEmNova { get; init; } = TimeSpan.FromHours(72);
    public double PesoMinimoResolvido { get; init; } = 5;
    public double ProporcaoResolvido { get; init; } = 0.6;
    public TimeSpan TempoConfirmacaoResolvida { get; init; } = TimeSpan.FromHours(48);

    public static ConfiguracaoConfianca B { get; } = new() { Nome = "B" };
    public static ConfiguracaoConfianca A { get; } = new()
    {
        Nome = "A", PesoVerificado = 1, UsarProximidade = false, UsarDecaimento = false
    };
    public ConfiguracaoConfianca ComPesoVerificado(double p) => this with { Nome = $"{Nome}-v{p}", PesoVerificado = p };
}
```

## Fórmulas

### Peso de perfil
`Cidadao → PesoCidadao`, `CidadaoVerificado → PesoVerificado`, `Administrador → PesoAdministrador`.

### Peso de proximidade `w_d(d)`
Se `!UsarProximidade` ou `d == null` → `1`.
- `d ≤ 200` → `2`
- `200 < d ≤ 2000` → `2 − (d − 200) / 1800 × (BonusMaximo − 1)` (com os padrões: `2 − (d − 200)/1800`)
- `d > 2000` → `1`

Nunca menor que 1 (decisão de projeto: penalizar distância só faria o atacante desligar o GPS).

### Fator temporal `w_t`
Se `!UsarDecaimento` → `1`. Senão `2^(−idade / meiaVida)`, com `idade = max(0, agora − RegistradoEm)` e a meia-vida conforme o `Tipo` do voto.

### Peso do voto
`w = w_p × w_d × w_t`

### Índice de confiança (só votos de existência)
`E⁺ = Σ w (Positivo)`, `E⁻ = Σ w (Negativo)`
`C = 100 × E⁺ / (E⁺ + E⁻ + K)` → arredondar para 1 casa decimal **só na exibição**.
Faixa: `C < 40 → Baixa`, `40 ≤ C < 70 → Media`, `C ≥ 70 → Alta`.

### Ciclo de vida
`R⁺ = Σ w (persistência Positivo)`, `R⁻ = Σ w (persistência Negativo)`, `p = R⁻ / (R⁺ + R⁻)` (0 se não há votos).
`votantes = nº de UsuarioId distintos entre TODOS os votos`.

| De | Para | Condição |
|---|---|---|
| Nova | Ativa | `votantes ≥ 5` **ou** `agora − CriadoEm ≥ 72 h` |
| Ativa | PossivelmenteResolvida | `R⁻ ≥ 5` e `p ≥ 0,6` → grava `CondicaoResolucaoDesde = agora` |
| PossivelmenteResolvida | Ativa | `p < 0,6` → `CondicaoResolucaoDesde = null` |
| PossivelmenteResolvida | Resolvida | `p ≥ 0,6` e `agora − CondicaoResolucaoDesde ≥ 48 h` |
| Resolvida | — | estado final |

O mínimo `R⁻ ≥ 5` vale **só para entrar** em PossivelmenteResolvida. Para permanecer, basta a proporção: sem votos novos, R⁺ e R⁻ decaem juntos e `p` não muda, então um problema sem movimento conclui a resolução em 48 h em vez de voltar para Ativa só porque os votos envelheceram.

Avaliar **uma transição por chamada** é suficiente, porque o job roda a cada 15 min; mas `Nova → Ativa → PossivelmenteResolvida` na mesma chamada não deve acontecer (persistência fica fechada em `Nova`).

## API pública do Domain

```csharp
public static class MotorConfianca
{
    public static double PesoPerfil(Perfil perfil, ConfiguracaoConfianca cfg);
    public static double PesoProximidade(double? distanciaMetros, ConfiguracaoConfianca cfg);
    public static double FatorTemporal(TimeSpan idade, TipoVoto tipo, ConfiguracaoConfianca cfg);
    public static double PesoVoto(VotoFato voto, DateTimeOffset agora, ConfiguracaoConfianca cfg);
    public static ResultadoConfianca CalcularConfianca(IReadOnlyCollection<VotoFato> votos, DateTimeOffset agora, ConfiguracaoConfianca cfg);
    public static EstadoProblema AvaliarCicloDeVida(EstadoProblema estado, IReadOnlyCollection<VotoFato> votos, DateTimeOffset agora, ConfiguracaoConfianca cfg);
}
public sealed record ResultadoConfianca(double Indice, FaixaConfianca Faixa, double EPositivo, double ENegativo);
```

## Casos de teste obrigatórios (xUnit, `Ocorrencias.Domain.Tests`)

Tolerância `1e-6` salvo indicação. `t0 = 2026-10-01T12:00:00Z`.

| # | Cenário | Esperado |
|---|---|---|
| 1 | `PesoProximidade(150, B)` | 2 |
| 2 | `PesoProximidade(1100, B)` | 1,5 |
| 3 | `PesoProximidade(5000, B)` e `(null, B)` | 1 |
| 4 | `PesoProximidade(50, A)` | 1 |
| 5 | `FatorTemporal(14 dias, Existencia, B)` | 0,5 |
| 6 | `FatorTemporal(3 dias, Persistencia, B)` | 0,5 |
| 7 | 15 confirmações de cidadãos a 100 m, todas em `t0`, `agora = t0`, B | `C ≈ 90,909` (Alta) |
| 8 | Mesmo do 7 com `agora = t0 + 28 dias` | `C ≈ 71,429` (Alta) |
| 9 | Mesmo do 7 com `agora = t0 + 56 dias` | `C ≈ 38,462` (Baixa) |
| 10 | Mesmo do 7 na config A, qualquer `agora` | `C ≈ 83,333` |
| 11 | 1 verificado a 100 m, B | `C ≈ 86,957` (Alta) |
| 12 | 1 verificado a 100 m, `B.ComPesoVerificado(3)` | `E⁺ = 6`, `C ≈ 66,667` (Media) |
| 13 | Sem votos | `C = 0`, Baixa |
| 14 | Nova com 4 votantes, `agora − CriadoEm = 71 h` | continua Nova |
| 15 | Nova com 5 votantes distintos | Ativa |
| 16 | Nova com 1 votante, `agora − CriadoEm = 72 h` | Ativa |
| 17 | Ativa, 3 "resolvido" de cidadãos a 100 m (R⁻ = 6), 1 "continua" (R⁺ = 2) | PossivelmenteResolvida, `CondicaoResolucaoDesde = agora` |
| 18 | Do 17, `agora + 47 h` sem novos votos | continua PossivelmenteResolvida |
| 19 | Do 17, `agora + 48 h` sem novos votos | Resolvida |
| 20 | PossivelmenteResolvida, chegam 3 "continua" a 100 m (p < 0,6) | Ativa, `CondicaoResolucaoDesde = null` |
| 21 | Mesmo usuário com 2 votos de existência na lista | **não pode acontecer** — a API garante unicidade; o Domain pode assumir isso, mas o teste documenta |

Se algum caso parecer contraintuitivo, **não altere a regra para fazer o teste passar**: registre e discuta com os autores, porque a regra está no texto do TCC.
