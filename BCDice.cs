using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordDiceBot
{
    public class BCDice
    {
        public static BCDice Instance { get; } = new();
        public IReadOnlyDictionary<string, GameSystemDetail> GameSystems { get; }
        private HttpClient Client { get; }

        private BCDice()
        {
            Client = new();
            GameSystems = GetGameSystemList().ConfigureAwait(false).GetAwaiter().GetResult();
            LocalConsole.Log(LogLevel.Info, LogSource.Create("BCDice"), $"Loaded {GameSystems.Count} game systems.");
            LocalConsole.Log(LogLevel.Info, LogSource.Create("BCDice"), "Completed Initialization.");
        }
        private async Task<Dictionary<string, GameSystemDetail>> GetGameSystemList()
        {
            var res = await Client.GetAsync($"{BotStart.BCDiceUrl}/v2/game_system");
            if (res == null)
            {
                LocalConsole.Log(LogLevel.Critical, LogSource.Create("BCDice"), "No response from BCDice API.");
                return new();
            }
            else
            {
                var content = await res.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                var objs = json.RootElement.GetProperty("game_system").EnumerateArray();
                var dic = new Dictionary<string, GameSystemDetail>();
                foreach (var obj in objs)
                {
                    var id = obj.GetProperty("id").GetString()!;
                    dic.Add(id, await GetGameSystemDetail(id));
                }
                return dic;
            }
        }
        private async Task<GameSystemDetail> GetGameSystemDetail(string id)
        {
            var res = await Client.GetAsync($"{BotStart.BCDiceUrl}/v2/game_system/{id}");
            if (res == null)
            {
                LocalConsole.Log(LogLevel.Critical, LogSource.Create("BCDice"), "No response from BCDice API.");
                return new();
            }
            else
            {
                var content = await res.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<GameSystemDetail>(content);
            }
        }

        public bool ExistSystemFromId(string id)
        {
            return GameSystems.ContainsKey(id);
        }
        public bool ExistSystemFromName(string name, out string id)
        {
            id = GameSystems.FirstOrDefault(g => g.Value.Name.Equals(name, StringComparison.CurrentCultureIgnoreCase)).Key ?? string.Empty;
            return !id.Equals(string.Empty);
        }

        public async Task<string?> GetHelpMessage(string id)
        {
            if (string.IsNullOrEmpty(id) || !ExistSystemFromId(id)) return null;

            var res = await Client.GetAsync($"{BotStart.BCDiceUrl}/v2/game_system/{id}");
            if (res == null)
            {
                LocalConsole.Log(LogLevel.Critical, LogSource.Create("BCDice"), "No response from BCDice API.");
                return null;
            }
            else
            {
                var content = await res.Content.ReadAsStringAsync();
                var json = JsonDocument.Parse(content);
                return json.RootElement.GetProperty("help_message").GetString();
            }
        }

        public async Task<RollResponse?> Roll(string id, string cmd)
        {
            if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(cmd) || !ExistSystemFromId(id))
                return null;

            var req = new FormUrlEncodedContent(new Dictionary<string, string>() { { "command", cmd } });
            var res = await Client.PostAsync($"{BotStart.BCDiceUrl}/v2/game_system/{id}/roll", req);
            if (res != null)
            {
                var content = await res.Content.ReadAsStringAsync();
                var roll = JsonSerializer.Deserialize<RollResponse>(content);
                return roll;
            }
            else
            {
                LocalConsole.Log(LogLevel.Critical, LogSource.Create("BCDice"), "No response from BCDice API.");
                return null;
            }
        }
    }

    public readonly struct GameSystemDetail
    {
        [JsonPropertyName("id")]
        public string Id { get; init; }
        [JsonPropertyName("name")]
        public string Name { get; init; }
        [JsonPropertyName("help_message")]
        public string HelpMessage { get; init; }
        [JsonPropertyName("command_pattern")]
        public string CommandPattern { get; init; }
    }

    public readonly struct RollSetting
    {
        [JsonPropertyName("default")]
        public string? DefaultSystem { get; init; }
        [JsonPropertyName("message_roll")]
        public bool IsEnabledMessageRoll { get; init; }
    }

    [Serializable]
    [JsonConverter(typeof(GuildSettingsConverter))]
    public class GuildSettings
    {
        public RollSetting Guild { get; }
        public IReadOnlyDictionary<ulong, RollSetting> Channel { get; }

        public GuildSettings()
        {
            Guild = new();
            Channel = new Dictionary<ulong, RollSetting>();
        }
        private GuildSettings(RollSetting guild, IReadOnlyDictionary<ulong, RollSetting> channel)
        {
            Guild = guild;
            Channel = channel;
        }

        public GuildSettings? SetDefaultSystem(string system)
        {
            string id;
            if (BCDice.Instance.ExistSystemFromId(system)) id = system;
            else if (!BCDice.Instance.ExistSystemFromName(system, out id)) return null;

            if (id == Guild.DefaultSystem) return null;
            else return new(new() { DefaultSystem = id, IsEnabledMessageRoll = Guild.IsEnabledMessageRoll }, Channel);
        }
        public GuildSettings? SetDefaultSystem(ulong channel, string? system)
        {
            var dic = new Dictionary<ulong, RollSetting>(Channel);
            bool suc;
            if (system != null)
            {
                string id;
                if (BCDice.Instance.ExistSystemFromId(system)) id = system;
                else if (!BCDice.Instance.ExistSystemFromName(system, out id)) return null;

                if (Channel.ContainsKey(channel))
                    dic[channel] = new() { DefaultSystem = id, IsEnabledMessageRoll = Channel[channel].IsEnabledMessageRoll };
                else dic.Add(channel, new() { DefaultSystem = id, IsEnabledMessageRoll = false });
                suc = true;
            }
            else suc = dic.Remove(channel);

            return suc ? new(Guild, dic) : null;
        }

        public GuildSettings? SetMessageRoll(bool enabled)
        {
            var changed = enabled ? (Guild.DefaultSystem != null && !Guild.IsEnabledMessageRoll) : Guild.IsEnabledMessageRoll;
            return changed ? new(new() { DefaultSystem = Guild.DefaultSystem, IsEnabledMessageRoll = false }, Channel) : null;
        }
        public GuildSettings? SetMessageRoll(ulong channel, bool enabled)
        {
            var dic = new Dictionary<ulong, RollSetting>(Channel);
            bool suc;
            if (Channel.ContainsKey(channel))
            {
                if (!enabled)
                {
                    if (Channel[channel].IsEnabledMessageRoll)
                    {
                        if (Guild.DefaultSystem != null) dic.Remove(channel);
                        else dic[channel] = new() { DefaultSystem = dic[channel].DefaultSystem, IsEnabledMessageRoll = false };
                        suc = true;
                    }
                    else suc = false;
                }
                else suc = !Channel[channel].IsEnabledMessageRoll;
            }
            else
            {
                if (Guild.DefaultSystem != null)
                {
                    dic.Add(channel, new() { DefaultSystem = null, IsEnabledMessageRoll = enabled });
                    suc = true;
                }
                else suc = false;
            }

            return suc ? new(Guild, dic) : null;
        }

        public class GuildSettingsConverter : JsonConverter<GuildSettings>
        {
            public override GuildSettings? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var system = reader.GetNextValue<RollSetting>(options);
                var settings = reader.GetNextValue<Dictionary<ulong, RollSetting>>(options);

                reader.CheckEndToken();
                return new(system, settings ?? new());
            }

            public override void Write(Utf8JsonWriter writer, GuildSettings value, JsonSerializerOptions options)
            {
                writer.WriteStartObject();

                writer.WritePropertyName("guild");
                JsonSerializer.Serialize(writer, value.Guild, options);
                writer.WritePropertyName("channel");
                JsonSerializer.Serialize(writer, value.Channel, options);

                writer.WriteEndObject();
            }
        }
    }

    [Serializable]
    [JsonConverter(typeof(RollResponseConverter))]
    public class RollResponse
    {
        public string Text { get; }
        public bool IsSecret { get; }
        public bool IsSuccess { get; }
        public bool IsDecisive { get; init; }
        public IReadOnlyList<DiceResult> Results { get; }

        private RollResponse(string text, bool secret, bool success, bool decisive, IEnumerable<DiceResult> results)
        {
            Text = text;
            IsSecret = secret;
            IsSuccess = success;
            IsDecisive = decisive;
            Results = results.ToList();
        }

        public class RollResponseConverter : JsonConverter<RollResponse>
        {
            public override RollResponse? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var ok = reader.GetNextValue<bool>(options);
                var text = reader.GetNextValue<string>(options);
                var secret = reader.GetNextValue<bool>(options);
                var success = reader.GetNextValue<bool>(options);
                var failure = reader.GetNextValue<bool>(options);
                var critical = reader.GetNextValue<bool>(options);
                var fumble = reader.GetNextValue<bool>(options);
                var rands = reader.GetNextValue<List<DiceResult>>(options);

                reader.CheckEndToken();
                return ok && text != null && (success ? (!failure && !fumble) : !critical) && rands != null && rands.Any()
                    ? new(text, secret, success, critical || fumble, rands) : null;
            }

            public override void Write(Utf8JsonWriter writer, RollResponse value, JsonSerializerOptions options) { }
        }
    }

    [Serializable]
    [JsonConverter(typeof(DiceResultConverter))]
    public class DiceResult
    {
        public DiceType Type { get; }
        public int Sides { get; }
        public int Value { get; }

        private DiceResult(DiceType type, int sides, int value)
        {
            Type = type;
            Sides = sides;
            Value = value;
        }

        public class DiceResultConverter : JsonConverter<DiceResult>
        {
            public override DiceResult? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
            {
                reader.CheckStartToken();

                var kind = reader.GetNextValue<string>(options);
                var type = kind switch
                {
                    "normal" => DiceType.Normal,
                    "tens_d10" => DiceType.D10,
                    "d9" => DiceType.D9,
                    _ => DiceType.Undefined
                };
                var sides = reader.GetNextValue<int>(options);
                var value = reader.GetNextValue<int>(options);

                reader.CheckEndToken();
                return new(type, sides, value);
            }

            public override void Write(Utf8JsonWriter writer, DiceResult value, JsonSerializerOptions options) { }
        }
    }

    public enum DiceType
    {
        Normal, D10, D9, Undefined = -1
    }
}