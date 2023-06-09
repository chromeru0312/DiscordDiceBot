using Discord;
using Discord.Interactions;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System.Reflection;
using System.Text.RegularExpressions;

namespace DiscordDiceBot
{
    public class DiscordBot
    {
        public static DiscordBot Instance { get; } = new();
        public DiscordSocketClient Client { get; }
        public InteractionService Interactions { get; }
        public ServiceProvider Services { get; }
        public IReadOnlyDictionary<ulong, GuildSettings> Settings { get; private set; }

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
            Client.LeftGuild += LeftGuild;
            Client.InteractionCreated += InteractionCreated;
            Client.MessageReceived += MessageReceived;
            Client.MessageUpdated += MessageUpdated;
            Interactions = new(Client);
            Services = new ServiceCollection().BuildServiceProvider();
            Settings = new Dictionary<ulong, GuildSettings>();
        }

        public async Task BotStart()
        {
            try
            {
                await Interactions.AddModulesAsync(Assembly.GetEntryAssembly(), Services);
                await Client.LoginAsync(TokenType.Bot, DiscordDiceBot.BotStart.DiscordToken);
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

            if (DataManager.Instance.TryDataLoad<Dictionary<ulong, GuildSettings>>("GuildSettings", out var dic))
            {
                var diff = dic.Keys.Except(Client.Guilds.Select(g => g.Id));
                foreach (var id in diff)
                    dic.Remove(id);
                Settings = dic;
            }
        }

        internal bool SetGuildDefaultSystem(ulong guild, string system)
        {
            if (!Settings.TryGetValue(guild, out var setting))
                setting = new();

            var updated = setting.SetDefaultSystem(system);
            if (updated != null)
            {
                Settings = new Dictionary<ulong, GuildSettings>(Settings) { [guild] = updated };
                DataManager.Instance.DataSave("GuildSettings", Settings, true);
                return true;
            }
            else return false;
        }
        internal bool SetChannelDefaultSystem(ulong guild, ulong channel, string? system)
        {
            if (!Settings.TryGetValue(guild, out var setting))
                setting = new();

            var updated = setting.SetDefaultSystem(channel, system);
            if (updated != null)
            {
                Settings = new Dictionary<ulong, GuildSettings>(Settings) { [guild] = updated };
                DataManager.Instance.DataSave("GuildSettings", Settings, true);
                return true;
            }
            else return false;
        }

        public bool SetGuildMessageRoll(ulong guild, bool enabled)
        {
            if (!Settings.ContainsKey(guild)) return false;

            var updated = Settings[guild].SetMessageRoll(enabled);
            if (updated != null)
            {
                Settings = new Dictionary<ulong, GuildSettings>(Settings) { [guild] = updated };
                DataManager.Instance.DataSave("GuildSettings", Settings, true);
                return true;
            }
            else return false;
        }
        public bool SetChannelMessageRoll(ulong guild, ulong channel, bool enabled)
        {
            if (!Settings.ContainsKey(guild)) return false;

            var updated = Settings[guild].SetMessageRoll(channel, enabled);
            if (updated != null)
            {
                Settings = new Dictionary<ulong, GuildSettings>(Settings) { [guild] = updated };
                DataManager.Instance.DataSave("GuildSettings", Settings, true);
                return true;
            }
            else return false;
        }

        private Task ChannelDestroyed(SocketChannel channel)
        {
            if (channel is IGuildChannel gc && Settings.TryGetValue(gc.GuildId, out var setting))
            {
                var updated = setting.SetDefaultSystem(channel.Id, null);
                if (updated != null)
                {
                    Settings = new Dictionary<ulong, GuildSettings>(Settings) { [gc.GuildId] = updated };
                }
            }
            return Task.CompletedTask;
        }

        private Task LeftGuild(SocketGuild guild)
        {
            if (Settings.ContainsKey(guild.Id))
            {
                var dic = new Dictionary<ulong, GuildSettings>(Settings);
                dic.Remove(guild.Id);
                Settings = dic;
            }
            return Task.CompletedTask;
        }

        private async Task InteractionCreated(SocketInteraction interaction)
        {
            await Log(new LogMessage(LogSeverity.Info, "Interaction", $"Interaction[{interaction.Id}] process is started."));
            try
            {
                var context = new SocketInteractionContext(Client, interaction);
                await Interactions.ExecuteCommandAsync(context, Services);
                await Log(new LogMessage(LogSeverity.Info, "Interaction", $"Interaction[{interaction.Id}] process is Success."));
            }
            catch (Exception ex)
            {
                await Log(new LogMessage(LogSeverity.Error, "Interaction", $"Interaction[{interaction.Id}] process throws an Exception.", ex));
                await interaction.RespondAsync(embed: BotEmbed.UnknownError());
                if (interaction.Type == InteractionType.ApplicationCommand)
                    await interaction.GetOriginalResponseAsync().ContinueWith(async (msg) => await msg.Result.DeleteAsync());
            }
        }

        private async Task MessageReceived(SocketMessage message)
        {
            if (message.Channel is IGuildChannel gc && Settings.TryGetValue(gc.GuildId, out var settings))
            {
                string system;
                if (settings.Channel.TryGetValue(message.Channel.Id, out var setting) && setting.DefaultSystem != null)
                    system = setting.DefaultSystem;
                else if (settings.Guild.DefaultSystem != null)
                    system = settings.Guild.DefaultSystem;
                else return;

                var pattern = BCDice.Instance.GameSystems[system].CommandPattern;
                if (Regex.IsMatch(message.Content, pattern))
                {
                    var roll = await BCDice.Instance.Roll(system, message.Content);
                    if (roll != null)
                    {
                        var embed = new EmbedBuilder()
                        {
                            Author = message.Author is IGuildUser gu ?
                                new() { Name = gu.Nickname ?? gu.Username, IconUrl = gu.GetGuildAvatarUrl() ?? gu.GetDisplayAvatarUrl() ?? gu.GetDefaultAvatarUrl() } :
                                new() { Name = message.Author.Username, IconUrl = message.Author.GetAvatarUrl() ?? message.Author.GetDefaultAvatarUrl() },
                            Description = roll.Text,
                            Footer = new() { Text = system },
                            Timestamp = DateTimeOffset.Now,
                        };
                        await message.Channel.SendMessageAsync(embed: embed.Build(), messageReference: new(message.Id));
                    }
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