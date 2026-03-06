using BotTemplate.Core.Configuration;
using BotTemplate.Core.Jobs;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

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

            if (IsJobExpired(job, acquiredAt))
            {
                job.Status = JobStatus.Failed;
                job.UpdatedAt = DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogWarning("Job failed {JobId}", job.Id);
                return;
            }

            try
            {
                logger.LogInformation("Processing started {JobId}", job.Id);
                await Task.Delay(1500, cancellationToken);

                job.Status = JobStatus.Completed;
                job.LastError = null;
                job.UpdatedAt = DateTime.UtcNow;

                await dbContext.SaveChangesAsync(cancellationToken);
                logger.LogInformation("Job completed {JobId}", job.Id);
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
                logger.LogError(ex, "Job failed {JobId}", job.Id);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        finally
        {
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

    private enum AcquisitionResultKind
    {
        NoCandidate,
        LostRace,
        Acquired
    }

    private readonly record struct AcquisitionResult(AcquisitionResultKind Kind, long? JobId, DateTime? AcquiredAt);
}
