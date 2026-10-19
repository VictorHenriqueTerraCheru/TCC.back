# Plano de implementação — API

Etapas em ordem. Cada etapa termina com `dotnet build` sem warnings no Domain, `dotnet test` verde e um commit. **Não pule a Etapa 1**: ela é o núcleo do TCC e destrava o Simulator em paralelo com o front.

---

## Etapa 0 — Esqueleto (≈ ½ dia)

- Solution com os 5 projetos da árvore do `CLAUDE.md`; `Directory.Build.props` com `net10.0`, `Nullable`, `ImplicitUsings`, `LangVersion latest`.
- `docker-compose.yml` com `postgis/postgis:16-3.4` (usuário/senha/banco `ocorrencias`).
- `Program.cs` mínimo: OpenAPI + Scalar, ProblemDetails, `JsonStringEnumConverter` camelCase, CORS por configuração (`Cors:Origens`, com `http://localhost:5173` em Development), health checks em `GET /api/v1/saude` (aplicação) e `GET /api/v1/saude/banco` (banco; 503 se fora), anônimos e documentados no OpenAPI (tag `Saude`).
- Garanta que o documento OpenAPI (`/openapi/v1.json`) descreva os enums como string e os campos anuláveis corretamente: o front gera os tipos TypeScript a partir dele.
- `.editorconfig`, `.gitignore`, GitHub Actions rodando `dotnet test`.

**Aceite:** `dotnet run` sobe e `/scalar` abre; CI verde.

## Etapa 1 — Motor de confiança no Domain (≈ 1–2 dias) ⭐

- Implementar tudo de `docs/MOTOR_CONFIANCA.md`: enums, `VotoFato`, `EstadoProblema`, `ConfiguracaoConfianca` (A, B, `ComPesoVerificado`), `MotorConfianca`.
- `Ocorrencias.Domain.Tests` com **todos** os casos da tabela, mais testes de propriedade simples: C ∈ [0, 100); C monotônico em E⁺; A independe de `agora`.

**Aceite:** 100% dos casos da tabela passam; o Domain não referencia nenhum pacote.

## Etapa 2 — Dados e autenticação (≈ 1–2 dias)

- `AppDbContext` com `UseNetTopologySuite()` e snake_case. Entidades: `Usuario : IdentityUser<Guid>` (+ `Nome`, `Perfil`, `CriadoEm`, `VerificadoEm`), `SolicitacaoVerificacao`, `Categoria` (seed dos 8 itens do `API.md`), `Problema` (`Point Localizacao` geography 4326, índice GIST), `Voto` (índice único `problema_id, usuario_id, tipo`), `Incidente` (`Point AreaAproximada` + GIST, índice em `registrado_em`).
- Renomear as tabelas do Identity (`usuarios`, `usuarios_roles` etc.) — não usamos roles do Identity; perfil é coluna.
- `JwtTokenService` (HS256, 7 dias, claims `sub`, `email`, `nome`, `perfil`). Policies `Autenticado` e `Administrador`.
- Features `Auth/Cadastrar`, `Auth/Entrar`, `Usuarios/ObterMe`, `Usuarios/AtualizarMe`, `Categorias/Listar`.
- Seed de desenvolvimento: 1 admin (`admin@tcc.local`), 1 verificado, 3 cidadãos.

**Aceite:** migration aplica num banco limpo; cadastro → login → `/me` funcionam; testes de integração com Testcontainers cobrindo `409 email-ja-cadastrado` e `401`.

## Etapa 3 — Geocodificação (≈ ½ dia)

- `Feature/Geocodificacao`: `NominatimClient` (HttpClient tipado, `https://nominatim.openstreetmap.org/reverse?format=jsonv2&lat&lon&zoom=18&addressdetails=1`), `ServicoGeocodificacao` com `IMemoryCache` (chave = lat/lng em 4 casas, TTL 7 dias) e limitador de 1 requisição/s ao Nominatim.
- Montagem de `endereco` e `rotuloArea` conforme `API.md` (mapear `road`, `house_number`, `suburb`/`neighbourhood`, `city`/`town`, `ISO3166-2-lvl4` → UF).
- Feature `Geocodificacao/Reversa` com rate limit de 30/min por IP (`Microsoft.AspNetCore.RateLimiting`).
- Nos testes, substitua o cliente por um fake (nunca chame o Nominatim real em teste).

**Aceite:** duas chamadas seguidas ao mesmo ponto geram uma única requisição ao Nominatim; com o Nominatim fora do ar, a resposta vem com `disponivel: false`.

## Etapa 4 — Problemas e votos (≈ 2–3 dias)

- `Problemas/Registrar` (valida categoria do tipo problema, descrição ≤ 280, status `Nova`; `endereco_aproximado` vem do `IServicoGeocodificacao`).
- `Problemas/BuscarDuplicatas` (`ST_DWithin` 50 m, mesma categoria, status ≠ Resolvida, ordenado por `ST_Distance`).
- `Problemas/ListarNaArea` (bbox com `&&`/`ST_MakeEnvelope`, filtros, limite 500, sem Resolvida).
- `Problemas/ObterDetalhe` (contagens de pessoas por valor; `naoUltimas24h`; `avaliacao` em Nova; `meuVoto`/`souAutor` se autenticado).
- `Problemas/ListarRecentes` (raio, paginação).
- `Votos/Votar` (voto único "Ainda está lá?"): regras de `API.md` → calcula distância no banco → UPSERT com `perfil_no_momento`/`distancia_metros` → carrega votos → `MotorConfianca.CalcularConfianca` + `AvaliarCicloDeVida` → salva `confianca`, `status`, `status_alterado_em`, `condicao_resolucao_desde` → responde o detalhe. Tudo numa transação.
- `Feature/Confianca/Jobs/CicloDeVidaJob`: a cada 15 min, para problemas não resolvidos, recalcula confiança e ciclo de vida (o decaimento muda o resultado sem votos novos).

