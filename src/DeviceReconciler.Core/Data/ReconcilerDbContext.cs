using DeviceReconciler.Core.Models;
using Microsoft.EntityFrameworkCore;

namespace DeviceReconciler.Core.Data;

public class ReconcilerDbContext : DbContext
{
    public ReconcilerDbContext(DbContextOptions<ReconcilerDbContext> options) : base(options) { }

    public DbSet<DeviceState> Devices => Set<DeviceState>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<DeviceState>().HasKey(d => d.DeviceId);
    }
}
