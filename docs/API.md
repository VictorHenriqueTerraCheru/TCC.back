# Contrato da API — Ocorrências Urbanas

Contrato único entre a API (.NET) e o front (React + TypeScript). **Qualquer mudança aqui precisa ser replicada nos dois repositórios.**

## Convenções

- Base URL: `/api/v1`
- JSON em `camelCase`. Datas em ISO 8601 UTC, sempre com `Z` (`2026-10-06T22:19:00Z`, com até 6 casas de fração de segundo). Coordenadas em graus decimais WGS 84 (`lat` ∈ [−90, 90], `lng` ∈ [−180, 180]). Números trafegam sempre como número (`"12"` é recusado).
- Parâmetros de query em `camelCase` (`raioKm`, `minLat`). Listas em query são separadas por vírgula (`grupos=viaPublica,eletrica`); valor desconhecido → `400`.
- Enums trafegam como **string camelCase** (ex.: `"possivelmenteResolvida"`).
- Autenticação: `Authorization: Bearer <jwt>`. Endpoints marcados com 🔓 aceitam visitante (sem token).
- Erros: ProblemDetails (RFC 9457). Validação → `400` com `errors: { campo: [mensagens] }` (campos em camelCase, mensagens em português). Regra de negócio violada → `409`, `422` ou `429` com `type` estável (ver tabela no fim). Sem `type` estável: `401` (sem token, token inválido ou expirado, credenciais erradas), `403` (rota de admin sem perfil `administrador`) e `404` (id inexistente).
- CORS: a API só aceita as origens configuradas em `Cors:Origens` (em dev, `http://localhost:5173`).
- Tipos do front: gerados a partir do OpenAPI da API (`/openapi/v1.json`) com `openapi-typescript`. Este arquivo continua sendo a referência humana; o OpenAPI, a de máquina.
- Paginação: `?pagina=1&tamanho=20` → resposta `{ itens: [...], pagina, tamanho, total }`. `pagina` ∈ [1, 10 000]; `tamanho` ∈ [1, 100], padrão 20.

## Enums

| Enum | Valores |
|---|---|
| `Perfil` | `cidadao`, `cidadaoVerificado`, `administrador` |
| `TipoOcorrencia` | `problema`, `incidente` |
| `StatusProblema` | `nova`, `ativa`, `possivelmenteResolvida`, `resolvida` |
| `FaixaConfianca` | `baixa`, `media`, `alta` |
| `ValorExistencia` | `confirma`, `naoVi` |
| `ValorPersistencia` | `continua`, `resolvido` |
| `TipoVinculo` | `moradorRegiao`, `trabalhaRegiao`, `servidorPublico`, `outro` |
| `StatusVerificacao` | `pendente`, `aprovada`, `recusada` |
| `NivelConcentracao` | `baixa`, `media`, `alta` |

## Auth

### `POST /auth/cadastro` 🔓
```json
{ "nome": "Mariana Souza", "email": "mariana@email.com", "senha": "********", "aceitouTermos": true }
```
Senha ≥ 8 caracteres; `aceitouTermos` precisa ser `true`. → `201`
```json
{ "token": "<jwt>", "expiraEm": "2026-10-13T22:19:00Z", "usuario": { "id": "uuid", "nome": "Mariana Souza", "email": "mariana@email.com", "perfil": "cidadao" } }
```
`409 email-ja-cadastrado`.

### `POST /auth/login` 🔓
```json
{ "email": "mariana@email.com", "senha": "********" }
```
→ `200` mesmo corpo do cadastro. `401` credenciais inválidas. JWT válido por 7 dias (sem refresh token — escopo de TCC).

O token carrega `sub`, `email`, `nome` e `perfil` **do momento do login**. Depois de uma aprovação de verificação, o perfil atual vem de `GET /me`: a API já pesa os votos pelo perfil do banco, mas a claim só muda num novo login. Use `GET /me` para decidir o que mostrar.

## Usuário

### `GET /me`
```json
{
  "id": "uuid", "nome": "Mariana Souza", "email": "mariana@email.com",
  "perfil": "cidadaoVerificado", "criadoEm": "2026-03-02T12:00:00Z", "verificadoEm": "2026-09-25T15:00:00Z",
  "totalRegistros": 18, "totalVotos": 142,
  "verificacao": { "status": "aprovada", "tipoVinculo": "moradorRegiao", "motivoRecusa": null, "criadaEm": "..." }
}
```
`verificacao` é a solicitação mais recente ou `null`. `totalRegistros` = problemas + incidentes registrados.

