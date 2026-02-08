namespace DrawingTrainer.Models;

public class CompletedDrawing
{
    public int Id { get; set; }
    public int SessionExerciseResultId { get; set; }
    public SessionExerciseResult SessionExerciseResult { get; set; } = null!;
    public string FilePath { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public DateTime UploadedAt { get; set; }
}
