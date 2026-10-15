using Microsoft.EntityFrameworkCore;

namespace Survsys.Data;

/// <summary>
/// The one way into the database. The schema is whatever the migrations in
/// this module say, and nothing outside this module creates or alters it.
/// </summary>
/// <remarks>
/// Entity configuration is not written here: it is picked up from every
/// <see cref="IEntityTypeConfiguration{TEntity}"/> in this assembly, so a new
/// entity arrives as its own configuration file rather than as another block
/// in a method that grows with the schema.
///
/// EF Core and SQL Server exceptions surface to the caller unwrapped — this
/// module never translates a failure into an HTTP concern, because it does
/// not know there is one.
/// </remarks>
public class SurvsysDbContext(DbContextOptions<SurvsysDbContext> options) : DbContext(options)
{
    public DbSet<Condition> Conditions => Set<Condition>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) =>
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(SurvsysDbContext).Assembly);
}
