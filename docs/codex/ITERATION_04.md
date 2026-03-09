# ITERATION 04 — JobExecutor (Execution Layer)

## Goal

Introduce an execution layer between the worker and Telegram transport.

The worker must no longer contain message-processing logic.

Instead, the worker will delegate job execution to a dedicated component:

JobExecutor.

This prepares the architecture for future features such as:

- LLM integration
- TTS generation
- web crawling
- multi-step orchestration
- richer Telegram interactions

Iteration 04 is strictly an architectural refactor.

No new product features are introduced.

The observable behavior of the bot must remain identical to Iteration 03.

---

## Target Architecture

Current architecture after Iteration 03:

Telegram  
↓  
Webhook  
↓  
Jobs table  
↓  
Worker  
↓  
TelegramSender  

Target architecture after Iteration 04:

Telegram  
↓  
Webhook  
↓  
Jobs table  
↓  
Worker  
↓  
JobExecutor  
↓  
TelegramSender  

The worker becomes responsible only for:

- job acquisition
- retry/state management
- concurrency limits
- calling JobExecutor

The worker must not implement business logic.

---

## New Component: JobExecutor

Introduce a new service:

JobExecutor

Location suggestion:

BotTemplate.Api/Execution/JobExecutor.cs

Responsibilities:

- parse UpdatePayload
- extract message text
- perform simulated processing
- send Telegram typing indicator
- send Telegram response

The logic currently implemented inside JobWorker must be moved here.

---

## JobExecutor Interface

Create an interface:

IJobExecutor

Example signature:

Task ExecuteAsync(Job job, CancellationToken cancellationToken)

This interface allows future execution engines or pipelines to replace the executor.

---

## JobExecutor Implementation

The default implementation must reproduce the current behavior from Iteration 03.

Execution flow:

1. Deserialize UpdatePayload using System.Text.Json
2. Extract message text
3. If text is null:
   - log
   - return without sending response
4. If text exists:
   - send typing indicator
   - wait ~2 seconds
   - send response message:
     "Got your message:\n\n{text}"

Exceptions must not be swallowed.

They must propagate to the worker so that the retry mechanism continues to work.

---

## Worker Changes

Modify JobWorker:

Replace the current processing block with a call to the executor.

Example flow:

1. Acquire job
2. Check MaxJobAgeMinutes
3. Call executor:

await jobExecutor.ExecuteAsync(job, cancellationToken)

4. If successful → mark Completed
5. If exception → existing retry logic applies

The worker must not:

- deserialize UpdatePayload
- inspect message text
- call TelegramSender directly

All such logic must live in JobExecutor.

---

## Dependency Injection

Register:

IJobExecutor → JobExecutor

Use standard DI registration in Program.cs.

Lifecycle:

Scoped or Singleton is acceptable for this iteration.

---

## Constraints

Iteration 04 must not introduce:

- orchestration frameworks
- command routing
- pipeline engines
- domain layers
- extra abstractions

Only one executor implementation is required.

Keep the design minimal.

---

## Verification

Behavior must remain identical to Iteration 03.

When a Telegram user sends a message:

1. webhook stores job
2. worker acquires job
3. worker calls JobExecutor
4. executor sends typing indicator
5. executor waits ~2 seconds
6. executor sends reply
7. worker marks job Completed

No changes to retry behavior, logging, or job lifecycle.

---

## Expected Outcome

The worker becomes infrastructure-only.

All execution logic lives inside JobExecutor.

This prepares the codebase for future iterations that will introduce:

- LLM processing
- TTS pipelines
- multi-step job orchestration