using DiscordDiceBot.Dice;
using DiscordDiceBot.Discord;
using Microsoft.Extensions.Configuration;
using System.Globalization;
using System.Net.Http.Json;
using System.Text;
using Timer = System.Timers.Timer;

namespace DiscordDiceBot
{
    public static class BotStart
    {
        internal static HttpClient Client { get; } = new();
        internal static Timer Timer { get; } = new(60000);
        internal static IConfigurationRoot Configuration { get; }
            = new ConfigurationBuilder()
                .AddJsonFile("appsettings.json")
#if DEBUG
                .AddJsonFile("appsettings.Development.json")
#endif
                .Build();
        private static bool Exit { get; set; } = false;

        public static void Main()
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            CultureInfo.CurrentCulture = new(1041);
            LocalConsole.Init();
            Console.CancelKeyPress += CancelKeyPress;
#if DEBUG
            LocalConsole.IsDebug = true;
#endif

            _ = BCDice.Instance;
            DiscordBot.Instance.BotStart().Wait();
            Timer.Elapsed += async (s, e) => await SendStatus();
            Task.Delay(1000 - DateTime.Now.Millisecond).Wait();
            Timer.Start();
            while (!Exit) { Task.Delay(1000).Wait(); }
        }

        private static void CancelKeyPress(object? sender, ConsoleCancelEventArgs e)
        {
            DiscordBot.Instance.BotStop().Wait();
            try
            {
                SendStatus(new("Application stopped.", null, true)).Wait();
            }
            catch { }
            e.Cancel = Exit = true;
        }

        internal static async Task SendStatus(ErrorDetail? error = null)
        {
            try
            {
                await Client.PostAsJsonAsync(Configuration["status_url"], new StatusRequest(error));
            }
            catch { }
        }
    }

    public readonly struct StatusRequest
    {
        public string AppName { get; }
        public ErrorDetail? Error { get; }

        public StatusRequest(ErrorDetail? error = null)
        {
            AppName = "DiscordDiceBot";
            Error = error;
        }
    }
    public readonly struct ErrorDetail
    {
        public string Message { get; }
        public string? Log { get; }
        public bool IsExit { get; }

        public ErrorDetail(string message, string? log, bool exit)
        {
            Message = message;
            Log = log;
            IsExit = exit;
        }
    }
}