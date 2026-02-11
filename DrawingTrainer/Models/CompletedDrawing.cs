namespace DrawingTrainer.Models;

public class CompletedDrawing
{
    public int Id { get; set; }

    // Session-based drawing (null for manual uploads)
    public int? SessionExerciseResultId { get; set; }
    public SessionExerciseResult? SessionExerciseResult { get; set; }

    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }

    // Direct fields for manual uploads
    public int? TagId { get; set; }
    public Tag? Tag { get; set; }
    public int DurationSeconds { get; set; }
    public DateTime? DrawnAt { get; set; }
    public int? ReferencePhotoId { get; set; }
    public ReferencePhoto? ReferencePhoto { get; set; }

    public int? ArtistId { get; set; }
    public Artist? Artist { get; set; }
}