### `PATCH /me`
```json
{ "nome": "Mariana S." }
```
`nome` com 1 a 120 caracteres (espaços nas pontas são removidos). → `200` com o mesmo corpo de `GET /me`.

### `GET /me/historico?filtro=tudo|registros|votos&pagina&tamanho`
Itens em ordem decrescente de data:
```json
{
  "tipo": "registroProblema | registroIncidente | votoExistencia | votoPersistencia",
  "ocorrenciaId": "uuid",
  "categoria": { "id": 1, "nome": "Buraco na via", "grupo": "viaPublica", "icone": "buraco" },
  "endereco": "Rua Voluntários da Pátria, 170",
  "valor": "confirma | naoVi | continua | resolvido | null",
  "statusProblema": "nova | ativa | ... | null",
  "data": "2026-10-06T09:41:00Z"
}
```
Para `registroIncidente`, `endereco` é o rótulo da área (ex.: "Praia de Botafogo, Botafogo") — ou `null`, se a geocodificação estava indisponível no registro — e `ocorrenciaId` é `null` (o incidente não é navegável). `data` é o instante do registro (problema/incidente) ou do voto. Os votos aparecem pelo valor atual (votar de novo substitui). Itens no mesmo instante têm ordem estável entre páginas.

## Categorias

### `GET /categorias` 🔓
```json
[ { "id": 1, "nome": "Buraco na via", "grupo": "viaPublica", "tipo": "problema", "icone": "buraco" } ]
```
Seed fixo:

| id | nome | grupo | tipo | icone |
|---|---|---|---|---|
| 1 | Buraco na via | viaPublica | problema | buraco |
| 2 | Calçada danificada | viaPublica | problema | calcada |
| 3 | Poste apagado | eletrica | problema | poste |
| 4 | Fio caído | eletrica | problema | fio |
| 5 | Descarte irregular | limpeza | problema | descarte |
| 6 | Lixo acumulado | limpeza | problema | lixo |
| 7 | Roubo | seguranca | incidente | roubo |
| 8 | Acidente de trânsito | transito | incidente | acidente |

## Problemas

### `GET /problemas?minLat&minLng&maxLat&maxLng&grupos=viaPublica,eletrica&status=nova,ativa,possivelmenteResolvida` 🔓
Marcadores da área visível. Nunca retorna `resolvida` (pedir só `status=resolvida` devolve `[]`). Limite de 500 itens (os de maior confiança primeiro; `nova` por último). O retângulo é recortado em lat/lng (planar). Exige `minLat < maxLat` e `minLng < maxLng` dentro dos limites de coordenada: **o front precisa limitar o `getBounds()` do Leaflet**, que passa de ±180 ao afastar o zoom ou cruzar o antimeridiano.
```json
[ { "id": "uuid", "categoriaId": 1, "lat": -22.9519, "lng": -43.1866, "status": "ativa", "confianca": 82.0, "faixa": "alta" } ]
```
Em `nova`, `confianca` e `faixa` vêm `null`.

### `GET /problemas/{id}` 🔓
```json
{
  "id": "uuid",
  "categoria": { "id": 1, "nome": "Buraco na via", "grupo": "viaPublica", "icone": "buraco" },
  "descricao": "Buraco grande perto da faixa de pedestres...",
  "lat": -22.9519, "lng": -43.1866,
  "endereco": "Rua Voluntários da Pátria, 190 — Botafogo",
  "criadoEm": "2026-10-04T07:52:00Z",
  "status": "ativa",
  "confianca": 82.0, "faixa": "alta",
  "avaliacao": { "votantes": 5, "necessarios": 5, "encerraEm": "2026-10-07T07:52:00Z" },
  "existencia": { "confirmacoes": 15, "naoVi": 1 },
  "persistencia": { "continua": 9, "resolvido": 1, "resolvidoUltimas24h": 1 },
  "meusVotos": { "existencia": "confirma", "persistencia": null },
  "souAutor": false
}
```
- Contadores são **contagens de pessoas** (não pesos) — é o que a tela mostra.
- `avaliacao` só vem preenchido em `nova`.
- `meusVotos`/`souAutor` só com token; para visitante vêm `null`/`false`.
- `confianca` vem com 1 casa decimal; `faixa` é calculada sobre o valor gravado (2 casas). Em `nova`, ambos `null`.
- `404` se o id não existe.
- A distância "você está a X m" é calculada **no front** com a posição atual do navegador.

