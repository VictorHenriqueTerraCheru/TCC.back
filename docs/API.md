# Contrato da API — Ocorrências Urbanas

Contrato único entre a API (.NET) e o front (React + TypeScript). **Qualquer mudança aqui precisa ser replicada nos dois repositórios.**

## Convenções

- Base URL: `/api/v1`
- JSON em `camelCase`. Datas em ISO 8601 UTC, sempre com `Z` (`2026-10-06T22:19:00Z`, com até 6 casas de fração de segundo). Coordenadas em graus decimais WGS 84 (`lat` ∈ [−90, 90], `lng` ∈ [−180, 180]). Números trafegam sempre como número (`"12"` é recusado).
- Parâmetros de query em `camelCase` (`radiusKm`, `minLat`). Listas em query são separadas por vírgula (`groups=viaPublica,eletrica`); valor desconhecido → `400`.
- Enums trafegam como **string camelCase** (ex.: `"possiblyResolved"`).
- Autenticação: `Authorization: Bearer <jwt>`. Endpoints marcados com 🔓 aceitam visitante (sem token).
- Erros: ProblemDetails (RFC 9457). Validação → `400` com `errors: { field: [messages] }` (campos em camelCase, mensagens em português). Regra de negócio violada → `409`, `422` ou `429` com `type` estável (ver tabela no fim). Sem `type` estável: `401` (sem token, token inválido ou expirado, credenciais erradas), `403` (rota de admin sem perfil `administrator`) e `404` (id inexistente).
- CORS: a API só aceita as origens configuradas em `Cors:Origins` (em dev, `http://localhost:5173`).
- Tipos do front: gerados a partir do OpenAPI da API (`/openapi/v1.json`) com `openapi-typescript`. Este arquivo continua sendo a referência humana; o OpenAPI, a de máquina.
- Paginação: `?page=1&pageSize=20` → resposta `{ items: [...], page, pageSize, total }`. `page` ∈ [1, 10 000]; `pageSize` ∈ [1, 100], padrão 20.

## Enums

| Enum | Valores |
|---|---|
| `Role` | `citizen`, `verifiedCitizen`, `administrator` |
| `ReportType` | `issue`, `incident` |
| `IssueStatus` | `new`, `active`, `possiblyResolved`, `resolved` |
| `TrustBand` | `low`, `medium`, `high` |
| `VoteAnswer` | `yes`, `no` (resposta a "Ainda está lá?") |
| `AffiliationType` | `livesInArea`, `worksInArea`, `publicServant`, `other` |
| `VerificationStatus` | `pending`, `approved`, `rejected` |
| `ConcentrationLevel` | `low`, `medium`, `high` |

## Auth

### `POST /auth/register` 🔓
```json
{ "name": "Mariana Souza", "email": "mariana@email.com", "password": "********", "acceptedTerms": true }
```
Senha ≥ 8 caracteres; `acceptedTerms` precisa ser `true`. → `201`
```json
{ "token": "<jwt>", "expiresAt": "2026-10-13T22:19:00Z", "user": { "id": "uuid", "name": "Mariana Souza", "email": "mariana@email.com", "role": "citizen" } }
```
`409 email-already-registered`.

### `POST /auth/login` 🔓
```json
{ "email": "mariana@email.com", "password": "********" }
```
→ `200` mesmo corpo do cadastro. `401` credenciais inválidas. JWT válido por 7 dias (sem refresh token — escopo de TCC).

O token carrega `sub`, `email`, `name` e `role` **do momento do login**. Depois de uma aprovação de verificação, o perfil atual vem de `GET /me`: a API já pesa os votos pelo perfil do banco, mas a claim só muda num novo login. Use `GET /me` para decidir o que mostrar.

## Usuário

### `GET /me`
```json
{
  "id": "uuid", "name": "Mariana Souza", "email": "mariana@email.com",
  "role": "verifiedCitizen", "createdAt": "2026-03-02T12:00:00Z", "verifiedAt": "2026-09-25T15:00:00Z",
  "totalReports": 18, "totalVotes": 142,
  "verification": { "status": "approved", "affiliationType": "livesInArea", "rejectionReason": null, "createdAt": "..." }
}
```
`verification` é a solicitação mais recente ou `null`. `totalReports` = problemas + incidentes registrados.

### `PATCH /me`
```json
{ "name": "Mariana S." }
```
`name` com 1 a 120 caracteres (espaços nas pontas são removidos). → `200` com o mesmo corpo de `GET /me`.

