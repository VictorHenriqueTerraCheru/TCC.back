# Bruno Collection da API de Ocorrências Urbanas Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Versionar uma collection Bruno completa para a API, com cinco logins de seed que substituem um JWT ativo único e permitem alternar a identidade usada nas rotas protegidas.

**Architecture:** A collection será armazenada em arquivos `.bru`, sob `bruno/ocorrencias-urbanas/`, sem qualquer mudança no contrato ou no código da API. O ambiente `Local` fornece URL e credenciais de desenvolvimento; cada request de login chama o mesmo `POST /api/v1/auth/login` e, apenas após receber `200`, salva `tokenAtivo`, `perfilAtivo` e `usuarioAtivo` como variáveis de execução da collection. Todas as requisições protegidas enviam `Bearer {{tokenAtivo}}`, portanto o último login executado é a conta ativa para a sessão corrente.

**Tech Stack:** Bruno (Bru Lang e scripts JavaScript), API .NET 10 existente, Docker Compose/PostGIS local.

**Spec:** `docs/API.md`; `src/Ocorrencias.Api/Infrastructure/Persistence/SeedDesenvolvimento.cs`; `src/Ocorrencias.Api/Modules/`.

## Global Constraints

- Não criar nem alterar endpoint, DTO, política de autorização ou seed da API: a collection consome o contrato existente.
- Versionar somente as credenciais deliberadamente públicas de **Development**: os cinco e-mails de seed e a senha `tcc12345`; não adicionar credenciais de ambiente real.
- O seed só é aplicado quando a tabela de usuários está vazia; o README da collection deve explicar isso e como reiniciar apenas o banco local quando for preciso restaurar o cenário.
- Usar um único JWT ativo, `tokenAtivo`; não guardar token por perfil. Um login bem-sucedido sempre substitui o valor anterior e uma falha de login nunca o altera.
- Manter requests visitantes sem cabeçalho `Authorization`; toda request protegida deve ter `Authorization: Bearer {{tokenAtivo}}` explícito.
- A collection deve cobrir cada combinação método/rota de `docs/API.md`, inclusive `GET /saude` e `GET /saude/banco`.
- Operações mutáveis devem capturar apenas IDs retornados pela API e reutilizá-los por variável; não incluir UUIDs inventados nos caminhos.

## Review Focus

1. Troca de conta: o token anterior não pode continuar sendo enviado depois de executar outro login.
2. Falha de login: `401` não pode apagar nem trocar `tokenAtivo`.
3. Endpoints públicos: não podem herdar bearer token por acidente, pois precisam continuar exercitando o acesso anônimo.
4. Fluxo de verificação: após aprovação, é obrigatório efetuar login novamente para obter a claim `perfil` atualizada no JWT.
5. Estado do banco: aprovar e recusar exigem solicitações distintas e pendentes; a collection não deve tratar uma execução repetida como se fosse idempotente.

---

## Estrutura de arquivos proposta

| Caminho | Responsabilidade |
|---|---|
| `bruno/ocorrencias-urbanas/bruno.json` | Metadados da collection Bruno. |
| `bruno/ocorrencias-urbanas/environments/Local.bru` | `baseUrl`, e-mails de seed e senha exclusiva de Development. |
| `bruno/ocorrencias-urbanas/README.md` | Pré-requisitos, variáveis de execução, troca de conta e fluxos mutáveis. |
| `bruno/ocorrencias-urbanas/00-Auth/*.bru` | Cadastro e cinco requests de login. |
| `bruno/ocorrencias-urbanas/01-Saude/*.bru` | Health checks anônimos. |
| `bruno/ocorrencias-urbanas/02-Categorias/*.bru` | Consulta pública de categorias. |
| `bruno/ocorrencias-urbanas/03-Problemas/*.bru` | Listas e operações de problemas; o POST captura `problemaId`. |
| `bruno/ocorrencias-urbanas/04-Votos/*.bru` | Votos de existência e persistência sobre `{{problemaId}}`. |
| `bruno/ocorrencias-urbanas/05-Incidentes/*.bru` | Registro e consultas de incidentes. |
| `bruno/ocorrencias-urbanas/06-Geocodificacao/*.bru` | Consulta reversa pública. |
| `bruno/ocorrencias-urbanas/07-Usuario/*.bru` | `GET/PATCH /me` e histórico do usuário ativo. |
| `bruno/ocorrencias-urbanas/08-Verificacao/*.bru` | Solicitação de verificação; captura `verificacaoId`. |
| `bruno/ocorrencias-urbanas/09-Admin/*.bru` | Lista, aprovação e recusa de solicitações. |
| `bruno/ocorrencias-urbanas/10-Fluxos-Manuais/README.md` | Sequências ordenadas para troca de conta e recursos mutáveis. |

