using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Data;
using DrawingTrainer.Models;
using DrawingTrainer.Services;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace DrawingTrainer.ViewModels;

public partial class ExerciseItem : ObservableObject
{
    [ObservableProperty]
    private Tag? _selectedTag;

    [ObservableProperty]
    private int _durationMinutes = 2;

    [ObservableProperty]
    private int _durationSeconds;

    [ObservableProperty]
    private int _repeatCount = 1;

    public ObservableCollection<Tag> AvailableTags { get; set; } = [];
}

public partial class SessionPlannerViewModel : ObservableObject
{
    private readonly ISessionPlanService _planService;
    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableCollection<SessionPlan> _savedPlans = [];

    [ObservableProperty]
    private SessionPlan? _selectedPlan;

    [ObservableProperty]
    private string _planName = string.Empty;

    [ObservableProperty]
    private ObservableCollection<ExerciseItem> _exercises = [];

    [ObservableProperty]
    private ObservableCollection<Tag> _availableTags = [];

    [ObservableProperty]
    private bool _isEditing;

    private int? _editingPlanId;

    public SessionPlannerViewModel(
        ISessionPlanService planService,
        IDbContextFactory<DrawingTrainerDbContext> contextFactory,
        INavigationService navigationService)
    {
        _planService = planService;
        _contextFactory = contextFactory;
        _navigationService = navigationService;
        _ = LoadAsync();
    }

    [RelayCommand]
    private void NewPlan()
    {
        _editingPlanId = null;
        PlanName = string.Empty;
        Exercises.Clear();
        AddExercise();
        IsEditing = true;
    }

    [RelayCommand]
    private void AddExercise()
    {
        Exercises.Add(new ExerciseItem { AvailableTags = AvailableTags });
    }

    [RelayCommand]
    private void RemoveExercise(ExerciseItem exercise)
    {
        Exercises.Remove(exercise);
    }

    [RelayCommand]
    private async Task SavePlan()
    {
        if (string.IsNullOrWhiteSpace(PlanName)) return;

        var exerciseData = Exercises
            .Where(e => e.SelectedTag != null)
            .SelectMany(e => Enumerable.Repeat(
                (e.SelectedTag!.Id, e.DurationMinutes * 60 + e.DurationSeconds),
                Math.Max(1, e.RepeatCount)))
            .ToList();

        if (exerciseData.Count == 0) return;

        if (_editingPlanId.HasValue)
        {
            await _planService.UpdatePlanAsync(_editingPlanId.Value, PlanName, exerciseData);
        }
        else
        {
            await _planService.CreatePlanAsync(PlanName, exerciseData);
        }

        IsEditing = false;
        await LoadPlansAsync();
    }

    [RelayCommand]
    private void CancelEdit()
    {
        IsEditing = false;
    }

    [RelayCommand]
    private void EditPlan(SessionPlan plan)
    {
        _editingPlanId = plan.Id;
        PlanName = plan.Name;
        Exercises.Clear();

        var ordered = plan.Exercises.OrderBy(e => e.SortOrder).ToList();
        for (int i = 0; i < ordered.Count;)
        {
            var current = ordered[i];
            int count = 1;
            while (i + count < ordered.Count
                && ordered[i + count].TagId == current.TagId
                && ordered[i + count].DurationSeconds == current.DurationSeconds)
            {
                count++;
            }

            Exercises.Add(new ExerciseItem
            {
                AvailableTags = AvailableTags,
                SelectedTag = AvailableTags.FirstOrDefault(t => t.Id == current.TagId),
                DurationMinutes = current.DurationSeconds / 60,
                DurationSeconds = current.DurationSeconds % 60,
                RepeatCount = count
            });

            i += count;
        }

        IsEditing = true;
    }

    [RelayCommand]
    private async Task DeletePlan(SessionPlan plan)
    {
        await _planService.DeletePlanAsync(plan.Id);
        await LoadPlansAsync();
    }

    [RelayCommand]
    private void StartSession(SessionPlan plan)
    {
        _navigationService.NavigateTo<ActiveSessionViewModel>(vm => vm.Initialize(plan));
    }

    private async Task LoadAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var tags = await context.Tags.OrderBy(t => t.Name).ToListAsync();
        AvailableTags = new ObservableCollection<Tag>(tags);
        await LoadPlansAsync();
    }

    private async Task LoadPlansAsync()
    {
        var plans = await _planService.GetAllPlansAsync();
        SavedPlans = new ObservableCollection<SessionPlan>(plans);
    }
}
