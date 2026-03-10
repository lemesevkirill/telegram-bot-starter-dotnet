using System.Diagnostics.Metrics;
using System.Threading;

namespace BotTemplate.Api;

public static class Metrics
{
    public const string MeterName = "BotTemplate.Api";

    public static readonly Meter Meter = new(MeterName, "1.0.0");

    public static readonly Counter<long> JobsCreatedTotal = Meter.CreateCounter<long>("jobs_created_total");
    public static readonly Counter<long> JobsAcquiredTotal = Meter.CreateCounter<long>("jobs_acquired_total");
    public static readonly Counter<long> JobsCompletedTotal = Meter.CreateCounter<long>("jobs_completed_total");
    public static readonly Counter<long> JobsFailedTotal = Meter.CreateCounter<long>("jobs_failed_total");

    public static readonly Counter<long> LlmRequestsTotal = Meter.CreateCounter<long>("llm_requests_total");
    public static readonly Counter<long> LlmErrorsTotal = Meter.CreateCounter<long>("llm_errors_total");

    public static readonly Counter<long> TtsRequestsTotal = Meter.CreateCounter<long>("tts_requests_total");
    public static readonly Counter<long> TtsErrorsTotal = Meter.CreateCounter<long>("tts_errors_total");

    public static readonly Counter<long> TelegramSendTotal = Meter.CreateCounter<long>("telegram_send_total");
    public static readonly Counter<long> TelegramSendErrorsTotal = Meter.CreateCounter<long>("telegram_send_errors_total");

    public static readonly Histogram<double> JobDurationSeconds = Meter.CreateHistogram<double>("job_duration_seconds", "seconds");
    public static readonly Histogram<double> JobQueueWaitSeconds = Meter.CreateHistogram<double>("job_queue_wait_seconds", "seconds");
    public static readonly Histogram<double> LlmLatencySeconds = Meter.CreateHistogram<double>("llm_latency_seconds", "seconds");
    public static readonly Histogram<double> TtsLatencySeconds = Meter.CreateHistogram<double>("tts_latency_seconds", "seconds");
    public static readonly Histogram<double> TelegramSendLatencySeconds = Meter.CreateHistogram<double>("telegram_send_latency_seconds", "seconds");

    private static long jobsPending;
    private static long jobsProcessing;
    private static long workerInflightJobs;

    private static readonly ObservableGauge<long> JobsPending = Meter.CreateObservableGauge(
        "jobs_pending",
        () => new Measurement<long>(Interlocked.Read(ref jobsPending)));

    private static readonly ObservableGauge<long> JobsProcessing = Meter.CreateObservableGauge(
        "jobs_processing",
        () => new Measurement<long>(Interlocked.Read(ref jobsProcessing)));

    private static readonly ObservableGauge<long> WorkerInflightJobs = Meter.CreateObservableGauge(
        "worker_inflight_jobs",
        () => new Measurement<long>(Interlocked.Read(ref workerInflightJobs)));

    public static void SetJobsPending(long value) => Interlocked.Exchange(ref jobsPending, value);

    public static void SetJobsProcessing(long value) => Interlocked.Exchange(ref jobsProcessing, value);

    public static void IncrementWorkerInflightJobs() => Interlocked.Increment(ref workerInflightJobs);

    public static void DecrementWorkerInflightJobs() => Interlocked.Decrement(ref workerInflightJobs);
}
