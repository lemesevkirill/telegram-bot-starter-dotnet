# ITERATION 02 — Job Worker

## Goal

Introduce a background worker that processes jobs stored in the `Jobs` table.

At the end of this iteration the system must support the following flow:

Telegram → Webhook → Jobs table → Worker → Job processing lifecycle

The webhook remains lightweight and only stores jobs.
Actual processing happens asynchronously in the worker.

---

# Current State

Iteration 01 implemented:

* Telegram webhook endpoint
* Job persistence in PostgreSQL
* EF Core migrations
* Configuration via Options pattern
* Startup diagnostics logging
* Development workflow without Docker
* Telegram webhook integration verified

Jobs currently accumulate in the database with status `Pending`, but nothing processes them yet.

---

# Scope of Iteration 02

This iteration introduces:

1. Job status lifecycle
2. Strongly typed JobStatus enum
3. Background worker
4. Job locking
5. Processing simulation
6. Retry tracking fields
7. Configurable worker polling and safety limits
8. Controlled in-process concurrency
9. README documentation update

No real Telegram responses or AI processing yet.

---

# JobStatus Enum

Job lifecycle statuses must be implemented as a **strongly typed enum with integer values**.

The enum must be used directly in the Job entity and stored in the database as an **int column**.

Do NOT store statuses as strings.

Example:

```csharp
/// <summary>
/// Lifecycle status of a job in the processing pipeline.
/// </summary>
public enum JobStatus
{
    /// <summary>
    /// Job was received via Telegram webhook and is waiting for processing.
    /// </summary>
    Pending = 0,

    /// <summary>
    /// Worker has acquired the job and is currently processing it.
    /// </summary>
    Processing = 1,

    /// <summary>
    /// Job finished successfully.
    /// </summary>
    Completed = 2,

    /// <summary>
    /// Job processing failed.
    /// </summary>
    Failed = 3
}
```

Each enum value must include **XML documentation comments** so IDE tooltips explain their meaning.

---

# Job Status Lifecycle

Jobs move through the following lifecycle:

Pending → Processing → Completed

If processing fails:

Processing → Failed

Meaning:

Pending
Job was received via webhook and is waiting in the queue.

Processing
Worker has acquired the job and is currently processing it.

Completed
Processing finished successfully.

Failed
Processing failed.

Retry rule:

- `Attempts` increments when a job transitions from `Pending` to `Processing`
- if processing fails and `Attempts < MaxAttempts`, the job returns to `Pending`
- if processing fails and `Attempts >= MaxAttempts`, the job becomes `Failed`

---

## Worker Processing State Machine

The worker processing model is a deterministic state machine with four states:

- Pending
- Processing
- Completed
- Failed

Allowed transitions:

- `Pending -> Processing`
- `Processing -> Completed`
- `Processing -> Pending`
- `Processing -> Failed`

Transition rules:

- `Pending -> Processing`
  Occurs when a worker successfully acquires the job using an atomic update.
  `Attempts` is incremented during this transition.

- `Processing -> Completed`
  Occurs when job processing finishes successfully.

- `Processing -> Pending`
  Occurs when processing throws an exception and `Attempts < MaxAttempts`.

- `Processing -> Failed`
  Occurs when processing throws an exception and `Attempts >= MaxAttempts`.

Important rule:

- `Attempts` increments only when the job transitions from `Pending` to `Processing`.

---

# Database Changes

Extend the `Jobs` table.

Required columns:

Status (int)
Attempts (int, default 0)
LastError (text, nullable)
UpdatedAt (timestamp)

Required index:

IX_Jobs_Status_Attempts_Id

Purpose:

Attempts
Tracks how many times the job was attempted.

LastError
Stores error message if processing fails.

UpdatedAt
Tracks the last status change.

IX_Jobs_Status_Attempts_Id
Supports the polling query on `Status`, `Attempts`, and `Id`.

Create a new EF migration for these changes.

Repository is pre-production. Database resets are acceptable for this iteration, so no string-to-int status migration path is required.

---

# Job Entity

The Job entity must use the enum directly.

Example concept:

```csharp
public JobStatus Status { get; set; }
```

EF Core must store this enum as an **integer column**.

---

# Worker

Introduce a background worker using:

BackgroundService

The worker runs continuously inside the same ASP.NET Core process.

Multiple application instances are allowed.

Multi-instance worker execution is expected.

Workers coordinate through atomic database updates so only one worker acquires a given pending job.

## WorkerOptions

Configuration class:

WorkerOptions

Properties:

