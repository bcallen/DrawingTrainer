using DrawingTrainer.Data;
using DrawingTrainer.Models;
using Microsoft.EntityFrameworkCore;

namespace DrawingTrainer.Services;

public interface ISessionPlanService
{
    Task<List<SessionPlan>> GetAllPlansAsync();
    Task<SessionPlan?> GetPlanAsync(int id);
    Task<SessionPlan> CreatePlanAsync(string name, List<(int tagId, int durationSeconds)> exercises);
    Task UpdatePlanAsync(int id, string name, List<(int tagId, int durationSeconds)> exercises);
    Task DeletePlanAsync(int id);
}

public class SessionPlanService : ISessionPlanService
{
    private readonly IDbContextFactory<DrawingTrainerDbContext> _contextFactory;

    public SessionPlanService(IDbContextFactory<DrawingTrainerDbContext> contextFactory)
    {
        _contextFactory = contextFactory;
    }

    public async Task<List<SessionPlan>> GetAllPlansAsync()
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SessionPlans
            .Include(sp => sp.Exercises)
                .ThenInclude(e => e.Tag)
            .OrderByDescending(sp => sp.CreatedAt)
            .ToListAsync();
    }

    public async Task<SessionPlan?> GetPlanAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        return await context.SessionPlans
            .Include(sp => sp.Exercises)
                .ThenInclude(e => e.Tag)
            .FirstOrDefaultAsync(sp => sp.Id == id);
    }

    public async Task<SessionPlan> CreatePlanAsync(string name, List<(int tagId, int durationSeconds)> exercises)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var plan = new SessionPlan
        {
            Name = name,
            CreatedAt = DateTime.Now,
            Exercises = exercises.Select((e, i) => new SessionExercise
            {
                TagId = e.tagId,
                DurationSeconds = e.durationSeconds,
                SortOrder = i
            }).ToList()
        };

        context.SessionPlans.Add(plan);
        await context.SaveChangesAsync();
        return plan;
    }

    public async Task UpdatePlanAsync(int id, string name, List<(int tagId, int durationSeconds)> exercises)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var plan = await context.SessionPlans
            .Include(sp => sp.Exercises)
            .FirstOrDefaultAsync(sp => sp.Id == id);

        if (plan == null) return;

        plan.Name = name;
        context.SessionExercises.RemoveRange(plan.Exercises);
        plan.Exercises = exercises.Select((e, i) => new SessionExercise
        {
            SessionPlanId = id,
            TagId = e.tagId,
            DurationSeconds = e.durationSeconds,
            SortOrder = i
        }).ToList();

        await context.SaveChangesAsync();
    }

    public async Task DeletePlanAsync(int id)
    {
        await using var context = await _contextFactory.CreateDbContextAsync();
        var plan = await context.SessionPlans
            .Include(sp => sp.Exercises)
            .FirstOrDefaultAsync(sp => sp.Id == id);

        if (plan == null) return;

        context.SessionExercises.RemoveRange(plan.Exercises);
        context.SessionPlans.Remove(plan);
        await context.SaveChangesAsync();
    }
}
