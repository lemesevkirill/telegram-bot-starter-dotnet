# ITERATION 03 — Telegram Response Sending

## Goal

Extend the worker pipeline so that processed jobs send responses back to Telegram.

After this iteration the system must support the following full pipeline:

Telegram → Webhook → Jobs table → Worker → Telegram response

Webhook behavior remains unchanged:

- webhook only stores jobs
- webhook must not send Telegram messages
- webhook must return immediately

All Telegram responses must be sent from the worker.

---

## Scope of Iteration 03

This iteration introduces:

- TelegramBotClient lifecycle management
- Telegram API integration
- UpdatePayload parsing using Telegram.Bot types
- Typing indicator support
- Sending text messages to users
- UpdateId deduplication

This iteration still does NOT introduce:

- AI integration
- command routing
- business logic layers
- complex Telegram interaction patterns

The goal is to demonstrate correct integration between the worker and the Telegram API.

All worker safety rules and invariants introduced in Iteration 02 remain in effect.

This includes:

- MaxJobAgeMinutes guard
- retry behavior
- allowed state transitions

Iteration 03 only replaces the simulated processing logic with Telegram response sending.

---

## Database Changes

Extend the `Jobs` table.

Add column:

UpdateId (bigint, NOT NULL)

EF Core mapping requirements:

- `UpdateId` must be mapped as required in the EF model.
- EF model configuration must produce `BIGINT NOT NULL` for `UpdateId`.
- EF model configuration must include a unique index on `UpdateId`.

Purpose:

Telegram may resend the same update if webhook responses fail.

To prevent duplicate job processing, UpdateId must be unique.

Create a unique index on:

UpdateId

Webhook deduplication must rely on the database constraint.

Algorithm:

1. Webhook receives `Update`.
2. Webhook attempts to insert the Job with `UpdateId`.
3. If the insert succeeds, webhook must return HTTP 200.
4. If a unique constraint violation occurs, the webhook must ignore the update and return HTTP 200.

This ensures idempotent webhook behavior even when multiple application instances are running.

The webhook must NOT perform a pre-check query for existing `UpdateId`.

---

## Migration Policy

This repository is still pre-production.

Database reset is acceptable.

No migration path or backfill strategy for UpdateId is required.

A new migration adding the column and unique index is sufficient.

Existing data may be dropped for this change.

---

## Webhook Changes

Webhook behavior remains minimal.

New step:

After deserializing `Telegram.Bot.Types.Update`, extract:

- UpdateId

Store it in the Job entity.

Full webhook flow:

1. Validate secret token
2. Read raw JSON request body
3. Deserialize `Telegram.Bot.Types.Update`
4. Extract:
   - UpdateId
   - ChatId
   - UserId
5. Insert Job:
   - UpdateId
   - ChatId
   - UserId
   - UpdatePayload
   - Status = Pending
6. Return HTTP 200

Unique constraint handling rule:

- Webhook must catch `DbUpdateException` caused by PostgreSQL unique constraint violation on `UpdateId`.
- PostgreSQL error code for this case is `23505`.
- If this error occurs, treat the update as duplicate delivery, ignore it, and return HTTP 200.

Webhook must not:

- send Telegram responses
- call external APIs
- execute background logic

The webhook must NOT perform a pre-check query before insert.

Deduplication must rely entirely on the database constraint.

---

## Telegram Client

Register a singleton Telegram client.

Use:

Telegram.Bot.TelegramBotClient

Configuration source:

TelegramOptions.BotToken

The client must be registered through dependency injection.

The client must have singleton lifecycle and be reused across worker tasks.

Do not create a new client per request.

---

## Telegram Sender Service

Introduce a minimal service responsible for sending Telegram messages.

Purpose:

Keep the worker decoupled from TelegramBotClient details.

Responsibilities:

- SendTypingAsync
- SendTextMessageAsync

Expected minimal method signatures:

- `Task SendTypingAsync(long chatId)`
- `Task SendTextMessageAsync(long chatId, string text)`

The service internally uses TelegramBotClient.

Do not introduce complex abstractions.

Keep the interface minimal.

---

## Update Parsing

Worker must parse UpdatePayload using:

Telegram.Bot.Types.Update

Deserialization must use:

System.Text.Json

Do NOT introduce Newtonsoft.Json.

Expected update type for this iteration:

Message updates containing text.

Worker behavior:

If the update does not contain a text message:

mark the job Completed without sending a response.

If payload deserialization fails:

treat it as a processing failure and follow the retry rules defined in Iteration 02.

---

## Worker Processing Behavior

Replace the simulated processing logic from Iteration 02.

The worker pipeline must now perform the following steps:

1. Acquire job
2. Check job age using MaxJobAgeMinutes (from Iteration 02)
3. Parse UpdatePayload
4. Extract message text
5. Send typing indicator
6. Simulate processing delay
7. Send response message
8. Mark job Completed

Example sequence:

SendChatAction (typing)

wait ~2 seconds

SendMessage

The delay simulates future operations such as:

- LLM processing
- TTS generation
- external API calls

---

## Telegram Typing Indicator

Worker should call Telegram API:

sendChatAction

Action type:

Typing

Purpose:

Demonstrate asynchronous response behavior.

Typing indicator should be sent before simulated processing.

---

## Response Message

For this iteration the bot must send a simple text reply.

Example format:

Got your message:

<user text>

This confirms that:

- payload parsing works
- Telegram API integration works
- worker pipeline functions correctly

This response format is only for validating the worker pipeline in this iteration.

Future iterations will replace it with LLM and TTS processing.

---

## Error Handling

Telegram API failures must follow the existing worker retry model.

If Telegram sending fails:

- exception is thrown
- error message stored in LastError
- retry logic follows existing rules

No additional retry policies are required.

Existing worker retry behavior from Iteration 02 remains unchanged.

---

## Delivery Semantics

Outbound Telegram responses in this iteration follow **at-least-once delivery semantics**.

If a worker crashes after sending a Telegram message but before persisting the job status update, the retry mechanism may cause the same response to be sent again.

Duplicate replies are acceptable for this iteration.

---

## Logging

Worker should log the following events:

- Job picked
- Sending typing indicator
- Sending Telegram message
- Job completed
- Job failed

Logs should help verify Telegram integration during development.

---

## Verification

After this iteration the system must support:

1. Telegram sends message
2. Webhook stores job
3. Worker acquires job
4. Worker checks job age
5. Worker parses UpdatePayload
6. Worker sends typing indicator
7. Worker waits approximately 2 seconds
8. Worker sends reply message
9. Job becomes Completed

Additional checks:

- duplicate updates do not create duplicate jobs
- webhook remains fast and non-blocking
- worker retry behavior still works
- multiple worker instances remain safe
- worker concurrency limits are respected

---

## Expected Architecture After Iteration 03

Telegram  
↓  
Webhook  
↓  
Jobs table  
↓  
Worker  
↓  
Telegram API response

---

## Out of Scope

This iteration does NOT include:

- command routing
- LLM integration
- TTS generation
- rich Telegram interactions
- callback queries
- media messages

These features will be introduced in later iterations.
