# ITERATION 05 - TTS Integration (OpenAI)

Status: Completed

## Goal

Extend execution pipeline with synthesized audio output, so the bot can return:

- translated text
- spoken audio version

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
ITTSService
↓
OpenAiTTSService
↓
TelegramSender

Execution flow: `LLM -> TTS -> Telegram audio message (caption contains translation)`.

---

## What Was Implemented

- `ITTSService` and `OpenAiTTSService`
- `TTSOptions`
- Stream-based TTS contract:
  - `Task<Stream> SynthesizeAsync(string text, CancellationToken ct)`
- Stream-based Telegram audio upload in `TelegramSender`
- `AudioMessage` DTO to carry audio metadata:
  - `Audio`
  - `Title`
  - `Performer`
  - `Caption`
  - `FileName`
- `TelegramJobExecutor` updated to:
  - call LLM
  - call TTS
  - construct `AudioMessage`
  - send audio through `TelegramSender`

---

## API Contracts

TTS:

- `Task<Stream> SynthesizeAsync(string text, CancellationToken ct)`

Telegram audio send:

- `Task SendAudioAsync(long chatId, AudioMessage message, CancellationToken ct)`

Transport behavior:

- `TelegramSender` uses `InputFileStream(message.Audio, message.FileName)`
- `title`, `performer`, `caption` come from `AudioMessage`

Stream ownership:

- executor owns stream lifecycle
- executor disposes stream after sending

---

## TTS Configuration

`TTSOptions` (`TTS` section):

- `ApiKey`
- `Model` (default `gpt-4o-mini-tts`)
- `Voice` (default `alloy`)
- `Format` (default `mp3`)
- `TimeoutSeconds` (default `30`)
- `MaxInputLength` (default `1000`)

`MaxInputLength` caps TTS input and reduces memory pressure.

Environment variables:

- `TTS__ApiKey`
- `TTS__Model`
- `TTS__Voice`
- `TTS__Format`
- `TTS__TimeoutSeconds`
- `TTS__MaxInputLength`

---

## Verification Summary

Iteration 05 delivers:

1. Executor translates message via LLM
2. Executor synthesizes MP3 audio stream via TTS
3. Executor sends translated content to Telegram as audio message with metadata
4. Worker lifecycle/retry behavior remains unchanged
