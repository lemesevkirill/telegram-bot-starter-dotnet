# ITERATION 06 - Observability

Status: Completed

## Goal

Provide basic application-level observability for the Telegram bot backend.

Expose metrics for:

- job pipeline throughput
- worker execution
- LLM usage
- TTS usage
- Telegram delivery behavior

---

## Architecture

Observability pipeline:

Bot application
→ OpenTelemetry metrics
→ OTLP exporter
→ Grafana
→ Dashboards

---

## Scope

This iteration introduces:

- OpenTelemetry metrics integration
- structured logging conventions for major pipeline events
- OTLP exporter configuration
- minimal dashboard-ready metric set

This iteration remains minimal and pragmatic.

---

## Structured Logging

### Event names

- `job_created`
- `job_acquired`
- `job_completed`
- `job_failed`
- `llm_started`
- `llm_completed`
- `llm_failed`
- `tts_started`
- `tts_completed`
- `tts_failed`
- `telegram_send_started`
- `telegram_send_completed`
- `telegram_send_failed`

### Standard log fields

- `job_id`
- `update_id`
- `chat_id`
- `attempt`
- `component`
- `operation`
- `status`
- `duration_ms`

Logs must NOT include:

- message payload bodies
- API keys / secrets

---

## Metrics

Use OpenTelemetry Meter API.

Metric naming follows Prometheus conventions.

### Counters

Job pipeline:

- `jobs_created_total`
- `jobs_acquired_total`
- `jobs_completed_total`
- `jobs_failed_total`

LLM:

- `llm_requests_total`
- `llm_errors_total`

TTS:

- `tts_requests_total`
- `tts_errors_total`

Telegram:

- `telegram_send_total`
- `telegram_send_errors_total`

### Histograms

Job pipeline:

- `job_duration_seconds`
- `job_queue_wait_seconds`

LLM:

- `llm_latency_seconds`

TTS:

- `tts_latency_seconds`

Telegram:

- `telegram_send_latency_seconds`

### Gauges

- `jobs_pending`
- `jobs_processing`
- `worker_inflight_jobs`

---

## Metric Label Policy

Allowed labels:

- `component`
- `operation`
- `result`
- `model` (optional)

Forbidden labels (high-cardinality):

- `job_id`
- `chat_id`
- `update_id`
- `user_id`

High-cardinality identifiers must appear only in logs.

---

## Instrumentation Points

### Webhook

- `jobs_created_total`

### Worker

- `jobs_acquired_total`
- `jobs_completed_total`
- `jobs_failed_total`
- `job_duration_seconds`
- `job_queue_wait_seconds`
- `worker_inflight_jobs`

### LLM service

- `llm_requests_total`
- `llm_latency_seconds`
- `llm_errors_total`

### TTS service

- `tts_requests_total`
- `tts_latency_seconds`
- `tts_errors_total`

### TelegramSender

- `telegram_send_total`
- `telegram_send_latency_seconds`
- `telegram_send_errors_total`

---

## Configuration

Observability options:

- `Observability__OtlpEndpoint`
- `Observability__OtlpApiKey`
- `Observability__ServiceName`

Resource attributes:

- `service.name`
- `deployment.environment`

---

## Non-Goals

This iteration does NOT include:

- distributed tracing
- alert rules
- large dashboard packs
- SLO/SLA monitoring
- major architecture refactors

The goal is a minimal observability foundation.

---

## Acceptance Criteria

Iteration 06 is complete when:

- application exports OpenTelemetry metrics
- metrics are visible in Grafana
- job pipeline behavior is observable via metrics
- LLM and TTS latency metrics are available

---

## Implementation Notes

Implemented metrics:

- Counters:
  - `jobs_created_total`
  - `jobs_acquired_total`
  - `jobs_completed_total`
  - `jobs_failed_total`
  - `llm_requests_total`
  - `llm_errors_total`
  - `tts_requests_total`
  - `tts_errors_total`
  - `telegram_send_total`
  - `telegram_send_errors_total`
- Histograms:
  - `job_duration_seconds`
  - `job_queue_wait_seconds`
  - `llm_latency_seconds`
  - `tts_latency_seconds`
  - `telegram_send_latency_seconds`
- Gauges:
  - `jobs_pending`
  - `jobs_processing`
  - `worker_inflight_jobs`

Instrumentation points:

Webhook
`src/BotTemplate.Api/Endpoints/TelegramWebhookEndpoint.cs`
`HandleAsync`
→ `jobs_created_total`

Worker
`src/BotTemplate.Api/Workers/JobWorker.cs`
`ProcessAcquiredJobAsync`
→ `jobs_acquired_total`
→ `jobs_completed_total`
→ `jobs_failed_total`
→ `job_duration_seconds`
→ `job_queue_wait_seconds`

Worker
`src/BotTemplate.Api/Workers/JobWorker.cs`
`StartJobProcessing` / `finally` in `ProcessAcquiredJobAsync`
→ `worker_inflight_jobs`

Worker
`src/BotTemplate.Api/Workers/JobWorker.cs`
`RefreshQueueMetricsAsync`
→ `jobs_pending`
→ `jobs_processing`

LLM Service
`src/BotTemplate.Api/LLM/OpenAiLLMService.cs`
`TranslateToGermanAsync`
→ `llm_requests_total`
→ `llm_latency_seconds`
→ `llm_errors_total`

TTS Service
`src/BotTemplate.Api/TTS/OpenAiTTSService.cs`
`SynthesizeAsync`
→ `tts_requests_total`
→ `tts_latency_seconds`
→ `tts_errors_total`

TelegramSender
`src/BotTemplate.Api/Services/TelegramSender.cs`
`SendWithMetricsAsync`
→ `telegram_send_total`
→ `telegram_send_latency_seconds`
→ `telegram_send_errors_total`

Logging confirmation:

- Structured event names implemented:
  - `job_created`
  - `job_acquired`
  - `job_completed`
  - `job_failed`
  - `llm_started`
  - `llm_completed`
  - `llm_failed`
  - `tts_started`
  - `tts_completed`
  - `tts_failed`
  - `telegram_send_started`
  - `telegram_send_completed`
  - `telegram_send_failed`
- Standard log fields emitted across pipeline with available context:
  - `job_id`
  - `update_id`
  - `chat_id`
  - `attempt`
  - `component`
  - `operation`
  - `status`
  - `duration_ms`

### Job Context Propagation

Iteration 06 introduces JobContext propagation across the execution pipeline
to provide consistent correlation fields in structured logs.

JobContext includes:

- JobId
- UpdateId
- ChatId
- Attempt

The context is created in JobWorker and propagated through:

Worker -> JobExecutor -> LLM / TTS / TelegramSender.

This ensures that all structured logs related to a job contain the same
correlation identifiers, enabling reliable log filtering and debugging.

This change does not affect job processing behavior, queue logic, or metrics.

### Log Export (Loki)

The system exports structured logs to Grafana Cloud Loki using Serilog.

Logs include correlation fields such as:

- job_id
- update_id
- chat_id
- attempt
- component
- operation
- result

This enables debugging of individual jobs through Grafana Explore.

Metrics remain exported through OpenTelemetry.

Logs and metrics together provide full observability.
