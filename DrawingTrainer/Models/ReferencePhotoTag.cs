namespace DrawingTrainer.Models;

public class ReferencePhotoTag
{
    public int ReferencePhotoId { get; set; }
    public ReferencePhoto ReferencePhoto { get; set; } = null!;
    public int TagId { get; set; }
    public Tag Tag { get; set; } = null!;
}