### Task 1: Criar o esqueleto e o ambiente local

**Files:**
- Create: `bruno/ocorrencias-urbanas/bruno.json`
- Create: `bruno/ocorrencias-urbanas/environments/Local.bru`
- Create: `bruno/ocorrencias-urbanas/README.md`

**Interfaces:**
- Produces: ambiente `Local` com `baseUrl = http://localhost:5080/api/v1`, `senhaSeed = tcc12345` e os cinco e-mails de seed.
- Produces: variáveis de execução `tokenAtivo`, `perfilAtivo`, `usuarioAtivo`, inicialmente ausentes até um login válido.

- [ ] **Step 1: Criar `bruno.json` e o ambiente `Local`**

Definir os e-mails `admin@tcc.local`, `verificado@tcc.local`, `cidadao1@tcc.local`, `cidadao2@tcc.local` e `cidadao3@tcc.local` como variáveis nomeadas, além de `baseUrl` e `senhaSeed`. Não cadastrar URL de produção.

- [ ] **Step 2: Escrever o README operacional da collection**

Documentar `docker compose up -d db` e `dotnet run --project src/Ocorrencias.Api`; registrar que o seed requer banco sem usuários e que `docker compose down -v` remove o banco local antes de recriá-lo. Marcar esse último comando como destrutivo e opcional.

- [ ] **Step 3: Validar que Bruno carrega a collection e reconhece o ambiente Local**

Abrir `bruno/ocorrencias-urbanas/` no Bruno e selecionar `Local`.

Expected: a URL interpolada para requests futuras começa com `http://localhost:5080/api/v1` e nenhuma variável de token vem preenchida por arquivo.

- [ ] **Step 4: Commit**

```bash
git add bruno/ocorrencias-urbanas
git commit -m "feat(bruno): adiciona estrutura da collection local"
```

### Task 2: Implementar autenticação e a troca de identidade

**Files:**
- Create: `bruno/ocorrencias-urbanas/00-Auth/Cadastro.bru`
- Create: `bruno/ocorrencias-urbanas/00-Auth/Login - Administrador.bru`
- Create: `bruno/ocorrencias-urbanas/00-Auth/Login - Cidadao verificado.bru`
- Create: `bruno/ocorrencias-urbanas/00-Auth/Login - Cidadao 1 - Mariana Souza.bru`
- Create: `bruno/ocorrencias-urbanas/00-Auth/Login - Cidadao 2 - Joao Pereira.bru`
- Create: `bruno/ocorrencias-urbanas/00-Auth/Login - Cidadao 3 - Ana Lima.bru`

**Interfaces:**
- Consumes: variáveis do ambiente `Local` da Task 1.
- Produces: após `200`, `tokenAtivo = body.token`, `perfilAtivo = body.usuario.perfil` e `usuarioAtivo = body.usuario.id`.
- Produces: o restante da collection pode usar `{{tokenAtivo}}` como bearer token.

- [ ] **Step 1: Criar a request de cadastro anônimo**

Mapear `POST {{baseUrl}}/auth/cadastro` com o corpo `nome`, `email`, `senha` e `aceitouTermos`. Usar variáveis específicas para a pessoa de cenário, para que o operador possa informar um e-mail ainda não usado; não sobrescrever `tokenAtivo` nessa request.

- [ ] **Step 2: Criar as cinco requests de login para a rota única**

