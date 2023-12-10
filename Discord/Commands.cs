using Discord;
using Discord.Interactions;
using DiscordDiceBot.Dice;

namespace DiscordDiceBot.Discord
{
    [EnabledInDm(false)]
    [Group("bcdice", "BCDiceの設定を行います。")]
    public class BCDiceCommand : InteractionModule
    {
        [SlashCommand("set-default", "rollコマンドに使用するデフォルトのゲームシステムを設定します。")]
        public async Task SetDefaultSystem([Summary(description: "設定先")] SetMode mode,
            [Summary(description: "使用するゲームシステム")] string system)
        {
            bool res;
            string mode_name;
            using var context = new SettingContext();
            var setting = await context.GetOrCreateGuildAsync(Context.Guild.Id);
            switch (mode)
            {
                case SetMode.Server:
                    res = setting.SetDefaultSystem(system);
                    mode_name = "サーバー";
                    break;
                case SetMode.Channel:
                    res = setting.SetDefaultSystem(Context.Channel.Id, system);
                    mode_name = "チャンネル";
                    break;
                default:
                    await RespondAsync(BotEmbed.UnknownError(), true);
                    CommandLog(LogLevel.Error, "bcdice", $"Unknown set mode : {Enum.GetName(typeof(SetMode), mode)}");
                    return;
            }

            await context.SaveChangesAsync();
            var embed = res ? BotEmbed.Default("設定完了", $"{mode_name}のデフォルトゲームシステムを `{system}` に設定しました。") :
                BotEmbed.Error("指定されたゲームシステムは存在しないか、現在と同じ設定です。");
            await RespondAsync(embed, !res);
        }
        [SlashCommand("remove-default", "現在のチェンネルに設定されているデフォルト設定を削除します。")]
        public async Task RemoveDefaultSystem()
        {
            using var context = new SettingContext();
            var setting = await context.GetOrCreateGuildAsync(Context.Guild.Id);
            setting.SetDefaultSystem(Context.Channel.Id, null);

            await context.SaveChangesAsync();
            await RespondAsync(BotEmbed.Default("設定完了", $"チャンネルのデフォルトゲームシステムを削除しました。"), false);
        }

        [SlashCommand("message-roll", "コマンドを用いないロールを行うかを設定します。")]
        public async Task SetMessageRoll([Summary(description: "設定先")] SetMode mode,
            [Summary(description: "ロール実行可否")] bool enable)
        {
            bool res;
            string mode_name;
            using var context = new SettingContext();
            var setting = await context.GetOrCreateGuildAsync(Context.Guild.Id);
            switch (mode)
            {
                case SetMode.Server:
                    res = setting.SetMessageRoll(enable);
                    mode_name = "サーバー";
                    break;
                case SetMode.Channel:
                    res = setting.SetMessageRoll(Context.Channel.Id, enable);
                    mode_name = "チャンネル";
                    break;
                default:
                    await RespondAsync(BotEmbed.UnknownError(), true);
                    CommandLog(LogLevel.Error, "bcdice", $"Unknown set mode : {Enum.GetName(typeof(SetMode), mode)}");
                    return;
            }

            await context.SaveChangesAsync();
            var embed = res ? BotEmbed.Default("設定完了", $"{mode_name}のコマンド不使用ロールを {(enable ? "ON" : "OFF")} に設定しました。") :
                BotEmbed.Error("デフォルトのゲームシステムが設定されていないか、現在と同じ設定です。");
            await RespondAsync(embed, !res);
        }

        public enum SetMode
        {
            Server, Channel
        }
    }

    public class RollCommand : InteractionModule
    {
        [SlashCommand("roll", "ダイスを振ります。")]
        public async Task FallguysUser([Summary(description: "コマンド")] string cmd,
            [Summary(description: "使用するゲームシステム")] string? system = null)
        {
            if (string.IsNullOrEmpty(system))
            {
                using var context = new SettingContext();
                RollSetting? setting = await context.Channel.FindAsync(Context.Guild.Id, Context.Channel.Id);
                setting ??= await context.GetOrCreateGuildAsync(Context.Guild.Id);
                system = setting.DefaultSystem;

                if (string.IsNullOrEmpty(system))
                {
                    await RespondAsync(BotEmbed.Error("ゲームシステムが設定されていません。"), true);
                    return;
                }
            }
            else if (!BCDice.Instance.ExistSystemFromId(system))
            {
                if (BCDice.Instance.ExistSystemFromName(system, out var id)) system = id;
                else
                {
                    await RespondAsync(BotEmbed.Error("指定されたゲームシステムが存在しません。"), true);
                    return;
                }
            }

            var roll = await BCDice.Instance.Roll(system, cmd);
            if (roll == null)
            {
                await RespondAsync(BotEmbed.Error("ゲームシステムが設定されていません。"), true);
                return;
            }
            else
            {
                var embed = new EmbedBuilder()
                {
                    Author = Context.User is IGuildUser gu ?
                        new() { Name = gu.Nickname ?? gu.Username, IconUrl = gu.GetGuildAvatarUrl() ?? gu.GetDisplayAvatarUrl() ?? gu.GetDefaultAvatarUrl() } :
                        new() { Name = Context.User.Username, IconUrl = Context.User.GetAvatarUrl() ?? Context.User.GetDefaultAvatarUrl() },
                    Description = roll.Text,
                    Footer = new() { Text = system },
                    Timestamp = DateTimeOffset.Now,
                };
                await RespondAsync(embed.Build(), roll.IsSecret);
            }
        }
    }

    public class InteractionModule : InteractionModuleBase<SocketInteractionContext>
    {
        protected Task RespondAsync(Embed embed, bool ephemeral, MessageComponent? components = null)
        {
            return base.RespondAsync(embeds: new[] { embed }, ephemeral: ephemeral, components: components);
        }
        protected Task ModifyOriginalResponseAsync(Embed embed)
        {
            return base.ModifyOriginalResponseAsync(p => p.Embed = embed);
        }
        protected private static void CommandLog(LogLevel level, string place, string message, Exception? exception = null)
        {
            LocalConsole.Log(level, LogSource.Create("DiscordCommand", place), message, exception);
        }
    }
}