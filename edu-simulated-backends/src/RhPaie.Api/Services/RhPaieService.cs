using Microsoft.EntityFrameworkCore;
using RhPaie.Api.Data;
using RhPaie.Api.Models;

namespace RhPaie.Api.Services;

// Implémentation du contrat SOAP. Palier de sécurité élevé (comme Finance) :
// en architecture cible, mTLS + HMAC sont validés en amont par la Gateway
// (policy APIM + Azure Function d'Imane). Contrairement au controller REST de Finance,
// SoapCore ne permet pas d'inspecter facilement les headers HTTP custom depuis
// l'implémentation du service — cette vérification devra donc être faite via un
// middleware ASP.NET Core classique en amont du endpoint SOAP (voir Program.cs),
// pas dans cette classe.
public class RhPaieService : IRhPaieService
{
    private readonly AppDbContext _context;

    public RhPaieService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<Enseignant>> ObtenirTousLesEnseignants()
    {
        await SimulerLatence();
        return _context.Enseignants.ToList();
    }

    public async Task<Enseignant?> ObtenirEnseignantParId(int id)
    {
        await SimulerLatence();
        return _context.Enseignants.FirstOrDefault(e => e.Id == id);
    }

    public async Task<Enseignant> AjouterEnseignant(Enseignant enseignant)
    {
        await SimulerLatence();
        enseignant.DateEmbauche = DateTime.UtcNow;
        _context.Enseignants.Add(enseignant);
        _context.SaveChanges();
        return enseignant;
    }

    public async Task<bool> MettreAJourSalaire(int id, decimal nouveauSalaire)
    {
        await SimulerLatence();
        var enseignant = _context.Enseignants.FirstOrDefault(e => e.Id == id);
        if (enseignant is null) return false;

        enseignant.Salaire = nouveauSalaire;
        _context.SaveChanges();
        return true;
    }

    // Task.Delay (non-bloquant) au lieu de Thread.Sleep : ne monopolise pas de thread
    // du pool ASP.NET Core pendant l'attente, évite l'effet de queuing sous charge concurrente.
    private static async Task SimulerLatence()
    {
        await Task.Delay(Random.Shared.Next(400, 800));
    }
}