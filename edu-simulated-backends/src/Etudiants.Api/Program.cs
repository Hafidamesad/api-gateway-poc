using Microsoft.EntityFrameworkCore;
using Bogus;
using Etudiants.Api.Data;
using Etudiants.Api.Models;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks();

var app = builder.Build();

// --- Migration automatique + seed au démarrage (pratique en dev/prototype) ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Etudiants.Any())
    {
        var faker = new Faker<Etudiant>("fr")
            .RuleFor(e => e.Nom, f => f.Name.LastName())
            .RuleFor(e => e.Prenom, f => f.Name.FirstName())
            .RuleFor(e => e.Email, (f, e) => f.Internet.Email(e.Prenom, e.Nom))
            .RuleFor(e => e.Filiere, f => f.PickRandom("Génie Logiciel", "Réseaux", "Génie Civil", "Génie Industriel"))
            .RuleFor(e => e.Annee, f => f.Random.Int(1, 5))
            .RuleFor(e => e.DateInscription, f => f.Date.Past(3));

        var etudiants = faker.Generate(200);
        db.Etudiants.AddRange(etudiants);
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