Todas usam `POST {{baseUrl}}/auth/login`, corpo JSON `{ "email": "{{emailDoPerfil}}", "senha": "{{senhaSeed}}" }`, e nomes que revelem usuário e perfil. Os valores esperados de `usuario.perfil` são, respectivamente, `administrador`, `cidadaoVerificado`, `cidadao`, `cidadao` e `cidadao`.

- [ ] **Step 3: Adicionar script pós-resposta comum aos cinco logins**

Assertar status `200` e o perfil esperado; somente então copiar `token`, `usuario.perfil` e `usuario.id` para as variáveis de execução. A request não pode atualizar essas variáveis quando a resposta não for `200`.

- [ ] **Step 4: Criar uma request de login inválido, ou documentar a edição temporária de senha em uma das cinco requests**

Cobrir `401` e verificar que `tokenAtivo` permanece igual ao token obtido no login válido anterior. A versão final deve deixar as cinco requests de login válidas prontas para uso manual.

- [ ] **Step 5: Validar a troca Admin → Mariana → Admin**

Executar, nesta ordem, `Login - Administrador`, `GET /me` (criada na Task 4), `Login - Cidadão 1 - Mariana Souza`, `GET /me`, e novamente o login de administrador.

Expected: cada `GET /me` retorna o usuário do último login e todas as requisições autenticadas subsequentes enviam o mesmo valor corrente de `tokenAtivo`.

- [ ] **Step 6: Commit**

```bash
git add bruno/ocorrencias-urbanas/00-Auth
git commit -m "feat(bruno): adiciona logins por usuario seed"
```

### Task 3: Cobrir saúde, consultas públicas e geocodificação

**Files:**
- Create: `bruno/ocorrencias-urbanas/01-Saude/Aplicacao.bru`
- Create: `bruno/ocorrencias-urbanas/01-Saude/Banco.bru`
- Create: `bruno/ocorrencias-urbanas/02-Categorias/Listar categorias.bru`
- Create: `bruno/ocorrencias-urbanas/03-Problemas/Listar na area.bru`
- Create: `bruno/ocorrencias-urbanas/03-Problemas/Listar recentes.bru`
- Create: `bruno/ocorrencias-urbanas/03-Problemas/Obter detalhe.bru`
- Create: `bruno/ocorrencias-urbanas/05-Incidentes/Mapa de calor.bru`
- Create: `bruno/ocorrencias-urbanas/05-Incidentes/Resumo da area.bru`
- Create: `bruno/ocorrencias-urbanas/06-Geocodificacao/Reversa.bru`

**Interfaces:**
- Consumes: `baseUrl`; `problemaId` é opcional até a Task 4 criá-lo.
- Produces: requests visitantes não possuem bearer e verificam os status descritos em `docs/API.md`.

- [ ] **Step 1: Criar requests de saúde e categorias**

Mapear `GET /saude`, `GET /saude/banco` e `GET /categorias`; testar status `200`, `status: "saudavel"` nos dois health checks e uma lista não vazia em categorias.

- [ ] **Step 2: Criar as consultas públicas de problemas e incidentes**

Usar coordenadas válidas de Botafogo nas queries de retângulo, raio e área; não fixar que a lista terá itens. Para o detalhe, parametrizar o caminho como `/problemas/{{problemaId}}` e informar no nome/descrição que depende do POST de problema.

- [ ] **Step 3: Criar a consulta reversa tolerante ao Nominatim**

Mapear `GET /geocodificacao/reversa` e aceitar resposta `200` tanto com `disponivel: true` quanto `false`, pois falha externa é comportamento previsto pelo contrato.

- [ ] **Step 4: Confirmar que nenhuma request deste grupo usa `Authorization`**

Executar ao menos uma request depois de um login e inspecionar os headers enviados.

Expected: os endpoints marcados como visitantes retornam sem bearer, inclusive quando `tokenAtivo` existe.

- [ ] **Step 5: Commit**

```bash
git add bruno/ocorrencias-urbanas/01-Saude bruno/ocorrencias-urbanas/02-Categorias bruno/ocorrencias-urbanas/03-Problemas bruno/ocorrencias-urbanas/05-Incidentes bruno/ocorrencias-urbanas/06-Geocodificacao
git commit -m "feat(bruno): adiciona consultas publicas da api"
```