### `GET /problemas/duplicatas?categoriaId&lat&lng`
Problemas da mesma categoria, não resolvidos, a até 50 m. Ordenados por distância (até 20). Exige token (o fluxo de registro já exige login). `faixa` é `null` para problemas em `nova`.
```json
[ { "id": "uuid", "categoria": {...}, "endereco": "...", "criadoEm": "...", "distanciaMetros": 30, "faixa": "alta", "confirmacoes": 15 } ]
```

### `POST /problemas`
```json
{ "categoriaId": 1, "lat": -22.9521, "lng": -43.1868, "descricao": "opcional, até 280" }
```
O endereço **não** vem do cliente: a API o obtém pela geocodificação reversa (normalmente acerto de cache, porque o front acabou de consultar o mesmo ponto). → `201` com o detalhe (`GET /problemas/{id}`) e `Location`. `422 categoria-nao-e-problema`; categoria inexistente → `400` em `errors.categoriaId`.

### `GET /problemas/recentes?lat&lng&raioKm=10&grupos&pagina&tamanho` 🔓
Problemas não resolvidos no raio, mais recentes primeiro. `raioKm` ∈ (0, 50], padrão 10.
```json
{ "itens": [ { "id": "uuid", "categoria": {...}, "endereco": "...", "criadoEm": "...", "status": "ativa", "faixa": "alta" } ], "pagina": 1, "tamanho": 20, "total": 37 }
```

## Votos

### `PUT /problemas/{id}/votos/existencia`
```json
{ "valor": "confirma", "lat": -22.9520, "lng": -43.1867 }
```
`lat`/`lng` opcionais, mas **juntos ou nenhum** (só um deles → `400`; sem permissão de localização → peso de proximidade 1). → `200` com o detalhe atualizado. `404` se o problema não existe. O autor pode votar persistência.
Erros: `422 autor-nao-vota-existencia`, `422 problema-resolvido`.

### `PUT /problemas/{id}/votos/persistencia`
```json
{ "valor": "resolvido", "lat": -22.9520, "lng": -43.1867 }
```
→ `200` com o detalhe atualizado. Erros: `422 persistencia-indisponivel-em-nova`, `422 problema-resolvido`.

> A API calcula a distância no PostGIS e **descarta** `lat`/`lng`; grava só `perfilNoMomento` e `distanciaMetros`.

## Incidentes

### `POST /incidentes`
```json
{ "categoriaId": 7, "lat": -22.9465, "lng": -43.1820, "ocorridoEm": "2026-09-26T08:30:00Z" }
```
`ocorridoEm` entre agora − 24 h e agora, **sem tolerância**: um horário alguns segundos no futuro (relógio do celular adiantado) é recusado. No front, "Agora" deve enviar o instante atual menos 1 minuto, e "Outro horário" deve parar em 23 h 45 atrás. A API arredonda `lat`/`lng` para 3 casas decimais antes de gravar e guarda o `rotuloArea` da geocodificação do ponto arredondado. → `201` com o resumo da área (`GET /incidentes/area`).
Erros: `422 categoria-nao-e-incidente`, `422 horario-fora-da-janela`, `429 limite-diario-incidentes` (janela móvel de 24 h por conta).

### `GET /incidentes/calor?minLat&minLng&maxLat&maxLng&dias=7&grupos=seguranca,transito` 🔓
Células agregadas (o ponto já é arredondado na gravação). `dias` ∈ [1, 30], por `ocorrido_em`. Mesmas regras de retângulo de `GET /problemas`.
```json
[ { "lat": -22.947, "lng": -43.182, "quantidade": 4 } ]
```

### `GET /incidentes/area?lat&lng` 🔓
Raio fixo de 400 m.
```json
{
  "raioMetros": 400, "atualizadoEm": "2026-10-06T09:30:00Z",
  "nivel": "alta",
  "contagens": [ { "categoriaId": 7, "nome": "Roubo", "dias7": 12, "dias30": 41 }, { "categoriaId": 8, "nome": "Acidente de trânsito", "dias7": 3, "dias30": 11 } ]
}
```
O título da área é o `rotuloArea` de `GET /geocodificacao/reversa` para o mesmo ponto; o front faz as duas chamadas em paralelo. `contagens` traz **todas** as categorias de incidente (inclusive zeradas), em ordem de id; janelas por `ocorrido_em`.