### `GET /me/history?filter=all|reports|votes&page&pageSize`
Itens em ordem decrescente de data:
```json
{
  "type": "issueReport | incidentReport | vote",
  "reportId": "uuid",
  "category": { "id": 1, "name": "Buraco na via", "group": "viaPublica", "icon": "buraco" },
  "address": "Rua Voluntários da Pátria, 170",
  "value": "yes | no | null",
  "issueStatus": "new | active | ... | null",
  "date": "2026-10-06T09:41:00Z"
}
```
Para `incidentReport`, `address` é o rótulo da área (ex.: "Praia de Botafogo, Botafogo") — ou `null`, se a geocodificação estava indisponível no registro — e `reportId` é `null` (o incidente não é navegável). `date` é o instante do registro (problema/incidente) ou do voto. Os votos aparecem pelo valor atual (votar de novo substitui). Itens no mesmo instante têm ordem estável entre páginas.

## Categorias

### `GET /categories` 🔓
Só as categorias **ativas**. Os dados abaixo são o conjunto inicial; o administrador cria, edita e desativa categorias em runtime (ver *Admin*), então o front deve montar filtros e listas a partir desta resposta e de `GET /groups`. `group` é a `key` de um grupo cadastrado; `icon` é o nome de um ícone Material Symbols (`flood`) ou uma chave legada do seed (`buraco`).
```json
[ { "id": 1, "name": "Buraco na via", "group": "viaPublica", "type": "issue", "icon": "buraco" } ]
```
Conjunto inicial (seed da migration):

| id | name | group | type | icon |
|---|---|---|---|---|
| 1 | Buraco na via | viaPublica | issue | buraco |
| 2 | Calçada danificada | viaPublica | issue | calcada |
| 3 | Poste apagado | eletrica | issue | poste |
| 4 | Fio caído | eletrica | issue | fio |
| 5 | Descarte irregular | limpeza | issue | descarte |
| 6 | Lixo acumulado | limpeza | issue | lixo |
| 7 | Roubo | seguranca | incident | roubo |
| 8 | Acidente de trânsito | transito | incident | acidente |

### `GET /groups` 🔓
Grupos de categorias, em ordem de exibição (`sortOrder`, depois `label`). Cada grupo tem um **tipo** só (problema ou incidente) e uma **cor**: um slot da paleta do front (`orange`, `ochre`, `brown`, `red`, `terracotta`, `blue`, `teal`, `purple`, `pink`, `gray`), nunca um hex.
```json
[ { "key": "viaPublica", "label": "Via pública", "type": "issue", "color": "orange", "sortOrder": 1 } ]
```
Conjunto inicial: `viaPublica` (Via pública, `issue`, `orange`), `eletrica` (Elétrica, `issue`, `ochre`), `limpeza` (Limpeza, `issue`, `brown`), `seguranca` (Segurança, `incident`, `red`), `transito` (Trânsito, `incident`, `terracotta`). O filtro `groups=` de `/issues` e `/incidents/heatmap` aceita qualquer `key` cadastrada.

## Problemas

### `GET /issues?minLat&minLng&maxLat&maxLng&groups=viaPublica,eletrica&status=new,active,possiblyResolved` 🔓
Marcadores da área visível. Nunca retorna `resolved` (pedir só `status=resolved` devolve `[]`). Limite de 500 itens (os de maior confiança primeiro; `new` por último). O retângulo é recortado em lat/lng (planar). Exige `minLat < maxLat` e `minLng < maxLng` dentro dos limites de coordenada: **o front precisa limitar o `getBounds()` do Leaflet**, que passa de ±180 ao afastar o zoom ou cruzar o antimeridiano.
```json
[ { "id": "uuid", "categoryId": 1, "lat": -22.9519, "lng": -43.1866, "status": "active", "trustScore": 82.0, "band": "high" } ]
```
Em `new`, `trustScore` e `band` vêm `null`.

