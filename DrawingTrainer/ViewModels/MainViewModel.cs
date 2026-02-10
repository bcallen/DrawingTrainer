using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Services;

namespace DrawingTrainer.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly INavigationService _navigationService;

    [ObservableProperty]
    private ObservableObject? _currentViewModel;

    public MainViewModel(INavigationService navigationService)
    {
        _navigationService = navigationService;
        _navigationService.CurrentViewModelChanged += () =>
            CurrentViewModel = _navigationService.CurrentViewModel;

        // Navigate to library on start
        _navigationService.NavigateTo<LibraryViewModel>();
    }

    [RelayCommand]
    private void NavigateToLibrary() => _navigationService.NavigateTo<LibraryViewModel>();

    [RelayCommand]
    private void NavigateToImport() => _navigationService.NavigateTo<PhotoImportViewModel>();

    [RelayCommand]
    private void NavigateToPlanner() => _navigationService.NavigateTo<SessionPlannerViewModel>();

    [RelayCommand]
    private void NavigateToGallery() => _navigationService.NavigateTo<GalleryViewModel>();

    [RelayCommand]
    private void NavigateToArtists() => _navigationService.NavigateTo<ArtistManagementViewModel>();
}
