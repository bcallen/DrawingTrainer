using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DrawingTrainer.Models;
using DrawingTrainer.Services;
using System.Collections.ObjectModel;

namespace DrawingTrainer.ViewModels;

public partial class ArtistManagementViewModel : ObservableObject
{
    private readonly IArtistService _artistService;

    [ObservableProperty]
    private ObservableCollection<Artist> _artists = [];

    [ObservableProperty]
    private string _newArtistName = string.Empty;

    [ObservableProperty]
    private Artist? _editingArtist;

    [ObservableProperty]
    private string _editName = string.Empty;

    public ArtistManagementViewModel(IArtistService artistService)
    {
        _artistService = artistService;
        _ = LoadArtistsAsync();
    }

    [RelayCommand]
    private async Task AddArtist()
    {
        if (string.IsNullOrWhiteSpace(NewArtistName)) return;

        await _artistService.CreateArtistAsync(NewArtistName);
        NewArtistName = string.Empty;
        await LoadArtistsAsync();
    }

    [RelayCommand]
    private void StartEdit(Artist artist)
    {
        EditingArtist = artist;
        EditName = artist.Name;
    }

    [RelayCommand]
    private void CancelEdit()
    {
        EditingArtist = null;
        EditName = string.Empty;
    }

    [RelayCommand]
    private async Task SaveEdit()
    {
        if (EditingArtist == null || string.IsNullOrWhiteSpace(EditName)) return;

        await _artistService.UpdateArtistAsync(EditingArtist.Id, EditName);
        EditingArtist = null;
        EditName = string.Empty;
        await LoadArtistsAsync();
    }

    [RelayCommand]
    private async Task DeleteArtist(Artist artist)
    {
        await _artistService.DeleteArtistAsync(artist.Id);
        await LoadArtistsAsync();
    }

    private async Task LoadArtistsAsync()
    {
        var artists = await _artistService.GetAllArtistsAsync();
        Artists = new ObservableCollection<Artist>(artists);
    }
}
