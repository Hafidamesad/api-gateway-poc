
using Microsoft.EntityFrameworkCore;
using Bogus;
using Finance.Api.Data;
using Finance.Api.Security;
using Finance.Api.Models;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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
    options.ListenAnyIP(5104, listenOptions =>
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

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        var jwtSecret = Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? builder.Configuration["Security:Jwt:Secret"]
            ?? "CHANGE_ME_DEV_JWT_SECRET_MIN_32_CHARS";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();
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


app.UseAuthentication();
app.UseAuthorization();

app.UseMiddleware<HmacVerificationMiddleware>();

app.MapControllers().RequireAuthorization();
app.MapHealthChecks("/health");

app.Run();
