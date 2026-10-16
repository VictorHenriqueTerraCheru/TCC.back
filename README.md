# TCC.back — API de Ocorrências Urbanas

Backend (.NET 10, Minimal APIs, PostgreSQL + PostGIS) do TCC "Sistema colaborativo de registro e validação de ocorrências urbanas" — Unilasalle-RJ.

- Visão geral, stack e regras: [`CLAUDE.md`](CLAUDE.md)
- Motor de confiança (normativo): [`docs/MOTOR_CONFIANCA.md`](docs/MOTOR_CONFIANCA.md)
- Contrato HTTP com o front: [`docs/API.md`](docs/API.md)
- Plano de implementação: [`docs/PLAN.md`](docs/PLAN.md)

## Rodando localmente

```bash
dotnet tool restore                         # dotnet-ef (manifesto local)
docker compose up -d db                     # PostGIS na porta 5432
dotnet run --project src/Ocorrencias.Api    # Scalar em http://localhost:5080/scalar
dotnet test                                 # testes de integração sobem um PostGIS via Testcontainers (requer Docker)
```

A geocodificação usa o Nominatim (OpenStreetMap), que **recusa (403) e-mails de exemplo** no `User-Agent`. Em desenvolvimento, configure um e-mail de contato real sem commitá-lo:

```bash
dotnet user-secrets set Nominatim:Email "seu-email@dominio.com" --project src/Ocorrencias.Api
```

Sem isso a API continua funcionando, mas os endereços saem como `Próximo a <lat>, <lng>` (`disponivel: false`).

Em `Development`, a API aplica as migrations e cria usuários de exemplo na primeira execução (senha de todos: `tcc12345`):

| e-mail | perfil |
|---|---|
| `admin@tcc.local` | administrador |
| `verificado@tcc.local` | cidadaoVerificado |
| `cidadao1@tcc.local`, `cidadao2@tcc.local`, `cidadao3@tcc.local` | cidadao |
