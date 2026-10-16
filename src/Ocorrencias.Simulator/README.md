# Ocorrencias.Simulator

Console que compara configurações do motor de confiança (A, B e variantes de peso do verificado) em cenários sintéticos, com e sem ataque coordenado. Referencia **só** o `Ocorrencias.Domain`: os pesos, o índice e o ciclo de vida são calculados pelas mesmas funções que a API usa (`MotorConfianca`). Roda em memória, sem banco, e é **determinístico**: a mesma semente gera CSVs idênticos byte a byte (RNF06).

## Como rodar

```bash
dotnet run -c Release --project src/Ocorrencias.Simulator -- \
  --config A,B,B-v3,B-v5 --variantes S1,S2,S3 --sementes 30 \
  --saida resultados/ --cenario cenarios/base.json
```

| Opção | Padrão | Descrição |
|---|---|---|
| `--config` | `A,B,B-v3,B-v5` | Configurações do motor (`ConfiguracaoConfianca.PorNome`: `A`, `B`, `B-v<peso>`) |
| `--variantes` | `S1,S2,S3` | Variantes de ataque (ver abaixo) |
| `--sementes` | `30` | Quantidade de sementes; usa as sementes `1..N` |
| `--saida` | `resultados/` | Diretório dos CSVs (ignorado pelo git) |
| `--cenario` | valores embutidos | JSON com os parâmetros; `cenarios/base.json` é igual aos padrões do código (um teste garante) |

A execução completa (4 configurações × 3 variantes × k = 1…30 × 30 sementes) leva poucos segundos.

## Modelo do cenário base

Todos os parâmetros estão em `cenarios/base.json` (nomes em camelCase; campo desconhecido é erro, para que um erro de digitação não passe em silêncio).

- **Área:** plano de 10 000 × 6 000 m (~ Zona Sul do Rio). Distâncias são euclidianas em metros.
- **Tempo:** 30 dias em passos de 1 h. O job de ciclo de vida é avaliado ao fim de cada hora (na API, a cada 15 min). Cada voto aceito também reavalia o ciclo de vida, como na API.
- **Usuários honestos:** 1 000 (5% verificados), cada um com um centro de circulação uniforme na área. É **elegível** para um problema quem tem o centro a até 1 500 m dele.
- **Problemas:** 200, criados em horas uniformes nos primeiros 20 dias; 70% verdadeiros. O autor é um elegível sorteado. Um verdadeiro é de fato resolvido após um tempo exponencial com média de 10 dias.
- **Passagens:** a cada hora, cada elegível passa pelo problema com probabilidade `p`. Por padrão `p` é calibrado para `passagensPorDia = 2` no centro da área: `p = 2 / 24 / elegíveis médios` (≈ 0,0007/h com 118 elegíveis). `probabilidadePassagem` fixa `p` diretamente. A distância da passagem é sorteada uniformemente num disco de 500 m.
- **Votos honestos** (ao passar a ≤ 500 m):
  - verdadeiro ainda não resolvido de fato → "confirma" com 30% e, independentemente, "continua" com 15%;
  - verdadeiro já resolvido de fato → "resolvido" com 30%;
  - falso → "não vi" com 20%;
  - 5% dos votos invertem o valor (ruído); o autor não vota existência.
- **Regras da API aplicadas na avaliação:** persistência é recusada em Nova, nenhum voto entra depois de Resolvida, o autor não vota existência e votar de novo substitui o voto (UPSERT por usuário e tipo). A distância é gravada em metros inteiros.

### Ataque

Um problema **falso extra** (o *alvo*) é criado no dia 3. A partir de 1 h após o registro e ao longo de 6 h, `k` contas (k = 1…30) votam "confirma" e, depois, "continua". "Continua" só entra se o alvo já tiver saído de Nova. O alvo também recebe os votos honestos de quem passa por ele, e são eles que derrubam o índice depois do ataque.

| Variante | Distância informada | Perfis |
|---|---|---|
| S1 | nenhuma (sem localização) | k cidadãos |
| S2 | uniforme em 0–50 m (GPS falsificado) | k cidadãos |
| S3 | uniforme em 0–50 m | 1 verificado + (k − 1) cidadãos |

Para cada semente, o mundo honesto é gerado **uma vez** e avaliado em todas as configurações; o ataque muda só o alvo. As comparações entre configurações, variantes e valores de k são, portanto, **pareadas**: os mesmos votos honestos em todas.

## Interpretações adotadas

O plano deixa estes pontos em aberto; estas são as definições usadas:

