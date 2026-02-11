using System.IO;
using DrawingTrainer.Data;
using DrawingTrainer.Models;
using Microsoft.EntityFrameworkCore;

namespace DrawingTrainer.Services;

public interface IDrawingSessionService
{
    Task<DrawingSession> StartSessionAsync(int sessionPlanId);
    Task<ReferencePhoto?> GetRandomPhotoForTagAsync(int tagId, int? excludePhotoId = null);
    Task<SessionExerciseResult> RecordExerciseResultAsync(int drawingSessionId, int sessionExerciseId, int referencePhotoId, int sortOrder, bool wasSkipped);
    Task CompleteSessionAsync(int drawingSessionId);
    Task<CompletedDrawing> UploadDrawingAsync(int sessionExerciseResultId, string filePath, int? artistId = null);
    Task<List<DrawingSession>> GetCompletedSessionsAsync();
    Task<DrawingSession?> GetSessionWithResultsAsync(int sessionId);
    Task<List<CompletedDrawing>> GetDrawingsAsync(int? tagId = null, int? artistId = null);
    Task<CompletedDrawing> UploadManualDrawingAsync(string filePath, int tagId, int durationSeconds, DateTime drawnAt, int? referencePhotoId, int? artistId = null);
    Task DeleteDrawingAsync(int drawingId);
}

public class DrawingSessionService : IDrawingSessionService
{
    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;
    private readonly IPhotoStorageService _storageService;
    private readonly Random _random = new();

    public DrawingSessionService(
        IDbContextFactory<DrawingTrainerDbContext> contextFactory,
        IPhotoStorageService storageService)
    {
        _contextFactory = contextFactory;
        _storageService = storageService;
    }

    public async Task<DrawingSession> StartSessionAsync(int sessionPlanId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var session = new DrawingSession
        {
            SessionPlanId = sessionPlanId,
            StartedAt = DateTime.Now,
            IsCompleted = false
        };

        context.DrawingSessions.Add(session);
        await context.SaveChangesAsync();
        return session;
    }

    public async Task<ReferencePhoto?> GetRandomPhotoForTagAsync(int tagId, int? excludePhotoId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.ReferencePhotos
            .Where(rp => rp.ReferencePhotoTags.Any(rpt => rpt.TagId == tagId));

        if (excludePhotoId.HasValue)
            query = query.Where(rp => rp.Id != excludePhotoId.Value);

        var photos = await query.ToListAsync();
        if (photos.Count == 0)
        {
            // Fall back to including excluded photo if it's the only one
            if (excludePhotoId.HasValue)
            {
                photos = await context.ReferencePhotos
                    .Where(rp => rp.ReferencePhotoTags.Any(rpt => rpt.TagId == tagId))
                    .ToListAsync();
            }
            if (photos.Count == 0) return null;
        }

        return photos[_random.Next(photos.Count)];
    }

    public async Task<SessionExerciseResult> RecordExerciseResultAsync(
        int drawingSessionId, int sessionExerciseId, int referencePhotoId, int sortOrder, bool wasSkipped)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var result = new SessionExerciseResult
        {
            DrawingSessionId = drawingSessionId,
            SessionExerciseId = sessionExerciseId,
            ReferencePhotoId = referencePhotoId,
            SortOrder = sortOrder,
            WasSkipped = wasSkipped
        };