### `GET /issues/{id}` 🔓
```json
{
  "id": "uuid",
  "category": { "id": 1, "name": "Buraco na via", "group": "viaPublica", "icon": "buraco" },
  "description": "Buraco grande perto da faixa de pedestres...",
  "lat": -22.9519, "lng": -43.1866,
  "address": "Rua Voluntários da Pátria, 190 — Botafogo",
  "createdAt": "2026-10-04T07:52:00Z",
  "status": "active",
  "trustScore": 82.0, "band": "high",
  "evaluation": { "voters": 5, "required": 5, "closesAt": "2026-10-07T07:52:00Z" },
  "votes": { "yes": 15, "no": 1, "noLast24h": 1 },
  "myVote": "yes",
  "isAuthor": false
}
```
- Contadores são **contagens de pessoas** (não pesos) — é o que a tela mostra.
- `evaluation` só vem preenchido em `new`.
- `votes` conta as respostas a "Ainda está lá?"; `noLast24h` são os "não" registrados nas últimas 24 h.
- `myVote`/`isAuthor` só com token; para visitante vêm `null`/`false`. `myVote` é `null` também para quem ainda não votou.
- `trustScore` vem com 1 casa decimal; `band` é calculada sobre o valor gravado (2 casas). Em `new`, ambos `null`.
- `404` se o id não existe.
- A distância "você está a X m" é calculada **no front** com a posição atual do navegador.

### `GET /issues/duplicates?categoryId&lat&lng`
Problemas da mesma categoria, não resolvidos, a até 50 m. Ordenados por distância (até 20). Exige token (o fluxo de registro já exige login). `band` é `null` para problemas em `new`.
```json
[ { "id": "uuid", "category": {...}, "address": "...", "createdAt": "...", "distanceMeters": 30, "band": "high", "confirmations": 15 } ]
```
`confirmations` = quantas pessoas responderam "sim".

### `POST /issues`
```json
{ "categoryId": 1, "lat": -22.9521, "lng": -43.1868, "description": "opcional, até 280" }
```
O endereço **não** vem do cliente: a API o obtém pela geocodificação reversa (normalmente acerto de cache, porque o front acabou de consultar o mesmo ponto). → `201` com o detalhe (`GET /issues/{id}`) e `Location`. `422 category-not-issue`; categoria inexistente → `400` em `errors.categoryId`.

### `GET /issues/recent?lat&lng&radiusKm=10&groups&page&pageSize` 🔓
Problemas não resolvidos no raio, mais recentes primeiro. `radiusKm` ∈ (0, 50], padrão 10.
```json
{ "items": [ { "id": "uuid", "category": {...}, "address": "...", "createdAt": "...", "status": "active", "band": "high" } ], "page": 1, "pageSize": 20, "total": 37 }
```

## Votos

### `PUT /issues/{id}/vote`
Resposta a "Ainda está lá?". Um voto por usuário e problema; votar de novo substitui. O mesmo voto alimenta o índice de confiança e o ciclo de vida (docs/MOTOR_CONFIANCA.md). Aceito também em `new`.
```json
{ "value": "yes", "lat": -22.9520, "lng": -43.1867 }
```
`value`: `yes` (está lá) ou `no` (foi resolvido ou não foi encontrado). `lat`/`lng` opcionais, mas **juntos ou nenhum** (só um deles → `400`; sem permissão de localização → peso de proximidade 1). → `200` com o detalhe atualizado. `404` se o problema não existe.
Erros: `422 author-cannot-vote`, `422 issue-resolved`.

> A API calcula a distância no PostGIS e **descarta** `lat`/`lng`; grava só `role_at_vote` e `distance_meters`.

## Incidentes

### `POST /incidents`
```json
{ "categoryId": 7, "lat": -22.9465, "lng": -43.1820, "occurredAt": "2026-09-26T08:30:00Z" }
```
`occurredAt` entre agora − 24 h e agora, **sem tolerância**: um horário alguns segundos no futuro (relógio do celular adiantado) é recusado. No front, "Agora" deve enviar o instante atual menos 1 minuto, e "Outro horário" deve parar em 23 h 45 atrás. A API arredonda `lat`/`lng` para 3 casas decimais antes de gravar e guarda o `areaLabel` da geocodificação do ponto arredondado. → `201` com o resumo da área (`GET /incidents/area`).
Erros: `422 category-not-incident`, `422 time-outside-window`, `429 daily-incident-limit` (janela móvel de 24 h por conta).

### `GET /incidents/heatmap?minLat&minLng&maxLat&maxLng&days=7&groups=seguranca,transito` 🔓
Células agregadas (o ponto já é arredondado na gravação). `days` ∈ [1, 30], por `occurred_at`. Mesmas regras de retângulo de `GET /issues`.
```json
[ { "lat": -22.947, "lng": -43.182, "count": 4 } ]
```

