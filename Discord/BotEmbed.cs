using Discord;

namespace DiscordDiceBot.Discord
{
    internal class BotEmbed
    {
        public static readonly Color DebiColor = 0x444C7D;

        internal static Embed Default(string? title = null, string? description = null, Color? color = null)
        {
            var embed = new EmbedBuilder()
            {
                Title = title,
                Description = description,
                Color = color ?? DebiColor,
                Timestamp = DateTime.Now
            };
            return embed.Build();
        }
        internal static Embed Error(string description)
        {
            return Default("エラー", description, Color.DarkRed);
        }
        internal static Embed UnknownError()
        {
            return Error("技術的問題が発生しました。\n管理者にお問い合わせください。");
        }
    }
}