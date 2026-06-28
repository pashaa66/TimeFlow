using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace TimeFlow
{
    public class Session
    {
        public double Start { get; set; }
        public double End { get; set; }
        public double Vsec { get; set; }

        [JsonIgnore] public bool IsActive => End == 0;
    }

    public class TaskItem
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Category { get; set; }
        public int EstimatedMinutes { get; set; }
        public bool Done { get; set; }
        public string CreatedAt { get; set; }
        public string CompletedAt { get; set; }
        public List<Session> Sessions { get; set; } = new();

        [JsonIgnore] public bool Running => Sessions.Exists(s => s.End == 0);

        public TaskItem() { Id = Guid.NewGuid().ToString("N"); CreatedAt = Config.NowIso(); }

        public TaskItem(string name, string category, int estimatedMinutes)
        {
            Id = Guid.NewGuid().ToString("N");
            Name = name; Category = category; EstimatedMinutes = estimatedMinutes;
            CreatedAt = Config.NowIso();
        }

        public void StartSession()
        {
            if (Running) return;
            Sessions.Add(new Session { Start = DateTimeOffset.UtcNow.ToUnixTimeSeconds(), End = 0, Vsec = 0 });
        }

        public void StopSession(VirtualClock clock = null)
        {
            double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var s in Sessions)
            {
                if (s.End == 0)
                {
                    s.End = now;
                    double st = s.Start > 0 ? s.Start : s.End;
                    s.Vsec = clock != null ? clock.ElapsedVirtualSeconds(st) : s.End - st;
                }
            }
        }

        public double ElapsedSeconds(VirtualClock clock = null)
        {
            double total = 0;
            double now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            foreach (var s in Sessions)
            {
                if (s.End != 0) total += s.Vsec;
                else if (clock != null) total += clock.ElapsedVirtualSeconds(s.Start > 0 ? s.Start : now);
                else total += now - (s.Start > 0 ? s.Start : now);
            }
            return total;
        }
    }

}
