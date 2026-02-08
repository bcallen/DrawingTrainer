namespace DrawingTrainer.Models;

public class SessionPlan
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<SessionExercise> Exercises { get; set; } = [];
    public List<DrawingSession> DrawingSessions { get; set; } = [];
}
