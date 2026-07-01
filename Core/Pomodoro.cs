using System;

namespace TimeFlow
{
    public class PomodoroTimer
    {
        public const string PhaseWork = "work";
        public const string PhaseBreak = "break";
        public const string PhaseLongBreak = "long_break";

        private readonly SettingsStore _settings;
        private readonly VirtualClock _clock;
        private readonly System.Timers.Timer _timer;

        public bool Running { get; private set; }
        public string Phase { get; private set; } = PhaseWork;
        public int Remaining { get; private set; }

        private int _completedWorks;
        private int _workSec, _breakSec, _longBreakSec, _cyclesUntilLong;

        private double _realStartUnix = 0;
        private int _remainingAtStart = 0;

        public event Action<int> Tick;
        public event Action<string> PhaseChanged;
        public event Action<string> FinishedPhase;

        public PomodoroTimer(SettingsStore settings, VirtualClock clock)
        {
            _settings = settings;
            _clock = clock;
            _clock.ConfigChanged += OnClockConfigChanged;
            _timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (s, e) => OnTick();
            Configure();
            Remaining = _workSec;
        }

        private void OnClockConfigChanged()
        {
            if (!Running) return;

            double realNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            double realElapsed = realNow - _realStartUnix;

            Remaining = Math.Max(0, _remainingAtStart - (int)realElapsed);

            _realStartUnix = realNow;
            _remainingAtStart = Remaining;

            Tick?.Invoke(Remaining);
        }

        private void Configure()
        {
            _workSec = _settings.GetInt("pomodoro_work_min", 25) * 60;
            _breakSec = _settings.GetInt("pomodoro_break_min", 5) * 60;
            _longBreakSec = _settings.GetInt("pomodoro_long_break_min", 15) * 60;
            _cyclesUntilLong = _settings.GetInt("pomodoro_cycles_until_long", 4);
        }

        public void ReloadSettings()
        {
            bool wasRunning = Running;
            Configure();
            if (!wasRunning) Reset();
        }

        public void Reset()
        {
            _timer.Stop();
            Running = false;
            Phase = PhaseWork;
            Remaining = _workSec;
            _realStartUnix = 0;
            _remainingAtStart = 0;
            Tick?.Invoke(Remaining);
            PhaseChanged?.Invoke(Phase);
        }

        public void Start()
        {
            if (Running) return;
            Configure();
            if (Remaining <= 0) { Remaining = _workSec; Phase = PhaseWork; }
            Running = true;
            _realStartUnix = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            _remainingAtStart = Remaining;
            _timer.Start();
            Tick?.Invoke(Remaining);
            PhaseChanged?.Invoke(Phase);
        }

        public void Stop()
        {
            Running = false;
            _timer.Stop();
            Tick?.Invoke(Remaining);
        }

        private void OnTick()
        {
            double realNow = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            double realElapsed = realNow - _realStartUnix;
            double virtualElapsed = _clock.Enabled
                ? realElapsed * _clock.Ratio
                : realElapsed;

            Remaining = Math.Max(0, _remainingAtStart - (int)virtualElapsed);

            if (Remaining <= 0)
            {
                Remaining = 0;
                _timer.Stop();
                Running = false;
                AdvancePhase();
            }
            else Tick?.Invoke(Remaining);
        }

        private void AdvancePhase()
        {
            string cur = Phase;
            FinishedPhase?.Invoke(cur);
            if (cur == PhaseWork)
            {
                _completedWorks++;
                if (_completedWorks % _cyclesUntilLong == 0) { Phase = PhaseLongBreak; Remaining = _longBreakSec; }
                else { Phase = PhaseBreak; Remaining = _breakSec; }
            }
            else { Phase = PhaseWork; Remaining = _workSec; }
            Tick?.Invoke(Remaining);
            PhaseChanged?.Invoke(Phase);
        }
    }
}