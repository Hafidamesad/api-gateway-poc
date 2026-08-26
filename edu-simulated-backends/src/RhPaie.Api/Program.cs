using Microsoft.EntityFrameworkCore;
using SoapCore;
using Bogus;
using RhPaie.Api.Data;
using RhPaie.Api.Models;
using RhPaie.Api.Services;
using RhPaie.Api.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using StackExchange.Redis;

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
    options.ListenAnyIP(5105, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

// --- Services ---
builder.Services.AddSoapCore();
builder.Services.AddScoped<IRhPaieService, RhPaieService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks();

// ============================================================
// Redis
// ============================================================

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = builder.Configuration["REDIS_CONNECTION"]
        ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION")
        ?? "localhost:6379";

    var options = ConfigurationOptions.Parse(config);
    options.AbortOnConnectFail = false;

    return ConnectionMultiplexer.Connect(options);
});

var app = builder.Build();

// --- Migration automatique + seed au démarrage ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();

    if (!db.Enseignants.Any())
    {
        var grades = new[] { "Professeur Assistant", "Professeur Habilité", "Professeur de l'Enseignement Supérieur" };

        var faker = new Faker<Enseignant>("fr")
            .RuleFor(e => e.Nom, f => f.Name.LastName())
            .RuleFor(e => e.Prenom, f => f.Name.FirstName())
            .RuleFor(e => e.Matricule, f => $"ENS-{f.Random.Int(1000, 9999)}")
            .RuleFor(e => e.Salaire, f => Math.Round(f.Random.Decimal(8000, 25000), 2))
            .RuleFor(e => e.Grade, f => f.PickRandom(grades))
            .RuleFor(e => e.DateEmbauche, f => f.Date.Past(15));

        db.Enseignants.AddRange(faker.Generate(60));
        db.SaveChanges();
    }
}

// --- Middleware sécurité : vérification HMAC réelle (signature + fraîcheur + anti-rejeu) ---
app.UseMiddleware<HmacVerificationMiddleware>();

app.UseRouting();

// --- Endpoint SOAP ---
app.UseSoapEndpoint<IRhPaieService>("/RhPaieService.asmx", new SoapEncoderOptions());

app.MapHealthChecks("/health");

app.Run();
