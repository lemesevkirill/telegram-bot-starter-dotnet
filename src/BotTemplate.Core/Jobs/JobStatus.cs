namespace BotTemplate.Core.Jobs;

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
