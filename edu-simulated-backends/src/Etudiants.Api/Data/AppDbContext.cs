using Microsoft.EntityFrameworkCore;
using Etudiants.Api.Models;

namespace Etudiants.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
    public DbSet<Etudiant> Etudiants => Set<Etudiant>();
}