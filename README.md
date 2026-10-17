# TCC.back — API de Ocorrências Urbanas

Backend (.NET 10, Minimal APIs, PostgreSQL + PostGIS) do TCC "Sistema colaborativo de registro e validação de ocorrências urbanas" — Unilasalle-RJ.

- Visão geral, stack e regras: [`CLAUDE.md`](CLAUDE.md)
- Motor de confiança (normativo): [`docs/MOTOR_CONFIANCA.md`](docs/MOTOR_CONFIANCA.md)
- Contrato HTTP com o front: [`docs/API.md`](docs/API.md)
- Plano de implementação: [`docs/PLAN.md`](docs/PLAN.md)

## Rodando localmente

```bash
docker compose up -d db                     # PostGIS na porta 5432
dotnet run --project src/Ocorrencias.Api    # Scalar em http://localhost:5080/scalar
dotnet test
```
