using Microsoft.EntityFrameworkCore;
using Finance.Api.Models;

namespace Finance.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Paiement> Paiements => Set<Paiement>();
    public DbSet<Scolarite> Scolarites => Set<Scolarite>();
}