using Microsoft.EntityFrameworkCore;
using SoapCore;
using Bogus;
using RhPaie.Api.Data;
using RhPaie.Api.Models;
using RhPaie.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// --- Services ---
builder.Services.AddSoapCore();
builder.Services.AddScoped<IRhPaieService, RhPaieService>();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHealthChecks();

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

// --- Middleware sécurité (palier élevé) ---
// SoapCore ne permet pas d'inspecter les headers custom depuis l'implémentation du service,
// donc la vérification de présence des headers HMAC se fait ici, avant que la requête
// n'atteigne le endpoint SOAP. Même contrat placeholder que Finance (à valider).
app.Use(async (HttpContext context, Func<Task> next) =>
{
    if (context.Request.Path.StartsWithSegments("/RhPaieService.asmx"))
    {
        var headersRequis = new[] { "X-HMAC-Signature", "X-Timestamp", "X-Nonce" };
        var manquants = headersRequis.Where(h => !context.Request.Headers.ContainsKey(h)).ToList();

        // Le WSDL (requête GET avec ?wsdl) reste accessible sans headers pour permettre
        // la découverte du contrat de service par les clients/outils.
        var estRequeteWsdl = context.Request.Query.ContainsKey("wsdl");

        if (manquants.Any() && !estRequeteWsdl)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsJsonAsync(new
            {
                error = $"Headers de sécurité manquants : {string.Join(", ", manquants)}",
                code = "MISSING_SECURITY_HEADERS",
                timestamp = DateTime.UtcNow
            });
            return;
        }
    }

    await next();
});

app.UseRouting();

// --- Endpoint SOAP ---
// Le WSDL est généré automatiquement, accessible à /RhPaieService.asmx?wsdl
app.UseSoapEndpoint<IRhPaieService>("/RhPaieService.asmx", new SoapEncoderOptions());

app.MapHealthChecks("/health");

app.Run();