### Task 4: Cobrir recursos autenticados e IDs encadeados

**Files:**
- Create: `bruno/ocorrencias-urbanas/03-Problemas/Registrar problema.bru`
- Create: `bruno/ocorrencias-urbanas/03-Problemas/Buscar duplicatas.bru`
- Create: `bruno/ocorrencias-urbanas/04-Votos/Votar existencia.bru`
- Create: `bruno/ocorrencias-urbanas/04-Votos/Votar persistencia.bru`
- Create: `bruno/ocorrencias-urbanas/05-Incidentes/Registrar incidente.bru`
- Create: `bruno/ocorrencias-urbanas/07-Usuario/Obter me.bru`
- Create: `bruno/ocorrencias-urbanas/07-Usuario/Atualizar me.bru`
- Create: `bruno/ocorrencias-urbanas/07-Usuario/Obter historico.bru`

**Interfaces:**
- Consumes: `tokenAtivo` de um dos cinco logins.
- Produces: `problemaId` salvo a partir de `POST /problemas` para detalhe, duplicatas e votos.

- [ ] **Step 1: Configurar o bearer explícito em toda request protegida desta tarefa**

Configurar `Authorization: Bearer {{tokenAtivo}}` em `GET/PATCH /me`, `GET /me/historico`, `POST /problemas`, `GET /problemas/duplicatas`, ambos os votos e `POST /incidentes`.

- [ ] **Step 2: Implementar `POST /problemas` e captura de ID**

Usar `categoriaId: 1` e coordenadas válidas; testar `201` e salvar `body.id` em `problemaId` somente em caso de sucesso.

- [ ] **Step 3: Implementar detalhe, duplicatas e os dois votos com `{{problemaId}}`**

Usar o ID capturado, aplicar os corpos de `docs/API.md` e testar os códigos de sucesso. O README deve orientar: criar o problema como administrador, trocar para outro cidadão e então votar em existência; o autor não pode votar existência no próprio registro.

- [ ] **Step 4: Implementar requests de usuário e incidente**

`GET /me` deve validar que o e-mail/perfil retornado corresponde a `perfilAtivo`; `PATCH /me` deve usar um nome explicitamente de teste; histórico deve aceitar os parâmetros de paginação. O incidente deve enviar `ocorridoEm` dentro das últimas 24 horas e não pressupor que a geocodificação externa esteja disponível.

- [ ] **Step 5: Validar o cenário solicitado de troca de conta**

Executar: login de administrador → registrar problema → login de cidadão comum diferente → obter detalhe → votar existência. Confirmar `201`, `200`, `200` e que o voto não recebe o erro `autor-nao-vota-existencia`.

- [ ] **Step 6: Commit**

```bash
git add bruno/ocorrencias-urbanas/03-Problemas bruno/ocorrencias-urbanas/04-Votos bruno/ocorrencias-urbanas/05-Incidentes bruno/ocorrencias-urbanas/07-Usuario
git commit -m "feat(bruno): adiciona recursos autenticados e votos"
```

### Task 5: Cobrir verificação e administração sem mascarar o estado

**Files:**
- Create: `bruno/ocorrencias-urbanas/08-Verificacao/Solicitar verificacao.bru`
- Create: `bruno/ocorrencias-urbanas/09-Admin/Listar verificacoes.bru`
- Create: `bruno/ocorrencias-urbanas/09-Admin/Aprovar verificacao.bru`
- Create: `bruno/ocorrencias-urbanas/09-Admin/Recusar verificacao.bru`
- Create: `bruno/ocorrencias-urbanas/10-Fluxos-Manuais/README.md`

**Interfaces:**
- Consumes: `tokenAtivo` e `verificacaoId`.
- Produces: `verificacaoId` a partir de `POST /verificacoes`; requests admin usam o mesmo ID apenas enquanto ele está pendente.

- [ ] **Step 1: Implementar a solicitação e salvar `verificacaoId`**

Após login de um cidadão ainda não verificado, enviar `tipoVinculo` e justificativa entre 30 e 500 caracteres, testar `201` e salvar `body.id` como `verificacaoId`.

