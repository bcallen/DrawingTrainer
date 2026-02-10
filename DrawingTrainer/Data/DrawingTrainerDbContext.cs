using DrawingTrainer.Models;
using Microsoft.EntityFrameworkCore;

namespace DrawingTrainer.Data;

public class DrawingTrainerDbContext : DbContext
{
    public DrawingTrainerDbContext(DbContextOptions<DrawingTrainerDbContext> options)
        : base(options) { }

    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<ReferencePhoto> ReferencePhotos => Set<ReferencePhoto>();
    public DbSet<ReferencePhotoTag> ReferencePhotoTags => Set<ReferencePhotoTag>();
    public DbSet<SessionPlan> SessionPlans => Set<SessionPlan>();
    public DbSet<SessionExercise> SessionExercises => Set<SessionExercise>();
    public DbSet<DrawingSession> DrawingSessions => Set<DrawingSession>();
    public DbSet<SessionExerciseResult> SessionExerciseResults => Set<SessionExerciseResult>();
    public DbSet<CompletedDrawing> CompletedDrawings => Set<CompletedDrawing>();
    public DbSet<Artist> Artists => Set<Artist>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ReferencePhotoTag>()
            .HasKey(rpt => new { rpt.ReferencePhotoId, rpt.TagId });

        modelBuilder.Entity<ReferencePhotoTag>()
            .HasOne(rpt => rpt.ReferencePhoto)
            .WithMany(rp => rp.ReferencePhotoTags)
            .HasForeignKey(rpt => rpt.ReferencePhotoId);

        modelBuilder.Entity<ReferencePhotoTag>()
            .HasOne(rpt => rpt.Tag)
            .WithMany(t => t.ReferencePhotoTags)
            .HasForeignKey(rpt => rpt.TagId);

        modelBuilder.Entity<SessionExercise>()
            .HasOne(se => se.SessionPlan)
            .WithMany(sp => sp.Exercises)
            .HasForeignKey(se => se.SessionPlanId);

        modelBuilder.Entity<SessionExercise>()
            .HasOne(se => se.Tag)
            .WithMany()
            .HasForeignKey(se => se.TagId);

        modelBuilder.Entity<DrawingSession>()
            .HasOne(ds => ds.SessionPlan)
            .WithMany(sp => sp.DrawingSessions)
            .HasForeignKey(ds => ds.SessionPlanId);

        modelBuilder.Entity<SessionExerciseResult>()
            .HasOne(ser => ser.DrawingSession)
            .WithMany(ds => ds.ExerciseResults)
            .HasForeignKey(ser => ser.DrawingSessionId);

        modelBuilder.Entity<SessionExerciseResult>()
            .HasOne(ser => ser.SessionExercise)
            .WithMany()
            .HasForeignKey(ser => ser.SessionExerciseId);

        modelBuilder.Entity<SessionExerciseResult>()
            .HasOne(ser => ser.ReferencePhoto)
            .WithMany(rp => rp.SessionExerciseResults)
            .HasForeignKey(ser => ser.ReferencePhotoId);

        modelBuilder.Entity<CompletedDrawing>()
            .HasOne(cd => cd.SessionExerciseResult)
            .WithMany(ser => ser.CompletedDrawings)
            .HasForeignKey(cd => cd.SessionExerciseResultId)
            .IsRequired(false);

        modelBuilder.Entity<CompletedDrawing>()
            .HasOne(cd => cd.Tag)
            .WithMany()
            .HasForeignKey(cd => cd.TagId)
            .IsRequired(false);

        modelBuilder.Entity<CompletedDrawing>()
            .HasOne(cd => cd.ReferencePhoto)
            .WithMany()
            .HasForeignKey(cd => cd.ReferencePhotoId)
            .IsRequired(false);

        modelBuilder.Entity<CompletedDrawing>()
            .HasOne(cd => cd.Artist)
            .WithMany(a => a.CompletedDrawings)
            .HasForeignKey(cd => cd.ArtistId)
            .IsRequired(false);

        // Seed default tags
        modelBuilder.Entity<Tag>().HasData(
            new Tag { Id = 1, Name = "Portrait" },
            new Tag { Id = 2, Name = "Landscape" },
            new Tag { Id = 3, Name = "Architecture" },
            new Tag { Id = 4, Name = "Figure" },
            new Tag { Id = 5, Name = "Still Life" },
            new Tag { Id = 6, Name = "Animal" }
        );
    }
}
