# ITERATION 04 - JobExecutor + LLM Integration

Status: Completed

## Goal

Introduce an execution layer so worker infrastructure concerns stay separate from business execution logic.

This iteration added LLM translation using OpenAI API and moved message execution into `TelegramJobExecutor`.

---

## Implemented Architecture

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

---

## What Was Implemented

- `IJobExecutor` and `TelegramJobExecutor`
- `ILLMService` and `OpenAiLLMService`
- `PromptBuilder`
- `LLMResult`
- `LLMOptions`
- DI wiring for executor and LLM services
- Worker processing path updated to call `IJobExecutor.ExecuteAsync(...)`

---

## Responsibilities

Worker (infrastructure):

- polling and acquisition
- retry and lifecycle state transitions
- concurrency control
- invoking executor

TelegramJobExecutor (execution):

- deserialize `UpdatePayload`
- extract Telegram message text
- send typing indicator
- call `ILLMService.TranslateToGermanAsync`
- send translated text via `TelegramSender`

OpenAiLLMService:

- build prompt via `PromptBuilder`
- call OpenAI `/responses`
- parse response
- return `LLMResult`

Exceptions in executor/services propagate to worker so existing retry behavior applies.

---

## Configuration

`LLMOptions` (`LLM` section):

- `ApiKey`
- `Model` (default `gpt-4.1-mini`)
- `BaseUrl` (default `https://api.openai.com/v1/`)
- `TimeoutSeconds` (default `60`)

Environment variables:

- `LLM__ApiKey`
- `LLM__Model`
- `LLM__BaseUrl`
- `LLM__TimeoutSeconds`

---

## Verification Summary

Iteration 04 delivers:

1. Worker acquires job and invokes executor
2. Executor calls LLM service for German translation
3. Executor sends translated text via Telegram
4. Worker marks job `Completed` on success
5. Existing retry behavior remains unchanged on errors

