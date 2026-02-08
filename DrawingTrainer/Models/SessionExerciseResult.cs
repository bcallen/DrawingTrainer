namespace DrawingTrainer.Models;

public class SessionExerciseResult
{
    public int Id { get; set; }
    public int DrawingSessionId { get; set; }
    public DrawingSession DrawingSession { get; set; } = null!;
    public int SessionExerciseId { get; set; }
    public SessionExercise SessionExercise { get; set; } = null!;
    public int ReferencePhotoId { get; set; }
    public ReferencePhoto ReferencePhoto { get; set; } = null!;
    public int SortOrder { get; set; }
    public bool WasSkipped { get; set; }
    public CompletedDrawing? CompletedDrawing { get; set; }
}
