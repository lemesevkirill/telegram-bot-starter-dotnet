# ITERATION 07 — Execution Options and Target Language Propagation

Status: Planned

## Goal

Introduce a lightweight execution-parameter mechanism using `ExecutionOptions` persisted on each Job, and propagate `targetLanguage` end-to-end from Telegram webhook input to LLM prompt generation.

This iteration replaces German-specific translation behavior with parameterized translation behavior.

---

## Motivation

The current implementation is hardcoded to German translation, which prevents runtime adaptation from request context.

This iteration introduces `ExecutionOptions` as a durable execution snapshot attached to a Job. New execution parameters can be added without adding new table columns for every option.

`ExecutionOptions` represent execution parameters only. They are not payload storage and not job state.

---

## Architecture

Pipeline after this iteration:

Telegram Update  
→ Webhook  
→ Extract `update.Message.From.LanguageCode`  
→ Build `ExecutionOptions` (`Dictionary<string,string>`)  
→ Persist Job (`ExecutionOptions` included)  
→ Worker loads Job  
→ Worker propagates `ExecutionOptions` into `JobContext`  
→ Executor reads `targetLanguage` from `ExecutionOptions` (fallback to `en`)  
→ `LLMService.TranslateAsync(text, targetLanguage)`  
→ Telegram response

`ExecutionOptions` are immutable after Job creation. Worker and executor must not re-extract language from `UpdatePayload`.

---

## Data Model

### Jobs table

Add a new column:

`ExecutionOptions JSONB NOT NULL DEFAULT '{}'::jsonb`

Purpose:

Store execution parameters required during job execution.

Example:

```json
{
  "targetLanguage": "de"
}
```

### C# representation

`ExecutionOptions` must be represented as:

`Dictionary<string,string>`

Do not use:

- `Dictionary<string,object>`
- `JsonNode`
- `JsonDocument`

CLR property must be non-null by default:

```csharp
ExecutionOptions = new Dictionary<string,string>();
```

### Migration requirements

Migration must add:

`ExecutionOptions JSONB NOT NULL DEFAULT '{}'::jsonb`

This guarantees existing and new rows always have a JSON object, including empty options.

---

## ExecutionOptions Semantics

- `ExecutionOptions` are attached at webhook job creation time.
- `ExecutionOptions` are immutable during processing.
- `ExecutionOptions` are the single source of truth for execution parameters.
- Keys must use `camelCase`.
- For this iteration, supported key:
  - `targetLanguage`

---

## Webhook Responsibilities

When creating a Job:

1. Read Telegram interface language:
   - `update.Message.From.LanguageCode`
2. Initialize `ExecutionOptions` as empty dictionary.
3. If language code exists and is non-empty, store:
   - `ExecutionOptions["targetLanguage"] = languageCode`
4. Persist Job with `ExecutionOptions`.

If Telegram `language_code` is missing, webhook must not write `targetLanguage`.

Webhook must not inject fallback language.

Example values:

- `de`
- `en`
- `fr`
- `ru`
- `pt-BR`

---

## Worker Responsibilities

Worker must load `ExecutionOptions` from Jobs table and propagate them into `JobContext` before calling executor.

`JobContext` must include:

- `JobId`
- `UpdateId`
- `ChatId`
- `Attempt`
- `ExecutionOptions`

Do not add convenience fields like `JobContext.TargetLanguage`.

---

## Executor Responsibilities

Executor must read language from execution options:

- `targetLanguage = ExecutionOptions["targetLanguage"]` if present
- fallback: `targetLanguage = "en"` if missing

Executor must not parse `UpdatePayload` to re-extract language.

Executor must call LLM with explicit language parameter:

- `LLMService.TranslateAsync(text, targetLanguage)`

---

## PromptBuilder Changes

Replace German-specific prompt builder method with parameterized method.

Replace:

`BuildGermanTranslationPrompt(string text)`

With:

`BuildTranslationPrompt(string text, string targetLanguage)`

Prompt template:

```text
Translate the following text to {targetLanguage}.
Return only the translated text.

Text:
{text}
```

---

## LLM Integration

LLM service must accept `targetLanguage` explicitly.

The LLM service must not depend on `JobContext`.

Example shape:

```csharp
Task<string> TranslateAsync(string text, string targetLanguage, CancellationToken ct);
```

---

## German Hardcoding Removal

Remove all German-specific behavior from pipeline components, including:

- Prompt text
- Operation names (logs/metrics)
- User-facing audio title text

Examples:

- Replace operation naming like `translate_to_german` with generic naming like `translate_text`
- Replace `"German Translation"` with `"Translation"`

---

## TTS Behavior

TTS configuration remains static in this starter.

Do not pass language into TTS.

TTS receives already translated text from the LLM stage.

Voice-language matching is out of scope.

---

## Scope

This iteration introduces:

- `ExecutionOptions` JSONB column in `Jobs`
- `Dictionary<string,string>` execution options model
- Extraction of Telegram `language_code`
- Storage of `targetLanguage` in `ExecutionOptions` when present
- Propagation of `ExecutionOptions` through `JobContext`
- Executor fallback to default language `en` when option is missing
- Parameterized prompt builder using `targetLanguage`
- LLM API call with explicit `targetLanguage` argument
- Removal of German-specific naming and prompt behavior

---

## Non-Goals

This iteration does not include:

- User language settings/profile storage
- UI for manual language selection
- Command argument parsing for language
- Schema validation framework for `ExecutionOptions`
- Multi-language TTS voice routing
- Multiple job types

---

## Acceptance Criteria

Iteration 07 is complete when:

- Jobs table includes `ExecutionOptions JSONB NOT NULL DEFAULT '{}'::jsonb`
- Job entity includes non-null `Dictionary<string,string> ExecutionOptions`
- Webhook reads `update.Message.From.LanguageCode`
- Webhook writes `ExecutionOptions["targetLanguage"]` only when language code is present
- Worker propagates `ExecutionOptions` into `JobContext`
- `JobContext` includes `ExecutionOptions` and no dedicated `TargetLanguage` field
- Executor reads `targetLanguage` from `ExecutionOptions`
- Executor applies fallback `en` when `targetLanguage` is missing
- Executor calls LLM with explicit `targetLanguage` argument
- PromptBuilder is parameterized via `BuildTranslationPrompt(text, targetLanguage)`
- LLM service no longer depends on `JobContext`
- German-specific prompt text, operation names, and audio title text are removed
- End-to-end pipeline produces translated output in user language when provided, else defaults to English