**Aceite:** testes de integração: duplicata a 30 m é achada e a 60 m não; autor não vota; voto em Nova é aceito; 5 votantes tiram de Nova; o job leva um problema "possivelmente resolvido" a "resolvido" após 48 h (com `TimeProvider` falso).

## Etapa 5 — Incidentes (≈ 1 dia)

- `Incidentes/Registrar` (janela de 24 h, arredondamento em 3 casas, limite de 10 por 24 h → 429, `rotulo_area` calculado pela geocodificação do ponto arredondado e guardado para o histórico).
- `Incidentes/ObterCalor` (agrupa por ponto arredondado, janela em dias, bbox).
- `Incidentes/ObterArea` (raio 400 m, contagens de 7 e 30 dias por categoria, nível: < 5 baixa, 5–14 média, ≥ 15 alta, pelo total de 7 dias).

**Aceite:** nenhuma resposta contém `autorId`; teste confirma que o ponto gravado tem 3 casas decimais.

## Etapa 6 — Verificação, admin e histórico (≈ 1 dia)

- `Verificacao/Solicitar` (30–500 caracteres, uma pendente por vez, recusa se já verificado).
- `Admin/ListarVerificacoes`, `Admin/Aprovar` (perfil → `CidadaoVerificado`, `VerificadoEm`), `Admin/Recusar` (motivo obrigatório).
- `Usuarios/ObterHistorico` (união de registros de problema, incidentes e votos; filtro tudo/registros/votos).

**Aceite:** um cidadão aprovado passa a ter votos com peso 10 nos votos **seguintes** (os anteriores mantêm `perfil_no_momento = cidadao`).

## Etapa 7 — Simulator (≈ 2–3 dias) ⭐

Console que referencia **só** o `Ocorrencias.Domain`. Roda em memória, sem banco.

**Modelo do cenário base** (parâmetros em `cenarios/base.json`, todos ajustáveis):
- Área: retângulo de ~10 × 6 km na Zona Sul do Rio; usuários com um "centro de circulação" sorteado e raio de circulação de 1,5 km.
- 30 dias em passos de 1 h. 200 problemas criados ao longo dos primeiros 20 dias; 70% verdadeiros, 30% falsos.
- 1 000 honestos (95% cidadãos, 5% verificados). Em cada passo, cada usuário "passa" por cada problema dentro do seu raio com probabilidade `p_passagem` (calibrar para ~2 passagens/dia por problema em área densa). Ao passar a ≤ 500 m, a pessoa responde "Ainda está lá?" (voto único): verdadeiro e não resolvido → "sim" com 30%; falso → "não" com 20%; verdadeiro e já resolvido → "não" com 30%. O autor não vota. 5% dos votos honestos invertem o valor (ruído). Distância do voto = distância real da passagem.
- Resolução real dos verdadeiros: exponencial com média de 10 dias.
- Ataque: em um problema falso, grupo de `k` contas (k = 1…30) responde "sim" ao longo de 6 h. Variantes: **S1** sem localização; **S2** distância 0–50 m (GPS falsificado); **S3** = S2 com 1 conta verificada.
- 30 sementes por (configuração × variante × k). `Random` com semente explícita — determinístico (RNF06).

**Métricas** (exportar CSV bruto + resumo por configuração):
- FP = fração de falsos com faixa Alta, medida uma vez por dia;
- FN = fração de verdadeiros não resolvidos e fora de Nova com faixa Baixa, medida uma vez por dia;
- tempo (h) do fim do ataque até o falso cair para Baixa (censurado em 30 dias);
- menor `k` que leva o falso a Alta em ≥ 50% das sementes;
- tempo (h) da resolução real até o status Resolvida.

**CLI:** `--config A,B,B-v3,B-v5 --variantes S1,S2,S3 --sementes 30 --saida resultados/ --cenario cenarios/base.json`.

**Aceite:** mesma semente → CSV idêntico byte a byte; uma execução completa termina em poucos minutos; README do Simulator explicando cada coluna do CSV.

## Etapa 8 — Deploy e entrega (≈ 1 dia)

- Banco PostgreSQL + PostGIS no Railway (template de PostGIS ou imagem `postgis/postgis:16-3.4` com volume); a própria API aplica as migrations e cria o administrador inicial ao subir em `Production`. Detalhes de conexão no `CLAUDE.md` ("Banco em produção").
- API em container (Dockerfile multi-stage) no Railway, no mesmo projeto do banco (rede privada). A API precisa de HTTPS (o front em HTTPS não pode chamar API em HTTP); o domínio público do Railway já serve HTTPS.
- `Cors__Origens__0` apontando para o domínio publicado do front; `Nominatim__Email` com um e-mail de contato real.
- Exportar diagramas: `docs/diagramas/*.puml` → PNG (PlantUML) para o TCC.

**Aceite:** pelo navegador do celular, a aplicação publicada cria conta, registra, vota e vê o mapa contra a API publicada.
