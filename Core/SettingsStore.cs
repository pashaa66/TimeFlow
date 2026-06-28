using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TimeFlow
{
    public class SettingsStore
    {
        private readonly Dictionary<string, string> _data = new();
        private readonly string _path;

        public SettingsStore()
        {
            _path = Config.SettingsFile;
            SetDefaults();
            Load();
        }

        private void SetDefaults()
        {
            _data["hourly_rate"] = "500";
            _data["currency"] = "₽";
            _data["pomodoro_work_min"] = "25";
            _data["pomodoro_break_min"] = "5";
            _data["pomodoro_long_break_min"] = "15";
            _data["pomodoro_cycles_until_long"] = "4";
            _data["virtual_time_enabled"] = "false";
            _data["virtual_time_ratio"] = "2";
            _data["categories"] = JsonSerializer.Serialize(Config.DefaultCategories);
        }

        public string Get(string key, string fallback = null)
            => _data.TryGetValue(key, out var v) ? v : fallback;

        public double GetDouble(string key, double fallback = 0)
            => double.TryParse(Get(key, fallback.ToString()), out var v) ? v : fallback;

        public int GetInt(string key, int fallback = 0)
            => int.TryParse(Get(key, fallback.ToString()), out var v) ? v : fallback;

        public bool GetBool(string key, bool fallback = false)
        {
            var v = Get(key, fallback.ToString());
            return v == "true" || v == "1" || v == "True";
        }

        public void Set(string key, object value) => _data[key] = value.ToString();

        public Dictionary<string, string> Categories()
        {
            var raw = Get("categories", "{}");
            try { return JsonSerializer.Deserialize<Dictionary<string, string>>(raw) ?? new(); }
            catch { return new(Config.DefaultCategories); }
        }

        public string ColorFor(string category)
            => Categories().TryGetValue(category, out var c) ? c : "#999999";

        public bool AddCategory(string name, string color)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) return false;
            var cats = Categories();
            cats[name] = color;
            _data["categories"] = JsonSerializer.Serialize(cats);
            return Save();
        }

        public bool RemoveCategory(string name)
        {
            var cats = Categories();
            if (!cats.Remove(name)) return false;
            _data["categories"] = JsonSerializer.Serialize(cats);
            return Save();
        }

        public void Load()
        {
            if (!File.Exists(_path)) { Save(); return; }
            string currentSection = "";
            foreach (var line in File.ReadAllLines(_path))
            {
                string l = line.Trim();
                if (string.IsNullOrEmpty(l) || l.StartsWith(";") || l.StartsWith("#")) continue;
                if (l.StartsWith("[") && l.EndsWith("]")) { currentSection = l.Substring(1, l.Length - 2); continue; }
                int eq = l.IndexOf('=');
                if (eq < 0) continue;
                string key = l.Substring(0, eq).Trim();
                string val = l.Substring(eq + 1).Trim();
                _data[key] = val;
            }
            foreach (var kv in new Dictionary<string, string>(_data)) { }
        }

        public bool Save()
        {
            try
            {
                using var sw = new StreamWriter(_path, false, System.Text.Encoding.UTF8);
                sw.WriteLine("[Main]");
                sw.WriteLine($"hourly_rate={Get("hourly_rate")}");
                sw.WriteLine($"currency={Get("currency")}");
                sw.WriteLine();
                sw.WriteLine("[Pomodoro]");
                sw.WriteLine($"pomodoro_work_min={Get("pomodoro_work_min")}");
                sw.WriteLine($"pomodoro_break_min={Get("pomodoro_break_min")}");
                sw.WriteLine($"pomodoro_long_break_min={Get("pomodoro_long_break_min")}");
                sw.WriteLine($"pomodoro_cycles_until_long={Get("pomodoro_cycles_until_long")}");
                sw.WriteLine();
                sw.WriteLine("[VirtualTime]");
                sw.WriteLine($"virtual_time_enabled={Get("virtual_time_enabled")}");
                sw.WriteLine($"virtual_time_ratio={Get("virtual_time_ratio")}");
                sw.WriteLine();
                sw.WriteLine("[Categories]");
                sw.WriteLine($"items={Get("categories")}");
                return true;
            }
            catch { return false; }
        }
        
    }
}
