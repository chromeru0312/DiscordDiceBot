using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using DiscordDiceBot.Dice;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DiscordDiceBot.Discord
{
    public class DiscordBot
    {
        public static DiscordBot Instance { get; } = new();
        public DiscordSocketClient Client { get; }
        public InteractionService Interactions { get; }
        public ServiceProvider Services { get; }

        private DiscordBot()
        {
            Client = new DiscordSocketClient(new DiscordSocketConfig
            {
                LogLevel = LogSeverity.Debug,
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.MessageContent,
                MessageCacheSize = 200,
            });
            Client.Log += Log;
            Client.Ready += Ready;
            Client.ChannelDestroyed += ChannelDestroyed;
            Client.JoinedGuild += JoinedGuild;
            Client.LeftGuild += LeftGuild;
            Client.InteractionCreated += InteractionCreated;
            Client.MessageReceived += MessageReceived;
            Client.MessageUpdated += MessageUpdated;
            Interactions = new(Client);
            Services = new ServiceCollection().BuildServiceProvider();
        }

        public async Task BotStart()
        {
            try
            {
                await Interactions.AddModulesAsync(Assembly.GetEntryAssembly(), Services);
                await Client.LoginAsync(TokenType.Bot, DiscordDiceBot.BotStart.Configuration["discord_token"]);
                await Client.StartAsync();
            }
            catch (Exception e)
            {
                LocalConsole.Log(LogLevel.Critical, LogSource.Create("DiscordBot", "Start"), "Cannnot start bot.", e);
                Environment.Exit(1);
            }
        }
        public async Task BotStop()
        {
            try
            {
                await Client.StopAsync();
                await Client.LogoutAsync();
            }
            catch (Exception e)
            {
                LocalConsole.Log(LogLevel.Critical, LogSource.Create("DiscordBot", "Stop"), "Cannnot stop bot properly.", e);
            }
        }

        private async Task Ready()
        {
            if (!true) await Interactions.RegisterCommandsGloballyAsync();
        }

        private async Task ChannelDestroyed(SocketChannel channel)
        {
            if (channel is not IGuildChannel gc) return;

            using var context = new SettingContext();
            var setting = await context.Channel.FindAsync(gc.GuildId, gc.Id);
            if (setting != null)
            {
                context.Channel.Remove(setting);
                await context.SaveChangesAsync();
            }
        }

        private async Task JoinedGuild(SocketGuild guild)
        {
            using var context = new SettingContext();
            await context.Guild.AddAsync(new(guild.Id));
            await context.SaveChangesAsync();
        }
        private async Task LeftGuild(SocketGuild guild)
        {
            using var context = new SettingContext();
            var setting = await context.Guild.FindAsync(guild.Id);
            if (setting != null)
            {
                context.Guild.Remove(setting);
                await context.SaveChangesAsync();
            }
        }

        private async Task InteractionCreated(SocketInteraction interaction)
        {
            try
            {
                var context = new SocketInteractionContext(Client, interaction);
                await Interactions.ExecuteCommandAsync(context, Services);
                await Log(new(LogSeverity.Info, "Interaction", $"Interaction process is Success. ID: {interaction.Id}"));
            }
            catch (Exception ex)
            {
                await Log(new(LogSeverity.Error, "Interaction", $"Interaction process throw an Exception. ID: {interaction.Id}", ex));
                await interaction.RespondAsync(embed: BotEmbed.UnknownError());
                if (interaction.Type == InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }

        private async Task MessageReceived(SocketMessage message)
        {
            if (message.Channel is not IGuildChannel gc || message.Author.IsBot) return;

            using var context = new SettingContext();
            var setting = await context.Channel.FindAsync(gc.GuildId, gc.Id);
            if (setting == null || !setting.IsEnabledMessageRoll || setting.DefaultSystem == null) return;

            var pattern = BCDice.Instance.GameSystems[setting.DefaultSystem].CommandPattern;
            if (Regex.IsMatch(message.Content, pattern))
            {
                var roll = await BCDice.Instance.Roll(setting.DefaultSystem, message.Content);
                if (roll != null)
                {
                    var embed = new EmbedBuilder()
                    {
                        Author = message.Author is IGuildUser gu ?
                            new() { Name = gu.Nickname ?? gu.Username, IconUrl = gu.GetGuildAvatarUrl() ?? gu.GetDisplayAvatarUrl() ?? gu.GetDefaultAvatarUrl() } :
                            new() { Name = message.Author.Username, IconUrl = message.Author.GetAvatarUrl() ?? message.Author.GetDefaultAvatarUrl() },
                        Description = roll.Text,
                        Footer = new() { Text = setting.DefaultSystem },
                        Timestamp = DateTimeOffset.Now,
                    };
                    await message.Channel.SendMessageAsync(embed: embed.Build(), messageReference: new(message.Id),
                        allowedMentions: new() { MentionRepliedUser = false });
                }
            }
        }

        private async Task MessageUpdated(Cacheable<IMessage, ulong> old, SocketMessage message, ISocketMessageChannel channel)
        {
            if (message.Author.Id == Client.CurrentUser.Id && message.Embeds.Count == 0 &&
                message.Components.Count == 0 && string.IsNullOrWhiteSpace(message.Content))
            {
                await message.DeleteAsync();
            }
        }

        private Task Log(LogMessage msg)
        {
            var level = msg.Severity switch
            {
                LogSeverity.Critical => LogLevel.Critical,
                LogSeverity.Error => LogLevel.Error,
                LogSeverity.Warning => LogLevel.Warning,
                LogSeverity.Info => LogLevel.Info,
                LogSeverity.Verbose => LogLevel.Info,
                LogSeverity.Debug => LogLevel.Debug,
                _ => LogLevel.Debug
            };
            var str = msg.Severity == LogSeverity.Verbose ? "Verbose : " : "";
            LocalConsole.Log(level, LogSource.Create("Discord", msg.Source), str + msg.Message, msg.Exception);
            return Task.CompletedTask;
        }
    }
}