- [ ] **Step 2: Implementar a listagem e as decisões administrativas**

Requerer login de administrador; mapear `GET /admin/verificacoes`, `POST /admin/verificacoes/{{verificacaoId}}/aprovar` e `POST /admin/verificacoes/{{verificacaoId}}/recusar`, com o corpo `{ "motivo": "..." }` na recusa. Testar `200` na lista e `204` nas decisões.

- [ ] **Step 3: Documentar dois fluxos mutuamente exclusivos**

No README de fluxos, indicar que aprovação e recusa não podem ser executadas sobre a mesma solicitação. Usar `cidadao2` para uma decisão e `cidadao3` para outra, ou reiniciar o banco local antes de repetir o cenário.

- [ ] **Step 4: Validar a renovação da claim após aprovação**

Executar cidadão comum → solicitar → administrador → aprovar → login novamente do mesmo cidadão → `GET /me`.

Expected: `GET /me` exibe `cidadaoVerificado` e o login renovado emite token com esse perfil. Sem o novo login, o JWT anterior permanece com claim `cidadao`.

- [ ] **Step 5: Commit**

```bash
git add bruno/ocorrencias-urbanas/08-Verificacao bruno/ocorrencias-urbanas/09-Admin bruno/ocorrencias-urbanas/10-Fluxos-Manuais
git commit -m "feat(bruno): adiciona verificacao e operacoes administrativas"
```

### Task 6: Auditoria de cobertura e execução manual de aceitação

**Files:**
- Modify: `bruno/ocorrencias-urbanas/README.md`
- Modify: todos os `.bru` criados para ajustar descrições, asserts e variáveis.

**Interfaces:**
- Consumes: todos os requests das Tasks 1–5 e a tabela de rotas de `docs/API.md`.
- Produces: uma collection carregável, com cobertura completa de método/rota e instruções que não prometem execução idempotente de operações mutáveis.

- [ ] **Step 1: Conferir a matriz de cobertura contra `docs/API.md`**

Confirmar as 23 combinações método/rota: cadastro, login, `me` (três operações), categorias, cinco operações de problemas, dois votos, três operações de incidentes, geocodificação, verificação, três operações admin e duas operações de saúde. Manter cinco logins separados além da rota única coberta.

- [ ] **Step 2: Executar checks representativos no Bruno**

Com a API e o banco local em execução, rodar os cinco logins, uma rota pública, `GET /me` após cada troca de conta e o fluxo Admin → criar problema → cidadão → votar. Registrar no README os pré-requisitos e o resultado esperado, não tokens.

- [ ] **Step 3: Validar a collection com a CLI Bruno, se ela estiver instalada**

Run: `bru run bruno/ocorrencias-urbanas --env Local`

Expected: as requests independentes passam; requests de fluxo que exigem um ID pendente são executadas manualmente ou separadas do runner, conforme documentado, para evitar falsa aprovação por estado anterior.

- [ ] **Step 4: Conferir o diff e garantir que a API não foi modificada**

Run: `git diff --check` e `git diff -- src/Ocorrencias.Api docs/API.md`.

Expected: não há whitespace error e não há alteração na API nem no contrato HTTP devido à collection.

- [ ] **Step 5: Commit**

```bash
git add bruno/ocorrencias-urbanas
git commit -m "docs(bruno): documenta fluxos e cobertura da collection"
```

## Self-review

- **Cobertura:** as 23 combinações método/rota do contrato estão contempladas; `POST /auth/login` tem cinco instâncias intencionais, uma por usuário seed.
- **Token e papéis:** o design usa um token ativo único, de modo que a mudança de identidade não depende de o operador editar headers manualmente.
- **Estado:** os fluxos que alteram usuário ou solicitação deixam explícita a não idempotência e evitam usar o mesmo ID para aprovação e recusa.
- **Segurança:** os únicos dados de acesso versionados são os seeds de Development já presentes no repositório; URL de produção, tokens e dados reais ficam fora da collection.
- **Contrato:** não há proposta de endpoint adicional; a collection usa a API tal como documentada.
