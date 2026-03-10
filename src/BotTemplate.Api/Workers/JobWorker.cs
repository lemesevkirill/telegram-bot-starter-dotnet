using BotTemplate.Core.Configuration;
using BotTemplate.Core.Execution;
using BotTemplate.Core.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace BotTemplate.Api.Workers;

public sealed class JobWorker(
    ILogger<JobWorker> logger,
    IServiceScopeFactory serviceScopeFactory,
    IOptions<WorkerOptions> workerOptionsAccessor) : BackgroundService
{
    private readonly WorkerOptions workerOptions = workerOptionsAccessor.Value;
    private readonly SemaphoreSlim concurrencyLimiter = new(workerOptionsAccessor.Value.MaxConcurrentJobs, workerOptionsAccessor.Value.MaxConcurrentJobs);
    private readonly Lock runningJobsLock = new();
    private readonly List<Task> runningJobs = [];

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Worker started");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await FillAvailableCapacityAsync(stoppingToken);
                await RefreshQueueMetricsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Worker cycle failed");
            }

            await Task.Delay(GetPollingDelay(), stoppingToken);
        }

        Task[] remainingJobs;
        lock (runningJobsLock)
        {
            remainingJobs = [.. runningJobs];
        }

        try
        {
            await Task.WhenAll(remainingJobs);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task FillAvailableCapacityAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested && concurrencyLimiter.Wait(0))
        {
            var acquisitionResult = await TryAcquireNextJobAsync(cancellationToken);

            if (acquisitionResult.Kind == AcquisitionResultKind.NoCandidate)
            {
                concurrencyLimiter.Release();
                break;
            }

            if (acquisitionResult.Kind == AcquisitionResultKind.LostRace)
            {
                concurrencyLimiter.Release();
                continue;
            }

            StartJobProcessing(acquisitionResult.JobId!.Value, acquisitionResult.AcquiredAt!.Value, cancellationToken);
        }
    }

    private void StartJobProcessing(long jobId, DateTime acquiredAt, CancellationToken cancellationToken)
    {
        Metrics.IncrementWorkerInflightJobs();
        var task = ProcessAcquiredJobAsync(jobId, acquiredAt, cancellationToken);

        lock (runningJobsLock)
        {
            runningJobs.Add(task);
        }
    }

    private async Task<AcquisitionResult> TryAcquireNextJobAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var candidateJobId = await dbContext.Jobs
            .AsNoTracking()
            .Where(job => job.Status == JobStatus.Pending && job.Attempts < workerOptions.MaxAttempts)
            .OrderBy(job => job.Id)
            .Select(job => (long?)job.Id)
            .FirstOrDefaultAsync(cancellationToken);

        if (candidateJobId is null)
        {
            // logger.LogInformation("No Job picked - no candidate");
            return new AcquisitionResult(AcquisitionResultKind.NoCandidate, null, null);
        }

        var acquiredAt = DateTime.UtcNow;
        var rowsAffected = await dbContext.Jobs
            .Where(job => job.Id == candidateJobId.Value && job.Status == JobStatus.Pending)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(job => job.Status, JobStatus.Processing)
                    .SetProperty(job => job.Attempts, job => job.Attempts + 1)
                    .SetProperty(job => job.UpdatedAt, acquiredAt),
                cancellationToken);

        if (rowsAffected == 0)
        {
            logger.LogInformation("No Job picked - lost race");
            return new AcquisitionResult(AcquisitionResultKind.LostRace, null, null);
        }

        logger.LogInformation("Job picked {JobId}", candidateJobId.Value);
        return new AcquisitionResult(AcquisitionResultKind.Acquired, candidateJobId.Value, acquiredAt);
    }

    private async Task ProcessAcquiredJobAsync(long jobId, DateTime acquiredAt, CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = serviceScopeFactory.CreateAsyncScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var job = await dbContext.Jobs.SingleAsync(currentJob => currentJob.Id == jobId, cancellationToken);
            var processingStopwatch = Stopwatch.StartNew();

            Metrics.JobsAcquiredTotal.Add(
                1,
                new KeyValuePair<string, object?>("component", "worker"),
                new KeyValuePair<string, object?>("operation", "acquire"),
                new KeyValuePair<string, object?>("result", "success"));

            logger.LogInformation(
                "job_acquired component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                "worker",
                "acquire",
                "success",
                job.Id,
                job.UpdateId,
                job.ChatId,
                job.Attempts,
                0d);

            if (IsJobExpired(job, acquiredAt))
            {
                job.Status = JobStatus.Failed;
                job.UpdatedAt = DateTime.UtcNow;
                job.LastError = $"Job expired: age exceeded {workerOptions.MaxJobAgeMinutes} minutes";
                await dbContext.SaveChangesAsync(cancellationToken);
                var durationSeconds = processingStopwatch.Elapsed.TotalSeconds;
                var queueWaitSeconds = (acquiredAt - job.CreatedAt).TotalSeconds;

                Metrics.JobsFailedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>("component", "worker"),
                    new KeyValuePair<string, object?>("operation", "process_job"),
                    new KeyValuePair<string, object?>("result", "failed"));
                Metrics.JobDurationSeconds.Record(durationSeconds);
                Metrics.JobQueueWaitSeconds.Record(queueWaitSeconds);

                logger.LogWarning(
                    "job_failed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                    "worker",
                    "process_job",
                    "failed_expired",
                    job.Id,
                    job.UpdateId,
                    job.ChatId,
                    job.Attempts,
                    processingStopwatch.Elapsed.TotalMilliseconds);
                return;
            }

            try
            {
                var ctx = new JobContext
                {
                    JobId = job.Id,
                    UpdateId = job.UpdateId,
                    ChatId = job.ChatId,
                    Attempt = job.Attempts
                };

                var jobExecutor = scope.ServiceProvider.GetRequiredService<IJobExecutor>();
                await jobExecutor.ExecuteAsync(ctx, job.UpdatePayload, cancellationToken);

                job.Status = JobStatus.Completed;
                job.LastError = null;
                job.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);
                var durationSeconds = processingStopwatch.Elapsed.TotalSeconds;
                var queueWaitSeconds = (acquiredAt - job.CreatedAt).TotalSeconds;

                Metrics.JobsCompletedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>("component", "worker"),
                    new KeyValuePair<string, object?>("operation", "process_job"),
                    new KeyValuePair<string, object?>("result", "success"));
                Metrics.JobDurationSeconds.Record(durationSeconds);
                Metrics.JobQueueWaitSeconds.Record(queueWaitSeconds);

                logger.LogInformation(
                    "job_completed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                    "worker",
                    "process_job",
                    "success",
                    job.Id,
                    job.UpdateId,
                    job.ChatId,
                    job.Attempts,
                    processingStopwatch.Elapsed.TotalMilliseconds);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                job.LastError = ex.Message;
                job.Status = job.Attempts < workerOptions.MaxAttempts
                    ? JobStatus.Pending
                    : JobStatus.Failed;
                job.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);
                var durationSeconds = processingStopwatch.Elapsed.TotalSeconds;
                var queueWaitSeconds = (acquiredAt - job.CreatedAt).TotalSeconds;

                Metrics.JobsFailedTotal.Add(
                    1,
                    new KeyValuePair<string, object?>("component", "worker"),
                    new KeyValuePair<string, object?>("operation", "process_job"),
                    new KeyValuePair<string, object?>("result", "failed"));
                Metrics.JobDurationSeconds.Record(durationSeconds);
                Metrics.JobQueueWaitSeconds.Record(queueWaitSeconds);

                logger.LogError(
                    ex,
                    "job_failed component={component} operation={operation} status={status} job_id={job_id} update_id={update_id} chat_id={chat_id} attempt={attempt} duration_ms={duration_ms}",
                    "worker",
                    "process_job",
                    "failed",
                    job.Id,
                    job.UpdateId,
                    job.ChatId,
                    job.Attempts,
                    processingStopwatch.Elapsed.TotalMilliseconds);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        finally
        {
            Metrics.DecrementWorkerInflightJobs();
            concurrencyLimiter.Release();

            lock (runningJobsLock)
            {
                runningJobs.RemoveAll(task => task.IsCompleted);
            }
        }
    }

    private TimeSpan GetPollingDelay()
    {
        var jitterMs = Random.Shared.Next(0, 201);
        return TimeSpan.FromMilliseconds(workerOptions.PollIntervalMs + jitterMs);
    }

    private bool IsJobExpired(Job job, DateTime acquiredAt)
    {
        var jobAge = acquiredAt - job.CreatedAt;
        return jobAge > TimeSpan.FromMinutes(workerOptions.MaxJobAgeMinutes);
    }

    private async Task RefreshQueueMetricsAsync(CancellationToken cancellationToken)
    {
        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var pendingCount = await dbContext.Jobs.CountAsync(job => job.Status == JobStatus.Pending, cancellationToken);
        var processingCount = await dbContext.Jobs.CountAsync(job => job.Status == JobStatus.Processing, cancellationToken);

        Metrics.SetJobsPending(pendingCount);
        Metrics.SetJobsProcessing(processingCount);
    }

    private enum AcquisitionResultKind
    {
        NoCandidate,
        LostRace,
        Acquired
    }

    private readonly record struct AcquisitionResult(AcquisitionResultKind Kind, long? JobId, DateTime? AcquiredAt);
}
