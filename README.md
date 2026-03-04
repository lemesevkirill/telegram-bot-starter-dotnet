# Telegram Bot Webhook Starter (Dotnet)

A production-oriented skeleton for building Telegram bots using:

- .NET 10
- ASP.NET Core Minimal API
- PostgreSQL
- EF Core
- Docker
- Fly.io

---

## Current Status

### ✅ Iteration 00
- .NET 10 solution
- Clean two-project structure
- Health endpoints:
  - GET /health/live
  - GET /health/ready
- Docker build working
- Fly configuration present

### ✅ Iteration 01
- Telegram webhook endpoint implemented
- Webhook mapping extracted to dedicated endpoint file (`/Endpoints/TelegramWebhookEndpoint.cs`)
- EF Core + PostgreSQL integration
- Persistent Job storage (`Jobs.Id` uses bigint identity primary key)
- Strongly-typed configuration via Options pattern

---

## Planned Architecture

Target design after planned iterations:

1. Telegram sends Update via webhook.
2. Webhook validates request and stores Job in PostgreSQL.
3. Background worker processes pending Jobs.
4. Worker executes long-running pipeline:
   - Text preprocessing
   - LLM call
   - TTS generation
5. Worker sends result back to Telegram.
6. Job is marked as completed.

No in-memory queues.
No message brokers.
Single PostgreSQL database.

---

## Project Structure
/src

/BotTemplate.Api
  /Endpoints

/BotTemplate.Core


---

## Configuration

Configuration uses strongly-typed Options pattern.

Environment overrides use double underscore syntax:


Telegram__BotToken=

Telegram__WebhookSecret=

Database__ConnectionString=


---

## Development Environment

- Run the API locally via `dotnet run` or your IDE.
- Docker is not required to run the application locally.
- Use Docker only for infrastructure services such as PostgreSQL, Redis, message brokers, or other external dependencies.
- Docker images are intended for deployment environments (production, CI, cloud).

### Run application locally

```bash
dotnet run --project src/BotTemplate.Api
```

### Start PostgreSQL via Docker (optional)

```bash
docker run -d \
  --name bot-postgres \
  -e POSTGRES_USER=bot \
  -e POSTGRES_PASSWORD=bot \
  -e POSTGRES_DB=botdb \
  -p 5432:5432 \
  postgres:16
```

The application connects to PostgreSQL via the configured connection string.

Recommended local development workflow:

1. Start infrastructure services (PostgreSQL) via Docker if needed.
2. Run the API locally via `dotnet run`.
3. Use IDE debugging and hot reload.

Running the API in Docker during development is not required and may slow down debugging.

---

## Roadmap

- [x] Iteration 00 — Solution skeleton
- [x] Iteration 01 — Webhook + EF Core
- [ ] Iteration 02 — Background worker
- [ ] Iteration 03 — Telegram response sending
- [ ] Iteration 04 — LLM integration
- [ ] Iteration 05 — TTS integration
- [ ] Iteration 06 — Observability (OpenTelemetry)

---

## Philosophy

This repository is not a framework.

It is a minimal, production-ready foundation for building reliable Telegram bots with long-running task processing.
