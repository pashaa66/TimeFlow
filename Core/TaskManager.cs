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

        public void SaveTasks()
        {
            JsonStore.Save(Config.TasksFile, new { tasks = _tasks }, encrypt: true);
        }

        public void SaveHistory()
        {
            JsonStore.Save(Config.HistoryFile, new { history = _history }, encrypt: true);
        }

        public TaskItem AddTask(string name, string category, int estimatedMinutes)
        {
            name = name?.Trim();
            if (string.IsNullOrEmpty(name)) return null;
            int em = estimatedMinutes < 0 ? 0 : estimatedMinutes;
            var t = new TaskItem(name, category, em);
            _tasks.Add(t);
            SaveTasks();
            TasksChanged?.Invoke();
            return t;
        }

        public bool RemoveTask(string id)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null) return false;
            _tasks.Remove(t);
            if (t.Running) t.StopSession(_clock);
            var r = HistoryRecord.FromTask(t, _clock, "removed");
            r.RemovedAt = Config.NowIso();
            _history.Add(r);
            SaveTasks(); SaveHistory();
            if (id == ActiveId()) ActiveChanged?.Invoke(null);
            TasksChanged?.Invoke();
            return true;
        }

        public bool CompleteTask(string id)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null || t.Done) return false;
            if (t.Running) t.StopSession(_clock);
            t.Done = true;
            t.CompletedAt = Config.NowIso();
            _history.Add(HistoryRecord.FromTask(t, _clock, "done"));
            SaveTasks(); SaveHistory();
            if (id == ActiveId()) ActiveChanged?.Invoke(null);
            TasksChanged?.Invoke();
            return true;
        }

        public bool UpdateTask(string id, string name, string category, int estimatedMinutes)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null) return false;
            if (!string.IsNullOrEmpty(name?.Trim())) t.Name = name.Trim();
            t.Category = category;
            t.EstimatedMinutes = estimatedMinutes < 0 ? 0 : estimatedMinutes;
            SaveTasks();
            TasksChanged?.Invoke();
            return true;
        }


        public TaskItem ActiveTask() => _tasks.FirstOrDefault(t => t.Running);
        public string ActiveId() => ActiveTask()?.Id;

        public void StartTask(string id)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null) return;
            t.StartSession();
            SaveTasks();
            ActiveChanged?.Invoke(id);
            TasksChanged?.Invoke();
        }

        public void StopTask(string id)
        {
            var t = _tasks.FirstOrDefault(x => x.Id == id);
            if (t == null) return;
            t.StopSession(_clock);
            SaveTasks();
            ActiveChanged?.Invoke(null);
            TasksChanged?.Invoke();
        }

        public TaskItem Get(string id) => _tasks.FirstOrDefault(t => t.Id == id);
    }
}