using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using BotTemplate.Api.Messaging;

namespace BotTemplate.Api.Services;

public sealed class TelegramSender(TelegramBotClient botClient)
{
    public Task SendTypingAsync(long chatId, CancellationToken cancellationToken = default)
    {
        return botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);
    }

    public Task SendTextMessageAsync(long chatId, string text, CancellationToken cancellationToken = default)
    {
        return botClient.SendMessage(chatId, text, cancellationToken: cancellationToken);
    }

    public Task SendAudioAsync(long chatId, AudioMessage message, CancellationToken cancellationToken = default)
    {
        return botClient.SendAudio(
            chatId,
            new InputFileStream(message.Audio, message.FileName),
            caption: message.Caption,
            performer: message.Performer,
            title: message.Title,
            cancellationToken: cancellationToken);
    }
}
