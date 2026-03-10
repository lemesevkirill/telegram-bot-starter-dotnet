using System.IO;
using BotTemplate.Core.Execution;

namespace BotTemplate.Api.TTS;

public interface ITTSService
{
    Task<Stream> GenerateAsync(JobContext ctx, string text, CancellationToken ct);
}
