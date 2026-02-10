namespace DrawingTrainer.Models;

public class Artist
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public List<CompletedDrawing> CompletedDrawings { get; set; } = [];
}
