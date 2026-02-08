using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Models;
using DrawingTrainer.Services;

namespace DrawingTrainer.ViewModels;

public enum SessionState
{
    Idle,
    Drawing,
    Break,
    Complete
}

public partial class ActiveSessionViewModel : ObservableObject
{
    private readonly IDrawingSessionService _sessionService;
    private readonly ITimerService _timerService;
    private readonly INavigationService _navigationService;

    private SessionPlan _plan = null!;
    private DrawingSession _session = null!;
    private List<SessionExercise> _exercises = [];
    private int _currentExerciseIndex;
    private int? _currentPhotoId;
    private int _resultSortOrder;

    [ObservableProperty]
    private SessionState _state = SessionState.Idle;

    [ObservableProperty]
    private string _currentPhotoPath = string.Empty;

    [ObservableProperty]
    private string _timerText = "00:00";

    [ObservableProperty]
    private double _timerProgress;

    [ObservableProperty]
    private string _exerciseInfo = string.Empty;

    [ObservableProperty]
    private string _categoryName = string.Empty;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private int _breakSecondsRemaining;

    private TimeSpan _currentExerciseDuration;

    public ActiveSessionViewModel(
        IDrawingSessionService sessionService,
        ITimerService timerService,
        INavigationService navigationService)
    {
        _sessionService = sessionService;
        _timerService = timerService;
        _navigationService = navigationService;

        _timerService.Tick += OnTimerTick;
        _timerService.Elapsed += OnTimerElapsed;
    }

    public void Initialize(SessionPlan plan)
    {
        _plan = plan;
        _exercises = plan.Exercises.OrderBy(e => e.SortOrder).ToList();
        _currentExerciseIndex = 0;
        _resultSortOrder = 0;
        _ = StartSessionAsync();
    }

    [RelayCommand]
    private void PauseResume()
    {
        if (IsPaused)
        {
            _timerService.Resume();
            IsPaused = false;
        }
        else
        {
            _timerService.Pause();
            IsPaused = true;
        }
    }

    [RelayCommand]
    private async Task Skip()
    {
        if (State != SessionState.Drawing) return;

        // Record as skipped
        var exercise = _exercises[_currentExerciseIndex];
        if (_currentPhotoId.HasValue)
        {
            await _sessionService.RecordExerciseResultAsync(
                _session.Id, exercise.Id, _currentPhotoId.Value, _resultSortOrder++, wasSkipped: true);
        }

        // Get new photo, timer continues
        var photo = await _sessionService.GetRandomPhotoForTagAsync(exercise.TagId, _currentPhotoId);
        if (photo != null)
        {
            _currentPhotoId = photo.Id;
            CurrentPhotoPath = photo.FilePath;
        }
    }

    [RelayCommand]
    private async Task EndSession()
    {
        _timerService.Stop();
        await _sessionService.CompleteSessionAsync(_session.Id);
        State = SessionState.Complete;

        _navigationService.NavigateTo<PostSessionViewModel>(vm => vm.Initialize(_session.Id));
    }

    private async Task StartSessionAsync()
    {
        _session = await _sessionService.StartSessionAsync(_plan.Id);
        await StartExerciseAsync();
    }

    private async Task StartExerciseAsync()
    {
        if (_currentExerciseIndex >= _exercises.Count)
        {
            await EndSession();
            return;
        }

        var exercise = _exercises[_currentExerciseIndex];
        _currentExerciseDuration = TimeSpan.FromSeconds(exercise.DurationSeconds);

        CategoryName = exercise.Tag?.Name ?? "Unknown";
        ExerciseInfo = $"Exercise {_currentExerciseIndex + 1} of {_exercises.Count}";

        var photo = await _sessionService.GetRandomPhotoForTagAsync(exercise.TagId);
        if (photo != null)
        {
            _currentPhotoId = photo.Id;
            CurrentPhotoPath = photo.FilePath;
        }
        else
        {
            CurrentPhotoPath = string.Empty;
            _currentPhotoId = null;
        }

        State = SessionState.Drawing;
        _timerService.Start(_currentExerciseDuration);
    }

    private void OnTimerTick(TimeSpan remaining)
    {
        TimerText = $"{(int)remaining.TotalMinutes:D2}:{remaining.Seconds:D2}";

        if (State == SessionState.Drawing && _currentExerciseDuration.TotalSeconds > 0)
        {
            TimerProgress = 1.0 - (remaining.TotalSeconds / _currentExerciseDuration.TotalSeconds);
        }
        else if (State == SessionState.Break)
        {
            BreakSecondsRemaining = (int)Math.Ceiling(remaining.TotalSeconds);
        }
    }

    private async void OnTimerElapsed()
    {
        if (State == SessionState.Drawing)
        {
            // Record result
            var exercise = _exercises[_currentExerciseIndex];
            if (_currentPhotoId.HasValue)
            {
                await _sessionService.RecordExerciseResultAsync(
                    _session.Id, exercise.Id, _currentPhotoId.Value, _resultSortOrder++, wasSkipped: false);
            }

            _currentExerciseIndex++;

            if (_currentExerciseIndex < _exercises.Count)
            {
                // Start break
                State = SessionState.Break;
                _timerService.Start(TimeSpan.FromSeconds(30));
            }
            else
            {
                await EndSession();
            }
        }
        else if (State == SessionState.Break)
        {
            await StartExerciseAsync();
        }
    }
}
