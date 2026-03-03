# Telegram Bot Webhook Starter (Dotnet)

## Purpose

This repository is a production-ready skeleton for building Telegram bots using .NET.

It demonstrates:

- Webhook-based Telegram bot architecture
- Background processing for long-running tasks
- PostgreSQL persistence
- Clean project structure
- Docker-based deployment
- Fly.io readiness
- Secure environment configuration
- External API integration example (e.g. OpenAI)

This is not a framework.
This is not a full SaaS template.
This is a minimal, production-oriented starter.

---

## Non-Goals

- No microservices
- No Redis
- No distributed systems
- No complex CQRS
- No overengineered DDD
- No message brokers
- No Postgres-as-RabbitMQ experiments (for now)
- No admin panel
- No frontend

---

## Tech Stack

- .NET 10
- ASP.NET Core Minimal API
- PostgreSQL
- EF Core
- Docker
- Fly.io
- OpenTelemetry (basic)
- Telegram.Bot SDK

---

## Architecture Principles

1. Webhook endpoint must respond fast (<50ms).
2. Long-running tasks must not execute inside HTTP request.
3. Background processing must be explicit and controlled.
4. Infrastructure must be environment-configurable.
5. No business logic inside controllers.

---

## Projects Structure

/src

/BotTemplate.Api

/BotTemplate.Core


### BotTemplate.Api
- Minimal API
- Webhook endpoint
- Dependency injection setup
- Background worker registration
- Health checks
- Configuration

### BotTemplate.Core
- Domain models
- Job model
- Background processing logic
- External API client abstraction
- Application services

---

## Environment Variables
TELEGRAM_BOT_TOKEN=
TELEGRAM_SECRET_TOKEN=
DATABASE_URL=
OPENAI_API_KEY=


No secrets must ever be committed.

---

## End-to-End Flow (Demo Scenario)

1. Telegram sends update to webhook
2. Webhook validates secret token
3. Webhook enqueues Job
4. HTTP response returns immediately
5. Background worker processes Job
6. Worker simulates long task (or calls external API)
7. Worker sends message back to user

---

## Iteration Plan

### Iteration 0
- Create solution
- Create two projects
- Add Dockerfile
- Add fly.toml
- Add health endpoints

### Iteration 1
- Implement Telegram webhook endpoint
- Validate secret token
- Log update ID
- Immediate 200 OK

### Iteration 2
- Implement background queue using Channel<T>
- Implement BackgroundService
- Enqueue job from webhook
- Worker logs processing

### Iteration 3
- Add EF Core
- Create Jobs table
- Persist jobs
- Worker processes persisted jobs

### Iteration 4
- Add structured logging
- Add basic OpenTelemetry tracing
- Add /metrics endpoint

---

## Definition of Done

The repository is considered ready when:

- A Telegram bot can receive webhook updates
- A long-running job is processed asynchronously
- PostgreSQL persistence works
- Application runs in Docker
- Application deploys to Fly.io
- No secrets are exposed
- README clearly explains setup and deployment