- PollIntervalMs
- MaxConcurrentJobs
- MaxAttempts
- MaxJobAgeMinutes

Example defaults:

- PollIntervalMs = 1000
- MaxConcurrentJobs = 4
- MaxAttempts = 2
- MaxJobAgeMinutes = 30

Production deployments may override `PollIntervalMs` to `2000`.

Environment variables:

- Worker__PollIntervalMs
- Worker__MaxConcurrentJobs
- Worker__MaxAttempts
- Worker__MaxJobAgeMinutes

Concurrency rules:

- worker keeps polling while local capacity is available
- worker may process up to `MaxConcurrentJobs` jobs in parallel inside one process
- job acquisition remains atomic across all worker instances
- each job execution must use its own service scope and `DbContext`

Polling delay:

- use `PollIntervalMs` plus jitter from `0` to `200` ms
- jitter reduces synchronization across multiple application instances

Pseudo flow:

loop forever

```
while local concurrency is below MaxConcurrentJobs:

select one job where Status = Pending and Attempts < MaxAttempts

order by Id ASC

attempt atomic acquisition

start processing task without blocking the polling loop

after acquisition, check job age

if job is too old, mark failed and skip processing

simulate processing

mark as completed
```

sleep for configured poll interval and repeat

---

# Job Locking

Prevent multiple workers from processing the same job.

Use an atomic database update pattern.

Concept:

1. Worker selects one candidate job:
   - `Status = Pending`
   - `Attempts < MaxAttempts`
   - `ORDER BY Id ASC`
   - one row only

2. Worker attempts an atomic acquisition:
   - change `Pending -> Processing`
   - increment `Attempts`
   - update `UpdatedAt`

3. If exactly one row was affected, the job was acquired.

4. If zero rows were affected, another worker acquired the job.

The worker must retry the loop.

This guarantees that only one worker acquires the job, even when multiple processes or containers poll at the same time.

`CreatedAt` remains in the schema for diagnostics but is not used for job ordering.

---

# Processing Simulation

For now job processing should simulate work.

Worker should:

log that the job is being processed

wait 1500 ms

mark job as completed

No external APIs yet.

After acquisition but before processing, worker must check job age.

If job age exceeds `MaxJobAgeMinutes`, mark the job as `Failed` and do not process it.

---

# Error Handling

If processing throws an exception:

store error message in LastError

Do not increment `Attempts` here.

`Attempts` was already incremented during acquisition.

if Attempts < MaxAttempts:
return job to Pending

if Attempts >= MaxAttempts:
set status to Failed

Retry condition is evaluated after the exception using the already incremented `Attempts` value.

update UpdatedAt

log the error

Iteration 02 does not implement automatic recovery for jobs stuck in `Processing`.

If a worker process crashes after acquiring a job, that job may remain in `Processing`.

Manual intervention is acceptable in this iteration.

---

# Logging

Worker must log lifecycle events.

Examples:

Worker started

Job picked: {JobId}

Job processing started

Job completed

Job failed

Do not log every polling cycle when no job is found.

---

# README Update

During this iteration update README.md.

Add a section describing the job lifecycle.

Example structure:

## Job Lifecycle

| Status     | Meaning                             |
| ---------- | ----------------------------------- |
| Pending    | Job received and waiting in queue   |
| Processing | Worker currently processing the job |
| Completed  | Job finished successfully           |
| Failed     | Job processing failed               |

README must also state that statuses are represented by the `JobStatus` enum.

---

# Development Environment

Local development continues to follow the existing policy.

Application runs locally using:

dotnet run

Infrastructure such as PostgreSQL may run in Docker.

The worker runs inside the same application process.

Docker remains for infrastructure only during local development.

---

# Verification

After this iteration the following must work:

1. Application starts successfully
2. Worker starts automatically
3. Telegram messages create jobs
4. Worker picks pending jobs
5. Jobs transition through lifecycle:

Pending → Processing → Completed

6. Database reflects status transitions
7. Attempts and UpdatedAt update correctly
8. Retry behavior follows `MaxAttempts`
9. FIFO ordering uses `Id ASC`
10. Jobs older than `MaxJobAgeMinutes` are marked `Failed` without processing
11. Worker respects `MaxConcurrentJobs`
12. Polling delay includes jitter
13. Polling index exists for the queue query

---

# Out of Scope

This iteration does NOT include:

Telegram responses
AI integration
TTS generation
distributed queues

These will be introduced in later iterations.

---

# Expected Architecture After Iteration 02

Telegram
↓
Webhook
↓
Jobs table
↓
Worker (BackgroundService)
↓
Job processing lifecycle
