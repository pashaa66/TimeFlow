using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TimeFlow
{
    public class TaskManager
    {
        private readonly SettingsStore _settings;
        private readonly VirtualClock _clock;
        private List<TaskItem> _tasks = new();
        private List<HistoryRecord> _history = new();

        public event Action TasksChanged;
        public event Action<string> ActiveChanged;

        public VirtualClock Clock => _clock;
        public IReadOnlyList<TaskItem> Tasks => _tasks;
        public IReadOnlyList<HistoryRecord> History => _history;

        public TaskManager(SettingsStore settings, VirtualClock clock)
        {
            _settings = settings;
            _clock = clock;
            Load();
        }

        private void Load()
        {
            var tdata = JsonStore.Load(Config.TasksFile);
            if (tdata.ValueKind == JsonValueKind.Object && tdata.TryGetProperty("tasks", out var tasksArr))
                foreach (var t in tasksArr.EnumerateArray())
                    _tasks.Add(JsonSerializer.Deserialize<TaskItem>(t.GetRawText()));

            var hdata = JsonStore.Load(Config.HistoryFile);
            if (hdata.ValueKind == JsonValueKind.Object && hdata.TryGetProperty("history", out var histArr))
                foreach (var h in histArr.EnumerateArray())
                    _history.Add(JsonSerializer.Deserialize<HistoryRecord>(h.GetRawText()));
        }

        public void SaveTasks() =>
            JsonStore.Save(Config.TasksFile, new { tasks = _tasks }, encrypt: true);

        public void SaveHistory() =>
            JsonStore.Save(Config.HistoryFile, new { history = _history }, encrypt: true);

        public TaskItem AddTask(string name, string category, int estimatedMinutes)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) return null;
            var t = new TaskItem(name, category, estimatedMinutes < 0 ? 0 : estimatedMinutes);
            _tasks.Add(t); SaveTasks(); TasksChanged?.Invoke(); return t;
        }

        public bool RemoveTask(string id)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null) return false;
            _tasks.Remove(t);
            if (t.Running) t.StopSession(_clock);
            var r = HistoryRecord.FromTask(t, _clock, "removed");
            r.RemovedAt = Config.NowIso();
            _history.Add(r); SaveTasks(); SaveHistory();
            if (id == ActiveId()) ActiveChanged?.Invoke(null);
            TasksChanged?.Invoke(); return true;
        }

        public bool CompleteTask(string id)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null || t.Done) return false;
            if (t.Running) t.StopSession(_clock);
            t.Done = true; t.CompletedAt = Config.NowIso();
            _history.Add(HistoryRecord.FromTask(t, _clock, "done"));
            SaveTasks(); SaveHistory();
            if (id == ActiveId()) ActiveChanged?.Invoke(null);
            TasksChanged?.Invoke(); return true;
        }

        public bool UpdateTask(string id, string name, string category, int estimatedMinutes)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null) return false;
            if (!string.IsNullOrEmpty(name?.Trim())) t.Name = name.Trim();
            t.Category = category;
            t.EstimatedMinutes = estimatedMinutes < 0 ? 0 : estimatedMinutes;
            SaveTasks(); TasksChanged?.Invoke(); return true;
        }

        public TaskItem ActiveTask() => _tasks.FirstOrDefault(t => t.Running);
        public string ActiveId() => ActiveTask()?.Id;

        public void StartTask(string id)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null) return;
            t.StartSession(); SaveTasks();
            ActiveChanged?.Invoke(id); TasksChanged?.Invoke();
        }

        public void StopTask(string id)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null) return;
            t.StopSession(_clock); SaveTasks();
            ActiveChanged?.Invoke(null); TasksChanged?.Invoke();
        }

        public TaskItem Get(string id) => _tasks.FirstOrDefault(t => t.Id == id);

        public double EarningsToday()
        {
            double rate = _settings.GetDouble("hourly_rate", 0);
            return SecondsOnDate(Config.TodayStr()) / 3600.0 * rate;
        }

        public double EarningsMonth()
        {
            double rate = _settings.GetDouble("hourly_rate", 0);
            var now = DateTime.Today;
            double total = 0;
            foreach (var day in AllTrackedDays())
            {
                var d = ParseDate(day);
                if (d.HasValue && d.Value.Year == now.Year && d.Value.Month == now.Month)
                    total += SecondsOnDate(day);
            }
            return total / 3600.0 * rate;
        }

        public double[] SecondsByWeekday()
        {
            var totals = new double[7];
            foreach (var kv in SecondsPerDayAll())
            {
                var d = ParseDate(kv.Key);
                if (d.HasValue)
                    totals[(int)d.Value.DayOfWeek == 0 ? 6 : (int)d.Value.DayOfWeek - 1] += kv.Value;
            }
            return totals;
        }

        public Dictionary<string, double> SecondsByCategoryToday()
        {
            var result = new Dictionary<string, double>();
            string today = Config.TodayStr();
            foreach (var t in _tasks)
                foreach (var kv in SplitSessionsByDay(t.Sessions, _clock))
                    if (kv.Key == today)
                        result[t.Category] = result.GetValueOrDefault(t.Category, 0) + kv.Value;
            foreach (var h in _history)
                foreach (var kv in SplitSessionsByDay(h.Sessions, null))
                    if (kv.Key == today)
                        result[h.Category] = result.GetValueOrDefault(h.Category, 0) + kv.Value;
            return result;
        }

        public Dictionary<string, double> SecondsByCategoryAll()
        {
            var result = new Dictionary<string, double>();
            foreach (var t in _tasks)
            {
                double sec = t.ElapsedSeconds(_clock);
                if (sec > 0) result[t.Category] = result.GetValueOrDefault(t.Category, 0) + sec;
            }
            foreach (var h in _history)
                result[h.Category] = result.GetValueOrDefault(h.Category, 0) + h.TotalSeconds;
            return result;
        }

        private double SecondsOnDate(string day)
        {
            double total = 0;
            foreach (var t in _tasks)
                total += SplitSessionsByDay(t.Sessions, _clock).GetValueOrDefault(day, 0);
            foreach (var h in _history)
                total += SplitSessionsByDay(h.Sessions, null).GetValueOrDefault(day, 0);
            return total;
        }

        private Dictionary<string, double> SecondsPerDayAll()
        {
            var result = new Dictionary<string, double>();
            foreach (var t in _tasks)
                foreach (var kv in SplitSessionsByDay(t.Sessions, _clock))
                    result[kv.Key] = result.GetValueOrDefault(kv.Key, 0) + kv.Value;
            foreach (var h in _history)
                foreach (var kv in SplitSessionsByDay(h.Sessions, null))
                    result[kv.Key] = result.GetValueOrDefault(kv.Key, 0) + kv.Value;
            return result;
        }

        private List<string> AllTrackedDays() => SecondsPerDayAll().Keys.ToList();

        private static Dictionary<string, double> SplitSessionsByDay(List<Session> sessions, VirtualClock clock)
        {
            var result = new Dictionary<string, double>();
            foreach (var s in sessions)
            {
                if (s.Start <= 0) continue;
                var dt = DateTimeOffset.FromUnixTimeSeconds((long)s.Start).LocalDateTime;
                string day = dt.ToString("yyyy-MM-dd");
                double sec = s.End == 0 && clock != null ? clock.ElapsedVirtualSeconds(s.Start) : s.Vsec;
                result[day] = result.GetValueOrDefault(day, 0) + sec;
            }
            return result;
        }

        private static DateTime? ParseDate(string s)
        {
            if (string.IsNullOrEmpty(s)) return null;
            return DateTime.TryParse(s.Substring(0, Math.Min(10, s.Length)), out var d) ? d : (DateTime?)null;
        }
    }
}