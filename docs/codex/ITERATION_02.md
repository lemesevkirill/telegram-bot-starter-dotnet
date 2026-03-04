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

Jobs currently accumulate in the database with status `pending`, but nothing processes them yet.

---

# Scope of Iteration 02

This iteration introduces:

1. Job status lifecycle
2. Strongly typed JobStatus enum
3. Background worker
4. Job locking
5. Processing simulation
6. Retry tracking fields
7. README documentation update

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

---

# Database Changes

Extend the `Jobs` table.

Required columns:

Status (int)
Attempts (int, default 0)
LastError (text, nullable)
UpdatedAt (timestamp)

Purpose:

Attempts
Tracks how many times the job was attempted.

LastError
Stores error message if processing fails.

UpdatedAt
Tracks the last status change.

Create a new EF migration for these changes.

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

The worker runs continuously.

Pseudo flow:

loop forever

```
fetch one pending job

atomically lock job

mark as processing

simulate processing

mark as completed
```

sleep briefly and repeat

---

# Job Locking

Prevent multiple workers from processing the same job.

Use an atomic database update pattern.

Concept:

UPDATE Jobs
SET Status = Processing
WHERE Id = ?
AND Status = Pending

Only continue if exactly one row was affected.

This guarantees that only one worker acquires the job.

---

# Processing Simulation

For now job processing should simulate work.

Worker should:

log that the job is being processed

wait 1–2 seconds

mark job as completed

No external APIs yet.

---

# Error Handling

If processing throws an exception:

increment Attempts

store error message in LastError

set status to Failed

update UpdatedAt

log the error

---

# Logging

Worker must log lifecycle events.

Examples:

Worker started

Job picked: {JobId}

Job processing started

Job completed

Job failed

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

---

# Out of Scope

This iteration does NOT include:

Telegram responses
AI integration
TTS generation
parallel workers
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
