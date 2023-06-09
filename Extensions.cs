using Discord;
using System.Text.Json;

namespace DiscordDiceBot
{
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

    public static class DiscordExtentions
    {
        public static bool Mentioned(this IMessage msg)
        {
            return msg.MentionedEveryone || msg.MentionedRoleIds.Count > 0;
        }
        public static bool MentionedHere(this IMessage msg)
        {
            return msg.MentionedEveryone && msg.Content.Contains("@here");
        }
        public static bool MentionedEveryone(this IMessage msg)
        {
            return msg.MentionedEveryone && msg.Content.Contains("@everyone");
        }
    }
}