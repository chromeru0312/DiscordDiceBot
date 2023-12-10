using System.Text.Json;

namespace DiscordDiceBot.Dice
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
            var res = await Client.GetAsync($"{BotStart.Configuration["bcdice_url"]}/v2/game_system");
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
            var res = await Client.GetAsync($"{BotStart.Configuration["bcdice_url"]}/v2/game_system/{id}");
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

            var res = await Client.GetAsync($"{BotStart.Configuration["bcdice_url"]}/v2/game_system/{id}");
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
            var res = await Client.PostAsync($"{BotStart.Configuration["bcdice_url"]}/v2/game_system/{id}/roll", req);
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
}