### `GET /incidents/area?lat&lng` 🔓
Raio fixo de 400 m.
```json
{
  "radiusMeters": 400, "updatedAt": "2026-10-06T09:30:00Z",
  "level": "high",
  "counts": [ { "categoryId": 7, "name": "Roubo", "days7": 12, "days30": 41 }, { "categoryId": 8, "name": "Acidente de trânsito", "days7": 3, "days30": 11 } ]
}
```
O título da área é o `areaLabel` de `GET /geocoding/reverse` para o mesmo ponto; o front faz as duas chamadas em paralelo. `counts` traz **todas** as categorias de incidente (inclusive zeradas), em ordem de id; janelas por `occurred_at`.

## Geocodificação

### `GET /geocoding/reverse?lat&lng` 🔓
Converte coordenadas em endereço usando o Nominatim (OpenStreetMap), **sempre pela API**. Cache em memória por coordenada arredondada em 4 casas (~11 m), TTL de 7 dias; no máximo 1 requisição por segundo ao Nominatim (fila); `User-Agent` identificando a aplicação (e-mail de contato opcional); `accept-language=pt-BR`. Limite por IP: 30 requisições/min → `429 rate-limited` com `Retry-After` (trate como endereço indisponível).
```json
{ "available": true, "address": "Rua Voluntários da Pátria, 170 — Botafogo", "street": "Rua Voluntários da Pátria", "number": "170", "neighborhood": "Botafogo", "city": "Rio de Janeiro", "state": "RJ", "areaLabel": "Rua Voluntários da Pátria, Botafogo" }
```
Regras de montagem: `address` = `street[, number] — neighborhood` (omitindo partes ausentes); `areaLabel` = `street, neighborhood` ou, sem logradouro, `neighborhood, city`.
Se o Nominatim falhar e não houver cache: `200` com `available: false` e `address = "Próximo a -22.95210, -43.18680"`. A API **nunca** falha um registro por causa da geocodificação.

## Verificação

### `POST /verifications`
```json
{ "affiliationType": "livesInArea", "justification": "30 a 500 caracteres" }
```
`justification` com 30 a 500 caracteres após remover espaços nas pontas. → `201`:
```json
{ "id": "uuid", "affiliationType": "livesInArea", "justification": "...", "status": "pending", "createdAt": "..." }
```
Erros: `409 verification-already-pending`, `422 already-verified` (perfil `verifiedCitizen` ou `administrator`). Depois de uma recusa, pode solicitar de novo.

## Admin (perfil `administrator`)

### `GET /admin/verifications?status=pending|approved|rejected&page&pageSize`
`status` opcional (ausente = todas). Pendentes vêm da mais antiga para a mais nova (fila); as demais, da mais nova para a mais antiga.
```json
{ "items": [ { "id": "uuid", "user": { "id": "uuid", "name": "Carlos Menezes", "initials": "CM" }, "affiliationType": "livesInArea", "justification": "...", "status": "pending", "createdAt": "...", "rejectionReason": null } ], "page": 1, "pageSize": 20, "total": 4 }
```

### `POST /admin/verifications/{id}/approve` → `204`
O solicitante passa a `verifiedCitizen` (vale para os votos seguintes). `409 verification-already-reviewed`; `404` se o id não existe.
### `POST /admin/verifications/{id}/reject`
```json
{ "reason": "obrigatório, até 500" }
```
→ `204`. `409 verification-already-reviewed`; `404` se o id não existe.

### `GET /admin/users?search&role=citizen|verifiedCitizen|administrator&page&pageSize`
Todos os usuários, do cadastro mais novo para o mais antigo. `search` (até 100) procura trecho do nome ou do e-mail, sem diferenciar maiúsculas; `role` filtra; ambos opcionais. Parâmetro inválido → `400`.
```json
{ "items": [ { "id": "uuid", "name": "Mariana Souza", "email": "mariana@email.com", "role": "verifiedCitizen", "createdAt": "...", "verifiedAt": "..." } ], "page": 1, "pageSize": 20, "total": 57 }
```

### `PATCH /admin/users/{id}/role`
```json
{ "role": "citizen | verifiedCitizen | administrator" }
```
→ `204`. Vale para os votos seguintes (o voto antigo mantém o `role_at_vote`). Virar `verifiedCitizen` grava `verifiedAt` (mantém a data se já era verificado); voltar a `citizen` zera. `422 own-role` (o admin não altera o próprio perfil); `404` se o id não existe; perfil inválido → `400`. A claim `role` do token do usuário alterado só muda num novo login (ver *Autenticação*); um admin rebaixado mantém o acesso de admin até o token dele expirar.

