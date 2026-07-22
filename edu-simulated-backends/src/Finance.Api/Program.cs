using Microsoft.EntityFrameworkCore;
using Bogus;
using Finance.Api.Data;
using Finance.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks();

var app = builder.Build();

// --- Migration automatique + seed au démarrage ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Paiements.Any())
    {
        var statuts = new[] { "en attente", "validé", "rejeté" };
        var modes = new[] { "carte", "virement", "espèces" };

        var paiementFaker = new Faker<Paiement>("fr")
            .RuleFor(p => p.EtudiantId, f => f.Random.Int(1, 50))
            .RuleFor(p => p.Montant, f => Math.Round(f.Random.Decimal(500, 15000), 2))
            .RuleFor(p => p.DatePaiement, f => f.Date.Past(1))
            .RuleFor(p => p.Statut, f => f.PickRandom(statuts))
            .RuleFor(p => p.ModePaiement, f => f.PickRandom(modes));

        var scolariteFaker = new Faker<Scolarite>("fr")
            .RuleFor(s => s.EtudiantId, f => f.Random.Int(1, 50))
            .RuleFor(s => s.MontantDu, f => Math.Round(f.Random.Decimal(10000, 30000), 2))
            .RuleFor(s => s.MontantPaye, (f, s) => Math.Round(f.Random.Decimal(0, s.MontantDu), 2))
            .RuleFor(s => s.Echeance, f => f.Date.Future(1));

        db.Paiements.AddRange(paiementFaker.Generate(150));
        db.Scolarites.AddRange(scolariteFaker.Generate(50));
        db.SaveChanges();
    }
}

// --- Pipeline HTTP ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();