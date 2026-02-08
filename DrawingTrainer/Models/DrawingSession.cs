namespace DrawingTrainer.Models;

public class DrawingSession
{
    public int Id { get; set; }
    public int SessionPlanId { get; set; }
    public SessionPlan SessionPlan { get; set; } = null!;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool IsCompleted { get; set; }
    public List<SessionExerciseResult> ExerciseResults { get; set; } = [];
}
