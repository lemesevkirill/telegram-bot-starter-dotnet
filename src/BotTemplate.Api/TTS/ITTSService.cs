using System.IO;

namespace BotTemplate.Api.TTS;

public interface ITTSService
{
    Task<Stream> SynthesizeAsync(string text, CancellationToken ct);
}
