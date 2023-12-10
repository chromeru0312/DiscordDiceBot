using System.Text;

namespace DiscordDiceBot
{
    public static class LocalConsole
    {
        public static string? LogPath { get; set; } = null;
        public static bool IsDebug { get; set; }
#if DEBUG
        = true;
#else
        = false;
#endif
        private static DateTime NextLogFileDate { get; set; } = DateTime.Today;
        private static ConsoleStream? ConsoleWriter { get; set; }
        private static ConsoleColor DefaultColor { get; set; }

        private static Queue<LogData> LogQueue { get; } = new();
        private static Task ConsoleTask { get; set; } = Task.CompletedTask;

        internal static void Init()
        {
            CreateNewLogFile();
            NextLogFileDate = DateTime.Today.AddDays(1);
        }
        private static void CreateNewLogFile()
        {
            if (LogPath == null)
            {
                LogPath = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory)!, "logs");
                if (!Directory.Exists(LogPath)) Directory.CreateDirectory(LogPath);
                DefaultColor = IsDebug ? ConsoleColor.White : Console.ForegroundColor;
            }

            var path = Path.Combine(LogPath, DateTime.Today.ToString("yyyyMMdd"));
            int no = 0;
            while (File.Exists(path + (no != 0 ? $"_{no}" : "") + ".log")) no++;
            ConsoleWriter = new ConsoleStream(new(path + (no != 0 ? $"_{no}" : "") + ".log", true, Encoding.UTF8)) { AutoFlush = true };
            Console.SetOut(ConsoleWriter);

            var log_info = "--- Log File Information ---\n" +
                $"Log Date: {DateTime.Today:yyyy-MM-dd}\n" +
                $"Mode: {(IsDebug ? "Debug" : "Release")}\n" +
                "--- End of Log File Information ---\n\n";
            ConsoleWriter.FileStream.WriteLine(log_info);
        }

        public static void Log(LogLevel level, LogSource source, string message, Exception? exception = null, bool mention = false)
        {
            LogQueue.Enqueue(new(DateTime.Now, level, source, message, exception, mention));
            if (ConsoleTask.IsCompleted)
            {
                ConsoleTask = new Task(LogInner);
                ConsoleTask.Start();
            }
        }
        private static void LogInner()
        {
            var tasks = new List<Task>();
            if (ConsoleWriter == null)
            {
                CreateNewLogFile();
                NextLogFileDate = DateTime.Today.AddDays(1);
            }
            while (LogQueue.Count > 0)
            {
                var data = LogQueue.Dequeue();
                var src = data.Source.ToString();
                if (string.IsNullOrWhiteSpace(src) && string.IsNullOrWhiteSpace(data.Message)) return;

                if (NextLogFileDate <= data.Date)
                {
                    CreateNewLogFile();
                    NextLogFileDate = DateTime.Today.AddDays(1);
                }
                var time = data.Date.ToString("HH:mm:ss.fff");
                var level = data.Level.ToString().ToUpper().PadRight(8);
                var source = $"[{src}]";
                var msgs = string.IsNullOrWhiteSpace(data.Message) ? new[] { "(No message.)" } : data.Message.Split('\n');

                if (data.Level != LogLevel.Debug || IsDebug)
                {
                    var color = data.Level switch
                    {
                        LogLevel.Critical => ConsoleColor.DarkRed,
                        LogLevel.Error => ConsoleColor.Red,
                        LogLevel.Warning => ConsoleColor.Yellow,
                        LogLevel.Info => ConsoleColor.DarkCyan,
                        LogLevel.Verbose => ConsoleColor.DarkGreen,
                        LogLevel.Debug => ConsoleColor.DarkGray,
                        _ => DefaultColor
                    };

                    foreach (var log in msgs)
                    {
                        Console.ForegroundColor = color;
                        Console.Write(level);

                        if (data.Level != LogLevel.Debug) Console.ForegroundColor = DefaultColor;
                        Console.WriteLine($" {time} {source} {log}");
                    }

                    if (data.Exception != null && (data.Level <= LogLevel.Warning))
                    {
                        var error_log = $"{data.Exception.GetType()} - {data.Exception.Message}\n{data.Exception.StackTrace}";
                        var ex = data.Exception;
                        var count = 5;
                        var inner = new List<string>();
                        while (count >= 0 && ex.InnerException != null)
                        {
                            ex = ex.InnerException;
                            error_log += $"\n--> {ex.GetType()} - {ex.Message}\n{ex.StackTrace?.Split('\n').Select(s => $"    {s}")}\n";
                            count--;
                            if (count == 0) error_log += "\n--> [and more inner exceptions...]";
                        }
                        Console.WriteLine($"--- Exception Dump ---\n{error_log}\n--- End of Exception Dump ---");
                        if (data.Level != LogLevel.Warning)
                            tasks.Add(BotStart.SendStatus(new($"{data.Source}\n{data.Message}", error_log, data.Level == LogLevel.Critical)));
                    }
                }
                Console.ForegroundColor = DefaultColor;

                if (data.Level == LogLevel.Critical)
                {
                    ConsoleWriter!.Close();
                    Environment.Exit(1);
                }
            }
            Task.WaitAll(tasks.ToArray());
        }

        internal class ConsoleStream : StreamWriter
        {
            public override bool AutoFlush
            {
                get => base.AutoFlush;
                set
                {
                    base.AutoFlush = value;
                    FileStream.AutoFlush = value;
                }
            }
            internal StreamWriter FileStream { get; }

            public ConsoleStream(StreamWriter file) : base(Console.OpenStandardOutput())
            {
                FileStream = file;
                Console.OutputEncoding = file.Encoding;
            }

            public override void Write(char value)
            {
                base.Write(value);
                FileStream.Write(value);
            }
            public override void Write(char[]? value)
            {
                base.Write(value);
                FileStream.Write(value);
            }
            public override void Write(string? value)
            {
                if (value != null) Write(value.ToCharArray());
            }
            public override void WriteLine(string? value)
            {
                base.WriteLine(value);
                FileStream.WriteLine(value);
            }

            public override void Flush()
            {
                base.Flush();
                FileStream.Flush();
            }
            public override void Close()
            {
                base.Close();
                FileStream.Close();
            }
            protected override void Dispose(bool disposing)
            {
                base.Dispose(disposing);
                if (disposing) FileStream.Dispose();
            }
        }

        private record LogData(DateTime Date, LogLevel Level, LogSource Source, string Message, Exception? Exception, bool Mention);
    }

    public enum LogLevel
    {
        Critical, Error, Warning, Info, Verbose, Debug, Trace
    }

    public class LogSource
    {
        public string Source { get; }
        public string? Place { get; }

        private LogSource(string source, string? place = null)
        {
            Source = source;
            Place = place;
        }

        public static LogSource Create(string source, string? place = null) => new(source, place);
        public static LogSource Create<T>(T _, string? place = null) => Create<T>(place);
        public static LogSource Create<T>(string? place = null) => new(typeof(T).Name, place);

        public override string ToString() => Source + (string.IsNullOrWhiteSpace(Place) ? string.Empty : $"({Place})");
    }
}