using Microsoft.EntityFrameworkCore;
using Transport.Api.Models;

namespace Transport.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Trajet> Trajets => Set<Trajet>();
}