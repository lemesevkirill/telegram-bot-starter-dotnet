using Telegram.Bot;
using Telegram.Bot.Types.Enums;

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
}
