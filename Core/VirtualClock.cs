using System;
using System.Timers;

namespace TimeFlow
{
 
    public class VirtualClock
    {
        public bool Enabled { get; private set; }
        public double Ratio { get; private set; } = 1.0;

        private double _realAnchor;
        private double _virtualOffset;
        private double _lastReal;
		private readonly System.Timers.Timer _timer;

        public event Action<double> Ticked;
        public event Action ConfigChanged;

        public VirtualClock()
        {
            _realAnchor = UnixNow();
            _lastReal = _realAnchor;
            _virtualOffset = 0;
			_timer = new System.Timers.Timer(1000);
            _timer.Elapsed += (s, e) => OnTick();
        }

        public void Configure(bool enabled, double ratio)
        {
            Accumulate();
            bool wasEnabled = Enabled;
            Enabled = enabled;
            Ratio = Math.Max(0.01, ratio);

            if (enabled)
            {
                if (!wasEnabled)
                {
                    _realAnchor = UnixNow();
                    _lastReal = _realAnchor;
                    _virtualOffset = 0;
                }
            }
            else
            {
                _realAnchor = UnixNow();
                _lastReal = _realAnchor;
                _virtualOffset = 0;
            }
            ConfigChanged?.Invoke();
        }

        public void Start()
        {
            if (!_timer.Enabled) { _timer.Start(); _lastReal = UnixNow(); }
        }

        public void Stop() => _timer.Stop();

        private void Accumulate()
        {
            double now = UnixNow();
            double dtReal = now - _lastReal;
            _lastReal = now;
            if (Enabled)
            {
                _virtualOffset += dtReal * (Ratio - 1.0);
            }
        }

        public double VirtualSecondsSinceEpoch()
        {
            Accumulate();
            return UnixNow() + _virtualOffset;
        }

        public double ElapsedVirtualSeconds(double sinceRealEpoch)
        {
            Accumulate();
            double interval = Math.Max(0, _lastReal - sinceRealEpoch);
            return Enabled ? interval * Ratio : interval;
        }

        private void OnTick()
        {
            Accumulate();
            Ticked?.Invoke(Enabled ? Ratio : 1.0);
        }

        public string NowDisplay()
        {
            double v = VirtualSecondsSinceEpoch();
            return DateTimeOffset.FromUnixTimeSeconds((long)v).LocalDateTime.ToString("HH:mm:ss");
        }

        private static double UnixNow() => DateTimeOffset.UtcNow.ToUnixTimeSeconds();
    }
}