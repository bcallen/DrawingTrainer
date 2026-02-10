using DrawingTrainer.Data;
using DrawingTrainer.Models;
using Microsoft.EntityFrameworkCore;

namespace DrawingTrainer.Services;

public interface IArtistService
{
    Task<List<Artist>> GetAllArtistsAsync();
    Task<Artist> CreateArtistAsync(string name);
    Task UpdateArtistAsync(int id, string name);
    Task DeleteArtistAsync(int id);
}

public class ArtistService : IArtistService
{
    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;

    public ArtistService(IDbContextFactory<DrawingTrainerDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<Artist>> GetAllArtistsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.Artists.OrderBy(a => a.Name).ToListAsync();
    }

    public async Task<Artist> CreateArtistAsync(string name)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var artist = new Artist
        {
            Name = name.Trim(),
            CreatedAt = DateTime.Now
        };
        context.Artists.Add(artist);
        await context.SaveChangesAsync();
        return artist;
    }

    public async Task UpdateArtistAsync(int id, string name)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var artist = await context.Artists.FindAsync(id);
        if (artist == null) return;

        artist.Name = name.Trim();
        await context.SaveChangesAsync();
    }

    public async Task DeleteArtistAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();

        // Null out ArtistId on related drawings (don't cascade delete)
        var drawings = await context.CompletedDrawings
            .Where(cd => cd.ArtistId == id)
            .ToListAsync();
        foreach (var drawing in drawings)
            drawing.ArtistId = null;

        var artist = await context.Artists.FindAsync(id);
        if (artist != null)
            context.Artists.Remove(artist);

        await context.SaveChangesAsync();
    }
}