### `GET /admin/categories`
Todas as categorias, inclusive as desativadas.
```json
[ { "id": 9, "name": "Rua inundada", "group": "clima", "type": "incident", "icon": "inundacao", "active": true } ]
```

### `POST /admin/categories`
```json
{ "name": "obrigatório, até 60, único (sem diferenciar maiúsculas)", "group": "chave de um grupo existente", "type": "issue | incident", "icon": "minúsculas, números, hífen e sublinhado, até 40 (ex.: flood)" }
```
→ `201` com a categoria (formato do `GET /admin/categories`). O `id` é gerado pela API (maior id + 1). `409 duplicate-name`; grupo inexistente → `400` em `errors.group`; `422 group-type-mismatch` (o grupo é de outro tipo).

### `PUT /admin/categories/{id}`
```json
{ "name": "...", "group": "...", "icon": "..." }
```
→ `200` com a categoria. O `type` não muda depois de criada. `404` se o id não existe; `409 duplicate-name`; `400` em `errors.group` e `422 group-type-mismatch` como na criação.

### `PATCH /admin/categories/{id}/active`
```json
{ "active": false }
```
→ `204`; `404` se o id não existe. Não há `DELETE`. Categoria inativa some de `GET /categories` e da contagem de `GET /incidents/area`, e `POST /issues` / `POST /incidents` com ela devolvem `400` em `errors.categoryId`; problemas e incidentes já registrados continuam exibindo a categoria.

### `POST /admin/groups`
```json
{ "label": "obrigatório, até 40, único (sem diferenciar maiúsculas)", "type": "issue | incident", "color": "um dos slots de GET /groups" }
```
→ `201` com o grupo. A `key` é gerada pela API a partir do rótulo (camelCase sem acento, até 30; "Água e esgoto" → `aguaEEsgoto`; repetida ganha sufixo numérico) e **não muda**. A `sortOrder` vai para o fim. `409 duplicate-group-label`.

### `PUT /admin/groups/{key}`
```json
{ "label": "...", "color": "...", "sortOrder": 3 }
```
→ `200` com o grupo. `type` e `key` não mudam. `404` se a chave não existe; `409 duplicate-group-label`.

### `DELETE /admin/groups/{key}` → `204`
Só apaga grupo sem categorias (ativas ou não): senão `409 group-in-use`. `404` se a chave não existe.

## Saúde

Para o host e o monitoramento; o front não precisa chamar.

### `GET /health` 🔓
```json
{ "status": "healthy" }
```
### `GET /health/database` 🔓
`200` com o mesmo corpo se o banco responde; `503 database-unavailable` se não.

## Códigos de erro (`type`)

| type | status | quando |
|---|---|---|
| `email-already-registered` | 409 | cadastro com e-mail existente |
| `category-not-issue` | 422 | POST /issues com categoria de incidente |
| `category-not-incident` | 422 | POST /incidents com categoria de problema |
| `author-cannot-vote` | 422 | autor votando no próprio problema |
| `issue-resolved` | 422 | voto em problema `resolved` |
| `time-outside-window` | 422 | incidente com `occurredAt` fora de [agora − 24 h, agora] |
| `daily-incident-limit` | 429 | 11º incidente da conta em 24 h |
| `verification-already-pending` | 409 | já existe solicitação pendente |
| `already-verified` | 422 | usuário já verificado |
| `duplicate-name` | 409 | POST/PUT /admin/categories com nome já usado |
| `category-limit-reached` | 409 | POST /admin/categories quando o id (smallint) esgotou |
| `group-type-mismatch` | 422 | categoria em grupo de outro tipo |
| `duplicate-group-label` | 409 | POST/PUT /admin/groups com rótulo já usado |
| `group-in-use` | 409 | DELETE /admin/groups de grupo que tem categorias |
| `verification-already-reviewed` | 409 | aprovar/recusar solicitação não pendente |
| `own-role` | 422 | PATCH /admin/users/{id}/role no próprio usuário |
| `rate-limited` | 429 | limite por IP da geocodificação excedido |
| `database-unavailable` | 503 | GET /health/database sem conexão com o banco |
