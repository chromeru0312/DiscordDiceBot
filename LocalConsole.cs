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
        private static ConsoleStream? ConsoleWriter { get; set; }
        private static ConsoleColor DefaultColor { get; set; }

        private static Queue<LogData> LogQueue { get; } = new();
        private static Task ConsoleTask { get; set; } = Task.CompletedTask;

        internal static void CreateNewLogFile()
        {
            if (LogPath == null)
            {
                LogPath = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory)!, "logs");
                if (!Directory.Exists(LogPath)) Directory.CreateDirectory(LogPath);
                DefaultColor = IsDebug ? ConsoleColor.White : Console.ForegroundColor;
            }
            var path = Path.Combine(LogPath, $"log-{DateTime.Now:yyyyMMddHHmm}.log");
            ConsoleWriter = new ConsoleStream(new(path, true, Encoding.UTF8)) { AutoFlush = true };
            Console.SetOut(ConsoleWriter);
        }

        public static void Log(LogLevel level, LogSource source, string message, Exception? exception = null)
        {
            LogQueue.Enqueue(new(DateTime.Now, level, source, message, exception));
            if (ConsoleTask.IsCompleted)
            {
                ConsoleTask = new Task(LogInner);
                ConsoleTask.Start();
            }
        }
        private static void LogInner()
        {
            var tasks = new List<Task>();
            if (ConsoleWriter == null) return;
            while (LogQueue.Count > 0)
            {
                var data = LogQueue.Dequeue();
                var src = data.Source.ToString();
                if (string.IsNullOrWhiteSpace(src) && string.IsNullOrWhiteSpace(data.Message)) return;

                var text = $"{data.Date:yyyy/MM/dd HH:mm:ss.fff} [{src}]";
                var msg = string.IsNullOrWhiteSpace(data.Message) ? "(No message.)" : data.Message;
                var msgs = msg.Split('\n');
                var level = data.Level.ToString();
                while (level.Length < 8) level += " ";

                if (!IsDebug && data.Level == LogLevel.Debug)
                {
                    foreach (var log in msgs)
                        ConsoleWriter.FileStream.WriteLine($"{level} {text} {log}");
                }
                else
                {
                    var color = data.Level switch
                    {
                        LogLevel.Critical => ConsoleColor.DarkRed,
                        LogLevel.Error => ConsoleColor.Red,
                        LogLevel.Warning => ConsoleColor.Yellow,
                        LogLevel.Notice => ConsoleColor.DarkGreen,
                        LogLevel.Info => ConsoleColor.DarkCyan,
                        LogLevel.Debug => ConsoleColor.DarkGray,
                        _ => DefaultColor
                    };

                    foreach (var log in msgs)
                    {
                        Console.ForegroundColor = color;
                        Console.Write(level);

                        if (data.Level != LogLevel.Debug)
                            Console.ForegroundColor = DefaultColor;
                        Console.WriteLine($" {text} {log}");
                    }

                    var e = data.Exception;
                    if (e != null && (data.Level == LogLevel.Critical || data.Level == LogLevel.Error))
                    {
                        var error = new List<string>() { $"{e.GetType()} - {e.Message}" };
                        var st = e.StackTrace?.Split('\n');
                        if (st != null) error.AddRange(st);
                        foreach (var line in error)
                        {
                            Console.ForegroundColor = color;
                            Console.Write(level);

                            Console.ForegroundColor = DefaultColor;
                            Console.WriteLine($" {text} {line}");
                        }

                        var ex = e;
                        var count = 5;
                        var inner = new List<string>();
                        while (count >= 0 && ex.InnerException != null)
                        {
                            ex = ex.InnerException;
                            inner.Add($"--> {ex.GetType()} - {ex.Message}");
                            var trace = ex.StackTrace?.Split('\n').Select(s => $"    {s}");
                            if (trace != null) inner.AddRange(trace);
                            count--;
                            if (count == 0)
                            {
                                inner.Add("--> [and more inner exceptions...]");
                                count--;
                                break;
                            }
                        }
                        foreach (var line in inner)
                        {
                            ConsoleWriter.FileStream.WriteLine($"{level} {text} {line}");
                        }
                        error.AddRange(inner);
                        if (count < 5)
                        {
                            var count_str = count < 0 ? "6+" : $"{5 - count}";
                            var multi = count == 4 ? "" : "s";
                            Console.WriteLine($"--> [and {count_str} inner exception{multi}...]");
                        }
                        tasks.Add(BotStart.SendStatus(new($"{data.Source}\n{data.Message}", string.Join('\n', error), false)));
                    }
                }
                Console.ForegroundColor = DefaultColor;

                if (data.Level == LogLevel.Critical)
                {
                    ConsoleWriter.Close();
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

        private record LogData(DateTime Date, LogLevel Level, LogSource Source, string Message, Exception? Exception);
    }

    public enum LogLevel
    {
        Critical, Error, Warning, Notice, Info, Debug, Trace
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