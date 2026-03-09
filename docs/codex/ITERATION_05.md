# ITERATION 05 — TTS Integration (OpenAI)

Status: Completed

## Goal

Extend the existing execution pipeline to support audio output.

The bot will now return:

- translated text
- a synthesized audio version of the translation

The audio will be generated using the OpenAI TTS API.

Telegram users will be able to **read the translation and listen to it**.

This iteration adds one new infrastructure component:

TTSService.

No architectural refactoring is required.

The execution flow remains simple and linear.

---

# Target Architecture

Current architecture after Iteration 04:

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
TelegramSender

Target architecture after Iteration 05:

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

Execution pipeline:

User message  
→ LLM translation  
→ TTS generation  
→ Telegram response (text + audio)

---

# Execution Flow

Updated execution sequence inside `TelegramJobExecutor`:

1. Deserialize UpdatePayload
2. Extract message text
3. If message text is null or whitespace:
   - log
   - return successfully
4. Send Telegram typing indicator
5. Call `ILLMService.TranslateToGermanAsync`
6. Receive translated text
7. Call `ITTSService.SynthesizeAsync`
8. Receive audio stream
9. Send Telegram message with:
   - translated text
   - audio attachment

Exceptions must propagate to the worker.

Retry behavior remains unchanged.

---

# TTS Layer

Introduce a new service responsible for speech synthesis.

Create:

ITTSService  
OpenAiTTSService

Location:

BotTemplate.Api/TTS/

    ITTSService.cs
    OpenAiTTSService.cs
    TTSOptions.cs

Purpose:

Encapsulate all interaction with the OpenAI TTS API.

---

# TTS Interface

Minimal interface for this iteration:

```
Task<Stream> SynthesizeAsync(string text, CancellationToken ct);
```

The method returns an MP3 audio stream.

The executor is responsible for sending the audio via Telegram.

---

# OpenAI TTS API

Use the OpenAI speech API.

Example model:

```
gpt-4o-mini-tts
```

Example parameters:

- model
- voice
- input text

Output format:

```
mp3
```

The service returns the generated audio as a stream.

---

# TTS Configuration

Introduce `TTSOptions` using the Options pattern.

Example configuration:

```
TTS__ApiKey
TTS__Model
TTS__Voice
TTS__Format
TTS__TimeoutSeconds
TTS__MaxInputLength
```

Example class:

```
public sealed class TTSOptions
{
    public string ApiKey { get; init; } = "";
    public string Model { get; init; } = "gpt-4o-mini-tts";
    public string Voice { get; init; } = "alloy";
    public string Format { get; init; } = "mp3";
    public int TimeoutSeconds { get; init; } = 30;
    public int MaxInputLength { get; init; } = 1000;
}
```

`MaxInputLength` limits TTS input size to reduce memory pressure during synthesis and upload.

---

# Telegram Response

The executor should send two things:

1. Text message with the translated text.
2. Audio message with the synthesized speech.

Audio format:

```
MP3
```

Telegram supports sending audio using multipart upload.

`TelegramSender` exposes a stream-based upload method:

```
Task SendAudioAsync(long chatId, Stream audio, string? caption, CancellationToken ct)
```

Implementation uses:

`new InputFileStream(audio, "translation.mp3")`

Caption contains the translated text.

Stream ownership:

- executor owns stream lifecycle
- executor disposes stream after sending

---

# Dependency Injection

Register new services:

```
ITTSService → OpenAiTTSService
```

Recommended lifecycle:

```
Scoped
```

Dependencies may include:

- HttpClient
- ILogger
- IOptions<TTSOptions>

---

# Constraints

Iteration 05 must NOT introduce:

- pipeline frameworks
- step orchestration engines
- job type routing
- generic tool systems

Execution remains a simple linear pipeline.

---

# Verification

Expected runtime behavior:

1. User sends message in Telegram.
2. Webhook stores job.
3. Worker acquires job.
4. Worker calls JobExecutor.
5. Executor sends typing indicator.
6. Executor calls ILLMService.
7. LLM translates text.
8. Executor calls ITTSService.
9. TTS generates audio stream.
10. Executor sends text reply.
11. Executor sends audio reply.
12. Worker marks job Completed.

Retry behavior must remain unchanged.

---

# Expected Outcome

After Iteration 05:

The bot returns:

- translated text
- spoken version of the translation

Architecture now supports a two-step processing pipeline:

```
LLM → TTS
```

without introducing additional orchestration layers.

This prepares the system for future iterations involving:

- long-form text summarization
- audio narration
- content analysis pipelines
