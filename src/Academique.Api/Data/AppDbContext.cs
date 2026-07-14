using Microsoft.EntityFrameworkCore;
using Academique.Api.Models;

namespace Academique.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Note> Notes => Set<Note>();
    public DbSet<EmploiDuTemps> EmploisDuTemps => Set<EmploiDuTemps>();
}