using Telegram.Bot.Types;

namespace fobot.POCOs;

public class CommunicationModel
{
    public string Text { get; set; }
    public Message TelegramMessage { get; set; }
    public User Sender { get; set; }
    public CancellationToken CancellationToken { get; set; }
    public bool isEditOldMessage { get; set; } = true;
}