- **Arredondamento:** o índice é arredondado em 2 casas antes de classificar a faixa, como a API faz ao gravar em `numeric(5,2)`.
- **Faixa exibida:** só existe para problemas em Ativa ou PossivelmenteResolvida, porque em Nova a confiança não aparece para ninguém. FP e "atingir Alta" usam a faixa exibida.
- **FP:** entre os falsos já criados, a fração com faixa Alta exibida, medida no fim de cada dia.
- **FN:** entre os verdadeiros já criados, ainda não resolvidos de fato e em Ativa ou PossivelmenteResolvida, a fração com faixa Baixa. Os marcados Resolvida antes da resolução real aparecem à parte (`verdadeiros_resolvidos_indevidamente`).
- **Recuperação:** horas do fim do ataque até o índice do alvo ficar abaixo de 40 (faixa Baixa calculada, exibida ou não), medido de hora em hora e censurado no fim dos 30 dias.
- **k mínimo:** o menor k em que o alvo atinge Alta exibida (em qualquer momento) em ≥ 50% das sementes.

## Arquivos de saída

CSV com vírgula, ponto decimal, fim de linha `\n` e UTF-8 sem BOM. Campos vazios = sem valor.

### `base_diario.csv`: cenário base, uma linha por (configuração, semente, dia)

| Coluna | Significado |
|---|---|
| `configuracao` | Nome da configuração (`A`, `B`, `B-v3`…) |
| `semente` | Semente da execução |
| `dia` | Dia (1…30); medição no fim do dia |
| `falsos_existentes` | Falsos já criados |
| `falsos_alta` | Desses, quantos com faixa Alta exibida |
| `fp` | `falsos_alta / falsos_existentes` (vazio se não há falsos) |
| `verdadeiros_avaliados` | Verdadeiros não resolvidos de fato, em Ativa ou PossivelmenteResolvida |
| `verdadeiros_baixa` | Desses, quantos com faixa Baixa |
| `fn` | `verdadeiros_baixa / verdadeiros_avaliados` (vazio se não há avaliados) |
| `verdadeiros_resolvidos_indevidamente` | Verdadeiros não resolvidos de fato que o sistema já marcou como Resolvida |

### `resolucao.csv`: uma linha por (configuração, semente, verdadeiro resolvido de fato dentro do horizonte)

| Coluna | Significado |
|---|---|
| `problema` | Índice do problema no cenário |
| `resolucao_real_h` | Hora da resolução real (desde o início da simulação) |
| `resolvida_h` | Primeiro fim de hora com status Resolvida (vazio se não chegou) |
| `tempo_h` | `resolvida_h − resolucao_real_h`; se censurado, `720 − resolucao_real_h`. Pode ser negativo se o sistema resolveu antes da realidade |
| `censurado` | 1 = não chegou a Resolvida até o fim |

### `ataque.csv`: uma linha por (configuração, variante, k, semente)

| Coluna | Significado |
|---|---|
| `variante`, `k` | Variante (S1/S2/S3) e número de contas atacantes |
| `atingiu_alta` | 1 = o alvo teve faixa Alta exibida em algum fim de hora |
| `primeira_alta_h` | Primeiro fim de hora com Alta exibida (vazio se nunca) |
| `status_fim_ataque` | Status do alvo no primeiro fim de hora após o fim do ataque |
| `indice_fim_ataque` | Índice de confiança do alvo nesse momento, com 2 casas como a API grava |
| `recuperacao_h` | Horas do fim do ataque até o índice cair abaixo de 40 (se censurado, até o fim) |
| `censurado` | 1 = não caiu para Baixa até o fim dos 30 dias |

### `resumo_configuracoes.csv`: uma linha por configuração

| Coluna | Significado |
|---|---|
| `sementes` | Sementes agregadas |
| `fp_medio`, `fn_medio` | Média, entre sementes, da média diária de FP/FN. Dias sem denominador e sementes sem nenhum dia medido são ignorados; vazio = nenhuma medição |
| `resolvidos_indevidamente_medio` | Média análoga da fração de verdadeiros marcados Resolvida antes da hora |
| `resolucao_mediana_h` | Mediana de `tempo_h` entre as resoluções não censuradas |
| `resolucao_censurados` | Fração de resoluções censuradas |

### `resumo_ataque.csv`: uma linha por (configuração, variante, k)

| Coluna | Significado |
|---|---|
| `taxa_alta` | Fração das sementes em que o alvo atingiu Alta exibida |
| `recuperacao_mediana_h` | Mediana de `recuperacao_h` (censurados entram com o valor censurado) |
| `recuperacao_censurados` | Fração de sementes censuradas |

### `k_minimo.csv`: uma linha por (configuração, variante)

| Coluna | Significado |
|---|---|
| `k_minimo_alta` | Menor k com `taxa_alta ≥ 0,5`; vazio = nenhum k até `kMaximo` |
