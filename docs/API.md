# Contrato da API — Ocorrências Urbanas

Contrato único entre a API (.NET) e o front (React + TypeScript). **Qualquer mudança aqui precisa ser replicada nos dois repositórios.**

## Convenções

- Base URL: `/api/v1`
- JSON em `camelCase`. Datas em ISO 8601 UTC (`2026-10-06T22:19:00Z`). Coordenadas em graus decimais WGS 84 (`lat`, `lng`).
- Enums trafegam como **string camelCase** (ex.: `"possivelmenteResolvida"`).
- Autenticação: `Authorization: Bearer <jwt>`. Endpoints marcados com 🔓 aceitam visitante (sem token).
- Erros: ProblemDetails (RFC 9457). Validação → `400` com `errors: { campo: [mensagens] }`. Regra de negócio violada → `409` ou `422` com `type` estável (ver tabela no fim).
- CORS: a API só aceita as origens configuradas em `Cors:Origens` (em dev, `http://localhost:5173`).
- Tipos do front: gerados a partir do OpenAPI da API (`/openapi/v1.json`) com `openapi-typescript`. Este arquivo continua sendo a referência humana; o OpenAPI, a de máquina.
- Paginação: `?pagina=1&tamanho=20` → resposta `{ itens: [...], pagina, tamanho, total }`.

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
`verificacao` é a solicitação mais recente ou `null`.

### `PATCH /me`
```json
{ "nome": "Mariana S." }
```

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
Para `registroIncidente`, `endereco` é o rótulo da área (ex.: "Praia de Botafogo, Botafogo") e `ocorrenciaId` é `null` (o incidente não é navegável).

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
Marcadores da área visível. Nunca retorna `resolvida`. Limite de 500 itens (os de maior confiança primeiro).
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
- A distância "você está a X m" é calculada **no front** com a posição atual do navegador.

### `GET /problemas/duplicatas?categoriaId&lat&lng`
Problemas da mesma categoria, não resolvidos, a até 50 m. Ordenados por distância.
```json
[ { "id": "uuid", "categoria": {...}, "endereco": "...", "criadoEm": "...", "distanciaMetros": 30, "faixa": "alta", "confirmacoes": 15 } ]
```

### `POST /problemas`
```json
{ "categoriaId": 1, "lat": -22.9521, "lng": -43.1868, "descricao": "opcional, até 280" }
```
O endereço **não** vem do cliente: a API o obtém pela geocodificação reversa (normalmente acerto de cache, porque o front acabou de consultar o mesmo ponto). → `201` com o detalhe (`GET /problemas/{id}`). `422 categoria-nao-e-problema`.

### `GET /problemas/recentes?lat&lng&raioKm=10&grupos&pagina&tamanho` 🔓
Problemas não resolvidos no raio, mais recentes primeiro.
```json
{ "itens": [ { "id": "uuid", "categoria": {...}, "endereco": "...", "criadoEm": "...", "status": "ativa", "faixa": "alta" } ], "pagina": 1, "tamanho": 20, "total": 37 }
```

## Votos

### `PUT /problemas/{id}/votos/existencia`
```json
{ "valor": "confirma", "lat": -22.9520, "lng": -43.1867 }
```
`lat`/`lng` opcionais (sem permissão de localização → peso de proximidade 1). → `200` com o detalhe atualizado.
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
`ocorridoEm` entre agora − 24 h e agora. A API arredonda `lat`/`lng` para 3 casas decimais antes de gravar e guarda o `rotuloArea` da geocodificação do ponto arredondado. → `201` com o resumo da área (`GET /incidentes/area`).
Erros: `422 categoria-nao-e-incidente`, `422 horario-fora-da-janela`, `429 limite-diario-incidentes`.

### `GET /incidentes/calor?minLat&minLng&maxLat&maxLng&dias=7&grupos=seguranca,transito` 🔓
Células agregadas (o ponto já é arredondado na gravação):
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
O título da área é o `rotuloArea` de `GET /geocodificacao/reversa` para o mesmo ponto; o front faz as duas chamadas em paralelo.

## Geocodificação

### `GET /geocodificacao/reversa?lat&lng` 🔓
Converte coordenadas em endereço usando o Nominatim (OpenStreetMap), **sempre pela API**. Cache em memória por coordenada arredondada em 4 casas (~11 m), TTL de 7 dias; no máximo 1 requisição por segundo ao Nominatim (fila); `User-Agent` identificando a aplicação e e-mail de contato; `accept-language=pt-BR`. Limite por IP: 30 requisições/min → `429`.
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
→ `201`. Erros: `409 verificacao-pendente-existente`, `422 ja-verificado`.

## Admin (perfil `administrador`)

### `GET /admin/verificacoes?status=pendente|aprovada|recusada&pagina&tamanho`
```json
{ "itens": [ { "id": "uuid", "usuario": { "id": "uuid", "nome": "Carlos Menezes", "iniciais": "CM" }, "tipoVinculo": "moradorRegiao", "justificativa": "...", "status": "pendente", "criadaEm": "...", "motivoRecusa": null } ], "pagina": 1, "tamanho": 20, "total": 4 }
```

### `POST /admin/verificacoes/{id}/aprovar` → `204`
### `POST /admin/verificacoes/{id}/recusar`
```json
{ "motivo": "obrigatório, até 500" }
```
→ `204`. `409 verificacao-ja-analisada`.

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
