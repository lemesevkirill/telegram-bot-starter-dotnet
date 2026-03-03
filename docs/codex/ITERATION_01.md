# ITERATION 01 — Telegram Webhook + EF Core + Persistent Job Storage

## Context

Iteration 00 established:

- .NET 10 solution
- Two projects:
    - BotTemplate.Api
    - BotTemplate.Core
- Working health endpoints
- Docker and Fly configuration

This iteration introduces:

- Telegram webhook endpoint
- EF Core with PostgreSQL
- Persistent Job storage
- Strongly-typed configuration via Options pattern

No background worker yet.
No LLM.
No TTS.
No Telegram responses yet.
No business orchestration layer.

---

## Goal

Implement a webhook endpoint that:

1. Receives Telegram Update via HTTP POST
2. Validates secret token header
3. Deserializes Update
4. Saves a Job entity into PostgreSQL
5. Returns HTTP 200 immediately

The webhook must not:

- Execute long-running logic
- Send Telegram messages
- Contain business logic
- Trigger background processing

---

## Dependencies to Add (BotTemplate.Api)

- Telegram.Bot
- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.Design
- Npgsql.EntityFrameworkCore.PostgreSQL

---

## Configuration (Strongly-Typed Options)

Configuration must use the ASP.NET Core Options pattern.

No direct calls to `Environment.GetEnvironmentVariable`.

### TelegramOptions (BotTemplate.Core)

Namespace: `BotTemplate.Core.Configuration`

Properties:

- BotToken (string, required)
- SecretToken (string, required)

Section name:

"Telegram"

---

### DatabaseOptions (BotTemplate.Core)

Namespace: `BotTemplate.Core.Configuration`

Properties:

- ConnectionString (string, required)

Section name:

"Database"

---

### appsettings.json (BotTemplate.Api)

Structure:

{
  "Telegram": {
    "BotToken": "",
    "SecretToken": ""
  },
  "Database": {
    "ConnectionString": ""
  }
}

Environment overrides must use:

Telegram__BotToken
Telegram__SecretToken
Database__ConnectionString

---

## Database

Single PostgreSQL database.

Register DbContext using AddDbContext.

Connection string must come from DatabaseOptions.

---

## Job Entity (BotTemplate.Core)

Namespace: BotTemplate.Core.Jobs

Fields:

- Id (Guid)
- ChatId (long)
- UserId (long)
- UpdatePayload (string)
- Status (string)
- CreatedAt (DateTime, UTC)

Status allowed values (string constants for now):

- "pending"
- "processing"
- "done"
- "failed"

Important:

- UpdatePayload must store raw JSON string.
- In EF mapping, map UpdatePayload as text column.
- Do not use json/jsonb types for now.

---

## DbContext (BotTemplate.Api)

Create:

AppDbContext

- DbSet<Job> Jobs

Use PostgreSQL provider.
Keep configuration minimal.

---

## Webhook Endpoint

Create:

POST /telegram/webhook

Behavior:

1. Validate header:
   X-Telegram-Bot-Api-Secret-Token

   Must match TelegramOptions.SecretToken.

   If invalid → return 401.

2. Read raw request body as string.

3. Deserialize raw JSON into:
   Telegram.Bot.Types.Update

4. Extract:
   - ChatId
   - UserId

   If missing → return 400.

5. Create new Job:
   - Status = "pending"
   - UpdatePayload = raw JSON
   - CreatedAt = DateTime.UtcNow

6. Save to database.

7. Return HTTP 200.

---

## Constraints

- Do not implement background worker.
- Do not send Telegram responses.
- Do not implement retry logic.
- Do not introduce repository pattern.
- Do not introduce service layer.
- Keep logic minimal inside endpoint.

---

## Acceptance Criteria

- dotnet build succeeds
- Migrations can be created
- Database can be updated
- Jobs table exists
- Valid secret → HTTP 200 and Job inserted
- Invalid secret → HTTP 401
- Missing ChatId/UserId → HTTP 400
- No compiler warnings