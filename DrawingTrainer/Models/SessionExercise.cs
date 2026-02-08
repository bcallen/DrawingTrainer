namespace DrawingTrainer.Models;

public class SessionExercise
{
    public int Id { get; set; }
    public int SessionPlanId { get; set; }
    public SessionPlan SessionPlan { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
    public int DurationSeconds { get; set; }
    public int SortOrder { get; set; }
}