## Geocodificação

### `GET /geocodificacao/reversa?lat&lng` 🔓
Converte coordenadas em endereço usando o Nominatim (OpenStreetMap), **sempre pela API**. Cache em memória por coordenada arredondada em 4 casas (~11 m), TTL de 7 dias; no máximo 1 requisição por segundo ao Nominatim (fila); `User-Agent` identificando a aplicação (e-mail de contato opcional); `accept-language=pt-BR`. Limite por IP: 30 requisições/min → `429 limite-requisicoes` com `Retry-After` (trate como endereço indisponível).
```json
{ "disponivel": true, "endereco": "Rua Voluntários da Pátria, 170 — Botafogo", "logradouro": "Rua Voluntários da Pátria", "numero": "170", "bairro": "Botafogo", "cidade": "Rio de Janeiro", "uf": "RJ", "rotuloArea": "Rua Voluntários da Pátria, Botafogo" }
```
Regras de montagem: `endereco` = `logradouro[, numero] — bairro` (omitindo partes ausentes); `rotuloArea` = `logradouro, bairro` ou, sem logradouro, `bairro, cidade`.
Se o Nominatim falhar e não houver cache: `200` com `disponivel: false` e `endereco = "Próximo a -22.95210, -43.18680"`. A API **nunca** falha um registro por causa da geocodificação.

## Verificação

### `POST /verificacoes`
```json
{ "tipoVinculo": "moradorRegiao", "justificativa": "30 a 500 caracteres" }
```
`justificativa` com 30 a 500 caracteres após remover espaços nas pontas. → `201`:
```json
{ "id": "uuid", "tipoVinculo": "moradorRegiao", "justificativa": "...", "status": "pendente", "criadaEm": "..." }
```
Erros: `409 verificacao-pendente-existente`, `422 ja-verificado` (perfil `cidadaoVerificado` ou `administrador`). Depois de uma recusa, pode solicitar de novo.

## Admin (perfil `administrador`)

### `GET /admin/verificacoes?status=pendente|aprovada|recusada&pagina&tamanho`
`status` opcional (ausente = todas). Pendentes vêm da mais antiga para a mais nova (fila); as demais, da mais nova para a mais antiga.
```json
{ "itens": [ { "id": "uuid", "usuario": { "id": "uuid", "nome": "Carlos Menezes", "iniciais": "CM" }, "tipoVinculo": "moradorRegiao", "justificativa": "...", "status": "pendente", "criadaEm": "...", "motivoRecusa": null } ], "pagina": 1, "tamanho": 20, "total": 4 }
```

### `POST /admin/verificacoes/{id}/aprovar` → `204`
O solicitante passa a `cidadaoVerificado` (vale para os votos seguintes). `409 verificacao-ja-analisada`; `404` se o id não existe.
### `POST /admin/verificacoes/{id}/recusar`
```json
{ "motivo": "obrigatório, até 500" }
```
→ `204`. `409 verificacao-ja-analisada`; `404` se o id não existe.

## Saúde

Para o host e o monitoramento; o front não precisa chamar.

### `GET /saude` 🔓
```json
{ "status": "saudavel" }
```
### `GET /saude/banco` 🔓
`200` com o mesmo corpo se o banco responde; `503 banco-indisponivel` se não.

## Códigos de erro (`type`)

| type | status | quando |
|---|---|---|
| `email-ja-cadastrado` | 409 | cadastro com e-mail existente |
| `categoria-nao-e-problema` | 422 | POST /problemas com categoria de incidente |
| `categoria-nao-e-incidente` | 422 | POST /incidentes com categoria de problema |
| `autor-nao-vota-existencia` | 422 | autor votando existência no próprio problema |
| `persistencia-indisponivel-em-nova` | 422 | voto de persistência em problema `nova` |
| `problema-resolvido` | 422 | voto em problema `resolvida` |
| `horario-fora-da-janela` | 422 | incidente com `ocorridoEm` fora de [agora − 24 h, agora] |
| `limite-diario-incidentes` | 429 | 11º incidente da conta em 24 h |
| `verificacao-pendente-existente` | 409 | já existe solicitação pendente |
| `ja-verificado` | 422 | usuário já verificado |
| `verificacao-ja-analisada` | 409 | aprovar/recusar solicitação não pendente |
| `limite-requisicoes` | 429 | limite por IP da geocodificação excedido |
| `banco-indisponivel` | 503 | GET /saude/banco sem conexão com o banco |
