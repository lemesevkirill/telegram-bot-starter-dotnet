# ITERATION 04 — JobExecutor + LLM Integration

Status: Completed

## Goal

Introduce an execution layer responsible for job processing and orchestration.

The worker must remain an infrastructure component responsible only for:

- job acquisition
- retry and lifecycle management
- concurrency control

All job execution logic must be delegated to a new component:

JobExecutor.

Iteration 04 also introduces the first real execution pipeline using an LLM service.

The bot will translate incoming Telegram messages to German using the OpenAI API.

This provides a realistic processing step while keeping the example simple.

---

# Target Architecture

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
IJobExecutor
↓
TelegramJobExecutor
↓
ILLMService
↓
OpenAiLLMService
↓
OpenAI API
↓
TelegramSender

Responsibilities are separated between infrastructure and execution layers.

---

# Worker Responsibilities

Worker remains infrastructure-only.

Responsible for:

- polling jobs
- atomic acquisition
- retry logic
- job lifecycle
- concurrency limits
- invoking JobExecutor

Worker must NOT:

- parse UpdatePayload
- call LLM APIs
- inspect Telegram message content
- send Telegram messages directly

---

# Execution Layer

## Interface

Create interface:

IJobExecutor

Signature:

Task ExecuteAsync(Job job, CancellationToken ct)

The worker calls the executor for every acquired job.

---

## Implementation

Create implementation:

TelegramJobExecutor

Location suggestion:

BotTemplate.Api/Execution/

    IJobExecutor.cs
    TelegramJobExecutor.cs

Responsibilities:

- deserialize UpdatePayload
- extract Telegram message
- send typing indicator
- call ILLMService
- send translated response

Exceptions must NOT be swallowed.

They must propagate to the worker so retry logic continues to function.

---

# LLM Integration

Introduce a dedicated service responsible for interacting with the LLM.

Create:

ILLMService
OpenAiLLMService

Location suggestion:

BotTemplate.Api/LLM/

    ILLMService.cs
    OpenAiLLMService.cs
    LLMOptions.cs

Purpose:

Encapsulate all interaction with the OpenAI API.

Responsibilities:

- build prompts
- call OpenAI API
- parse responses
- return LLMResult

JobExecutor must NOT call OpenAI directly.

---

# Prompt Builder

Introduce a minimal component responsible for constructing prompts.

Create:

PromptBuilder

Location:

BotTemplate.Api/LLM/PromptBuilder.cs

Purpose:

Centralize prompt construction logic.

Example behavior:

Translate the following text to German.

Text:
{user_message}

Example method:

string BuildGermanTranslationPrompt(string text)

This prevents prompt strings from being scattered across the codebase.

---

# LLM Result Type

Introduce a simple result type returned by the LLM service.

Create:

LLMResult

Location:

BotTemplate.Api/LLM/LLMResult.cs

Example structure:

public sealed class LLMResult
{
    public string Text { get; init; } = "";
}

The service returns this object instead of raw strings.

This allows future extensions such as:

- token usage
- model metadata
- structured outputs

without breaking the interface.

---

# LLM Behavior

For this iteration the LLM performs a simple task:

Translate user text to German.

Prompt example:

Translate the following text to German.

Text:
{user_message}

The service must return only the translated text.

---

# OpenAI Client

Use HttpClient REST call to OpenAI API.

Recommended model:

gpt-4.1-mini

The request must:

- send the translation prompt
- receive completion
- extract generated text

Minimal error handling is acceptable for this iteration.

## LLMOptions

Configuration class:

LLMOptions

Section:

LLM

Properties:

- ApiKey (required)
- Model (default `gpt-4.1-mini`)
- BaseUrl (default `https://api.openai.com/v1/`)
- TimeoutSeconds (default `60`)

---

# Execution Flow

When a job is executed:

1. JobExecutor deserializes UpdatePayload
2. Extracts Telegram message text
3. If message text is null:
       log
       return
4. If message text exists:
       send typing indicator
       call ILLMService.TranslateToGermanAsync(text)
       send Telegram response

Example interaction:

User sends:

Hello, how are you?

Bot replies:

Hallo, wie geht es dir?

---

# Dependency Injection

Register services:

IJobExecutor → TelegramJobExecutor
ILLMService → OpenAiLLMService

Recommended lifecycle:

Scoped

OpenAiLLMService dependencies may include:

- HttpClient
- ILogger
- PromptBuilder

---

# Worker Changes

Modify JobWorker.

Replace message processing logic with executor invocation.

Execution flow:

1. Acquire job
2. Check MaxJobAgeMinutes
3. Call executor:

   await jobExecutor.ExecuteAsync(job, ct)

4. If successful → mark Completed
5. If exception → retry logic applies

Worker must remain infrastructure-only.

---

# Constraints

Iteration 04 must NOT introduce:

- workflow engines
- tool frameworks
- command routing
- pipeline DSL
- multiple executor implementations

Only one executor implementation is required.

Keep the architecture minimal and pragmatic.

---

# Verification

Expected runtime behavior:

1. Telegram message arrives
2. Webhook stores job
3. Worker acquires job
4. Worker calls JobExecutor
5. Executor sends typing indicator
6. Executor calls ILLMService
7. LLM translates text
8. Executor sends translated message
9. Worker marks job Completed

Retry behavior must remain unchanged.

---

# Expected Outcome

After Iteration 04:

Worker → infrastructure

JobExecutor → orchestration

ILLMService / OpenAiLLMService → LLM interaction

PromptBuilder → prompt construction

LLMResult → structured LLM response

This prepares the system for future features:

- richer LLM pipelines
- TTS generation
- crawling
- multi-step orchestration
