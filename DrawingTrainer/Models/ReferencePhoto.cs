namespace DrawingTrainer.Models;

public class ReferencePhoto
{
    public int Id { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public DateTime ImportedAt { get; set; }
    public string? ThumbnailPath { get; set; }
    public List<ReferencePhotoTag> ReferencePhotoTags { get; set; } = [];
    public List<SessionExerciseResult> SessionExerciseResults { get; set; } = [];
}
