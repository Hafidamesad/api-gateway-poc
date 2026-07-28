using Microsoft.EntityFrameworkCore;
using Bogus;
using Academique.Api.Data;
using Academique.Api.Models;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureKestrel(options =>
{
    options.ConfigureHttpsDefaults(https =>
    {
        https.ServerCertificate = new X509Certificate2("certs/service.p12", "changeit");
        https.ClientCertificateMode = ClientCertificateMode.RequireCertificate;
        https.ClientCertificateValidation = (clientCert, chain, sslPolicyErrors) =>
        {
            var caCert = new X509Certificate2("certs/ca.crt");
            using var validationChain = new X509Chain();
            validationChain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            validationChain.ChainPolicy.CustomTrustStore.Add(caCert);
            validationChain.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
            bool isValid = validationChain.Build(clientCert);
            if (!isValid)
            {
                foreach (var status in validationChain.ChainStatus)
                {
                    Console.WriteLine($"Chain error: {status.StatusInformation}");
                }
            }
            return isValid;
        };
    });
    options.ListenAnyIP(5103, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

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

    if (!db.Notes.Any())
    {
        var matieres = new[] { "Algorithmique", "Bases de données", "Réseaux", "Systèmes d'exploitation", "Génie Logiciel" };
        var jours = new[] { "Lundi", "Mardi", "Mercredi", "Jeudi", "Vendredi" };
        var creneaux = new[] { "08:30-10:30", "10:45-12:45", "14:00-16:00", "16:15-18:15" };

        // On simule 50 étudiants (EtudiantId 1 à 50), avec quelques notes et créneaux chacun
        var noteFaker = new Faker<Note>("fr")
            .RuleFor(n => n.EtudiantId, f => f.Random.Int(1, 50))
            .RuleFor(n => n.Matiere, f => f.PickRandom(matieres))
            .RuleFor(n => n.Valeur, f => Math.Round(f.Random.Double(8, 20), 2))
            .RuleFor(n => n.Semestre, f => f.PickRandom("S1", "S2", "S3", "S4"));

        var edtFaker = new Faker<EmploiDuTemps>("fr")
            .RuleFor(e => e.EtudiantId, f => f.Random.Int(1, 50))
            .RuleFor(e => e.Jour, f => f.PickRandom(jours))
            .RuleFor(e => e.Creneau, f => f.PickRandom(creneaux))
            .RuleFor(e => e.Matiere, f => f.PickRandom(matieres))
            .RuleFor(e => e.Salle, f => $"Salle {f.Random.Int(100, 400)}");

        db.Notes.AddRange(noteFaker.Generate(300));
        db.EmploisDuTemps.AddRange(edtFaker.Generate(200));
        db.SaveChanges();
    }
}

// --- Pipeline HTTP ---
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();
