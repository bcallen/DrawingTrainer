using CommunityToolkit.Mvvm.ComponentModel;

namespace DrawingTrainer.Services;

public interface INavigationService
{
    ObservableObject CurrentViewModel { get; }
    event Action? CurrentViewModelChanged;
    void NavigateTo<T>(Action<T>? configure = null) where T : ObservableObject;
}

public class NavigationService : INavigationService
{
    private readonly IServiceProvider _serviceProvider;
    private ObservableObject _currentViewModel = null!;

    public NavigationService(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public ObservableObject CurrentViewModel
    {
        get => _currentViewModel;
        private set
        {
            _currentViewModel = value;
            CurrentViewModelChanged?.Invoke();
        }
    }

    public event Action? CurrentViewModelChanged;

    public void NavigateTo<T>(Action<T>? configure = null) where T : ObservableObject
    {
        var viewModel = (T)_serviceProvider.GetService(typeof(T))!;
        configure?.Invoke(viewModel);
        CurrentViewModel = viewModel;
    }
}
