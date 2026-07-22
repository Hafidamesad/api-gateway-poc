using Microsoft.EntityFrameworkCore;
using RhPaie.Api.Models;

namespace RhPaie.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Enseignant> Enseignants => Set<Enseignant>();
}