        context.SessionExerciseResults.Add(result);
        await context.SaveChangesAsync();
        return result;
    }

    public async Task CompleteSessionAsync(int drawingSessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var session = await context.DrawingSessions.FindAsync(drawingSessionId);
        if (session == null) return;

        session.IsCompleted = true;
        session.CompletedAt = DateTime.Now;
        await context.SaveChangesAsync();
    }

    public async Task<CompletedDrawing> UploadDrawingAsync(int sessionExerciseResultId, string filePath, int? artistId = null)
    {
        var storedPath = await _storageService.StoreDrawingPhotoAsync(filePath);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var drawing = new CompletedDrawing
        {
            SessionExerciseResultId = sessionExerciseResultId,
            FilePath = storedPath,
            OriginalFileName = Path.GetFileName(filePath),
            UploadedAt = DateTime.Now,
            ArtistId = artistId
        };

        context.CompletedDrawings.Add(drawing);
        await context.SaveChangesAsync();
        return drawing;
    }

    public async Task<List<DrawingSession>> GetCompletedSessionsAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.DrawingSessions
            .Include(ds => ds.SessionPlan)
            .Include(ds => ds.ExerciseResults)
            .Where(ds => ds.IsCompleted)
            .OrderByDescending(ds => ds.CompletedAt)
            .ToListAsync();
    }

    public async Task<DrawingSession?> GetSessionWithResultsAsync(int sessionId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.DrawingSessions
            .Include(ds => ds.SessionPlan)
            .Include(ds => ds.ExerciseResults)
                .ThenInclude(er => er.ReferencePhoto)
            .Include(ds => ds.ExerciseResults)
                .ThenInclude(er => er.SessionExercise!)
                    .ThenInclude(se => se.Tag)
            .Include(ds => ds.ExerciseResults)
                .ThenInclude(er => er.CompletedDrawings)
                    .ThenInclude(cd => cd.Artist)
            .FirstOrDefaultAsync(ds => ds.Id == sessionId);
    }

    public async Task<List<CompletedDrawing>> GetDrawingsAsync(int? tagId = null, int? artistId = null)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var query = context.CompletedDrawings
            .Include(cd => cd.SessionExerciseResult)
                .ThenInclude(ser => ser!.ReferencePhoto)
            .Include(cd => cd.SessionExerciseResult)
                .ThenInclude(ser => ser!.SessionExercise!)
                    .ThenInclude(se => se.Tag)
            .Include(cd => cd.Tag)
            .Include(cd => cd.ReferencePhoto)
            .Include(cd => cd.Artist)
            .AsQueryable();

        if (tagId.HasValue)
        {
            query = query.Where(cd =>
                (cd.SessionExerciseResultId != null && cd.SessionExerciseResult!.SessionExercise != null && cd.SessionExerciseResult.SessionExercise.TagId == tagId.Value) ||
                (cd.SessionExerciseResultId == null && cd.TagId == tagId.Value));
        }

        if (artistId.HasValue)
        {
            query = query.Where(cd => cd.ArtistId == artistId.Value);
        }

        return await query.OrderByDescending(cd => cd.UploadedAt).ToListAsync();
    }

    public async Task<CompletedDrawing> UploadManualDrawingAsync(
        string filePath, int tagId, int durationSeconds, DateTime drawnAt, int? referencePhotoId, int? artistId = null)
    {
        var storedPath = await _storageService.StoreDrawingPhotoAsync(filePath);

        await using var context = await _contextFactory.CreateDbContextAsync();
        var drawing = new CompletedDrawing
        {
            FilePath = storedPath,
            OriginalFileName = Path.GetFileName(filePath),
            UploadedAt = DateTime.Now,
            TagId = tagId,
            DurationSeconds = durationSeconds,
            DrawnAt = drawnAt,
            ReferencePhotoId = referencePhotoId,
            ArtistId = artistId
        };

        context.CompletedDrawings.Add(drawing);
        await context.SaveChangesAsync();
        return drawing;
    }

    public async Task DeleteDrawingAsync(int drawingId)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var drawing = await context.CompletedDrawings.FindAsync(drawingId);
        if (drawing == null) return;

        context.CompletedDrawings.Remove(drawing);
        await context.SaveChangesAsync();

        // Delete the stored file
        if (!string.IsNullOrEmpty(drawing.FilePath) && File.Exists(drawing.FilePath))
        {
            try { File.Delete(drawing.FilePath); } catch { }
        }
    }
}
