using System.Text.Json;
using System.Text.Json.Serialization;

namespace DiscordDiceBot.Dice
{
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
                return ok && text != null && (success ? !failure && !fumble : !critical) && rands != null && rands.Any()
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

    public static class JsonExtensions
    {
        public static void CheckStartToken(this ref Utf8JsonReader reader)
        {
            if (reader.TokenType != JsonTokenType.StartObject)
                throw new JsonException($"The start position of json is invalid. position:{reader.Position}");
            reader.Read();
        }
        public static bool IsEndToken(this ref Utf8JsonReader reader)
        {
            return reader.TokenType == JsonTokenType.EndObject;
        }
        public static void CheckEndToken(this ref Utf8JsonReader reader)
        {
            if (!reader.IsEndToken())
                throw new JsonException($"The end position of json is invalid. position:{reader.Position}");
        }
        public static T? GetNextValue<T>(this ref Utf8JsonReader reader, JsonSerializerOptions? options = null)
        {
            if (typeof(T) == typeof(DateTime))
                return (T)(object)DateTime.Parse(reader.GetNextValue<string>(options)!);

            reader.Read();
            var value = JsonSerializer.Deserialize<T>(ref reader, options);
            reader.Read();
            return value;
        }
    }
}