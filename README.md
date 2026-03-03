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

### 🚧 Iteration 01 (In Progress)
- Telegram webhook endpoint
- PostgreSQL integration
- Persistent Job storage
- Strongly-typed configuration (Options pattern)

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

/BotTemplate.Core


---

## Configuration

Configuration uses strongly-typed Options pattern.

Environment overrides use double underscore syntax:


Telegram__BotToken=

Telegram__SecretToken=

Database__ConnectionString=


---

## Roadmap

- [x] Iteration 00 — Solution skeleton
- [ ] Iteration 01 — Webhook + EF Core
- [ ] Iteration 02 — Background worker
- [ ] Iteration 03 — Telegram response sending
- [ ] Iteration 04 — LLM integration
- [ ] Iteration 05 — TTS integration
- [ ] Iteration 06 — Observability (OpenTelemetry)

---

## Philosophy

This repository is not a framework.

It is a minimal, production-ready foundation for building reliable Telegram bots with long-running task processing.