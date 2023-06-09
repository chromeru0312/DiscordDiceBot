using System.Text.Json;

namespace DiscordDiceBot
{
    public class DataManager
    {
        public static DataManager Instance { get; } = new();
        public string DataPath { get; }

        private DataManager()
        {
            DataPath = Path.Combine(Path.GetDirectoryName(AppContext.BaseDirectory)!, "data");
            if (!Directory.Exists(DataPath)) Directory.CreateDirectory(DataPath);
        }

        public bool StringSave(string id, string extention, string obj, bool update = false)
        {
            return StringSaveAsync(id, extention, obj, update).Result;
        }
        public async Task<bool> StringSaveAsync(string id, string extention, string obj, bool update = false)
        {
            return await DataSaveBase(id, extention, obj, update);
        }

        public bool DataSave<T>(string id, T obj, bool update = false, JsonSerializerOptions? options = null)
        {
            return DataSaveAsync(id, obj, update, options).Result;
        }
        public async Task<bool> DataSaveAsync<T>(string id, T obj, bool update = false, JsonSerializerOptions? options = null)
        {
            return await DataSaveBase(id, ".data", JsonSerializer.Serialize(obj, options), update);
        }

        private async Task<bool> DataSaveBase(string id, string extention, string obj, bool update = false)
        {
            var path = GetPathFromId(id, extention);
            var temp = Path.ChangeExtension(path, ".tmp");
            if (File.Exists(path) && !update) return false;

            try
            {
                var fs = new FileStream(temp, FileMode.Create, FileAccess.Write);
                var sw = new StreamWriter(fs);
                await sw.WriteAsync(obj);
                await sw.DisposeAsync();
                await fs.DisposeAsync();
                if (update) File.Delete(path);
                File.Move(temp, path);
                LocalConsole.Log(LogLevel.Info, LogSource.Create(this, "Save"), $"File Saved. ID:{id}.");
                return true;
            }
            catch (Exception e)
            {
                if (File.Exists(temp)) File.Delete(temp);
                LocalConsole.Log(LogLevel.Error, LogSource.Create(this, "Save"), $"File Saving is failed. ID:{id}.", e);
                return false;
            }
        }

        public string? StringLoad(string id, string extention)
        {
            if (TryStringLoad(id, extention, out var str))
            {
                return str;
            }
            else
            {
                LocalConsole.Log(LogLevel.Warning, LogSource.Create(this, "Load"), $"File is not found. ID:{id}.");
                return null;
            }
        }
        public bool TryStringLoad(string id, string extention, out string str)
        {
            return LoadBase(id, out str, extention);
        }
        public T DataLoad<T>(string id, JsonSerializerOptions? options = null) where T : new()
        {
            return DataLoadNullable<T>(id, options) ?? new();
        }
        public T? DataLoadNullable<T>(string id, JsonSerializerOptions? options = null)
        {
            if (TryDataLoadNullable(id, out T? obj, options))
            {
                return obj;
            }
            else
            {
                LocalConsole.Log(LogLevel.Warning, LogSource.Create(this, "Load"), $"File is not found. ID:{id}.");
                return default;
            }
        }
        public bool TryDataLoad<T>(string id, out T obj, JsonSerializerOptions? options = null) where T : new()
        {
            if (TryDataLoadNullable<T>(id, out var obj1, options))
            {
                obj = obj1!;
                return true;
            }
            else
            {
                obj = new();
                return false;
            }
        }
        public bool TryDataLoadNullable<T>(string id, out T? obj, JsonSerializerOptions? options = null)
        {
            if (LoadBase(id, out var str))
            {
                try
                {
                    obj = JsonSerializer.Deserialize<T>(str, options)!;
                    return true;
                }
                catch (Exception e)
                {
                    LocalConsole.Log(LogLevel.Error, LogSource.Create(this, "Load"), $"Data parsing failed. ID:{id}.", e);
                }
            }
            obj = default;
            return false;
        }
        private bool LoadBase(string id, out string obj, string? extention = null)
        {
            var path = GetPathFromId(id, extention ?? ".data");
            if (!File.Exists(path))
            {
                obj = string.Empty;
                return false;
            }

            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var sr = new StreamReader(fs);
            obj = sr.ReadToEnd();
            LocalConsole.Log(LogLevel.Info, LogSource.Create(this, "Load"), $"File Loaded. ID:{id}.");
            return true;
        }

        private string GetPathFromId(string id, string extention)
        {
            if (string.IsNullOrEmpty(id)) throw new ArgumentNullException(nameof(id));
            else if (string.IsNullOrEmpty(extention)) throw new ArgumentNullException(nameof(extention));
            if (!extention.StartsWith('.')) extention = '.' + extention;
            var split = id.Split('/');
            var path = DataPath;
            for (int i = 0; i < split.Length - 1; i++) path = Path.Combine(path, split[i]);
            if (!Directory.Exists(path)) Directory.CreateDirectory(path);
            return Path.Combine(path, $"{split[^1]}{extention}");
        }
    }
}