using Microsoft.EntityFrameworkCore;
using Bogus;
using Transport.Api.Data;
using Transport.Api.Models;
using Transport.Api.Security;
using System.Security.Cryptography.X509Certificates;
using Microsoft.AspNetCore.Server.Kestrel.Https;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
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
    options.ListenAnyIP(5102, listenOptions =>
    {
        listenOptions.UseHttps();
    });
});

builder.Services.AddSingleton<IConnectionMultiplexer>(sp =>
{
    var config = builder.Configuration["REDIS_CONNECTION"]
        ?? Environment.GetEnvironmentVariable("REDIS_CONNECTION")
        ?? "localhost:6379";

    return ConnectionMultiplexer.Connect(config);
});

// --- Services ---
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddHealthChecks();

// ============================================================
// JWT Authentication
// ============================================================

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;

        var jwtSecret =
            Environment.GetEnvironmentVariable("JWT_SECRET")
            ?? builder.Configuration["Security:Jwt:Secret"]
            ?? throw new InvalidOperationException(
                "JWT_SECRET is not set. Export JWT_SECRET before starting this service (see .env.example at repo root).");

        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = "role"
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

// --- Migration automatique + seed au démarrage ---
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
    if (!db.Trajets.Any())
    {
        var lignes = new[] { "L1", "L2", "L3", "L4", "L5" };
        var statuts = new[] { "à l'heure", "retardé", "annulé" };
        var faker = new Faker<Trajet>("fr")
            .RuleFor(t => t.LigneBus, f => f.PickRandom(lignes))
            .RuleFor(t => t.Depart, f => f.Address.StreetName())
            .RuleFor(t => t.Arrivee, f => f.Address.StreetName())
            .RuleFor(t => t.HeureDepart, f => f.Date.Soon(1))
            .RuleFor(t => t.PlacesDisponibles, f => f.Random.Int(0, 50))
            .RuleFor(t => t.Statut, f => f.PickRandom(statuts));
        var trajets = faker.Generate(100);
        db.Trajets.AddRange(trajets);
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
