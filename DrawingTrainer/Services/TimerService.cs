using System.Windows.Threading;

namespace DrawingTrainer.Services;

public interface ITimerService
{
    TimeSpan Remaining { get; }
    bool IsRunning { get; }
    bool IsPaused { get; }
    event Action<TimeSpan>? Tick;
    event Action? Elapsed;
    void Start(TimeSpan duration);
    void Pause();
    void Resume();
    void Stop();
}

public class TimerService : ITimerService
{
    private readonly DispatcherTimer _timer;
    private DateTime _endTime;
    private TimeSpan _remainingWhenPaused;

    public TimerService()
    {
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        _timer.Tick += OnTimerTick;
    }

    public TimeSpan Remaining { get; private set; }
    public bool IsRunning => _timer.IsEnabled;
    public bool IsPaused { get; private set; }

    public event Action<TimeSpan>? Tick;
    public event Action? Elapsed;

    public void Start(TimeSpan duration)
    {
        IsPaused = false;
        Remaining = duration;
        _endTime = DateTime.Now + duration;
        _timer.Start();
    }

    public void Pause()
    {
        if (!IsRunning) return;
        IsPaused = true;
        _remainingWhenPaused = _endTime - DateTime.Now;
        if (_remainingWhenPaused < TimeSpan.Zero)
            _remainingWhenPaused = TimeSpan.Zero;
        _timer.Stop();
    }

    public void Resume()
    {
        if (!IsPaused) return;
        IsPaused = false;
        _endTime = DateTime.Now + _remainingWhenPaused;
        _timer.Start();
    }

    public void Stop()
    {
        IsPaused = false;
        _timer.Stop();
        Remaining = TimeSpan.Zero;
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        Remaining = _endTime - DateTime.Now;
        if (Remaining <= TimeSpan.Zero)
        {
            Remaining = TimeSpan.Zero;
            _timer.Stop();
            Tick?.Invoke(Remaining);
            Elapsed?.Invoke();
        }
        else
        {
            Tick?.Invoke(Remaining);
        }
    }
}
