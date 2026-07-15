using Microsoft.EntityFrameworkCore;
using RhPaie.Api.Data;
using RhPaie.Api.Models;

namespace RhPaie.Api.Services;

// Implémentation du contrat SOAP. Palier de sécurité élevé (comme Finance) :
// en architecture cible, mTLS + HMAC sont validés en amont par la Gateway
// (policy APIM + Azure Function). Contrairement au controller REST de Finance,
// SoapCore ne permet pas d'inspecter facilement les headers HTTP custom depuis
// l'implémentation du service — cette vérification devra donc être faite via un
// middleware ASP.NET Core classique en amont du endpoint SOAP (voir Program.cs)

public class RhPaieService : IRhPaieService
{
    private readonly AppDbContext _context;

    public RhPaieService(AppDbContext context)
    {
        _context = context;
    }

    public List<Enseignant> ObtenirTousLesEnseignants()
    {
        SimulerLatence();
        return _context.Enseignants.ToList();
    }

    public Enseignant? ObtenirEnseignantParId(int id)
    {
        SimulerLatence();
        return _context.Enseignants.FirstOrDefault(e => e.Id == id);
    }

    public Enseignant AjouterEnseignant(Enseignant enseignant)
    {
        SimulerLatence();
        enseignant.DateEmbauche = DateTime.UtcNow;
        _context.Enseignants.Add(enseignant);
        _context.SaveChanges();
        return enseignant;
    }

    public bool MettreAJourSalaire(int id, decimal nouveauSalaire)
    {
        SimulerLatence();
        var enseignant = _context.Enseignants.FirstOrDefault(e => e.Id == id);
        if (enseignant is null) return false;

        enseignant.Salaire = nouveauSalaire;
        _context.SaveChanges();
        return true;
    }

    // Latence la plus haute des 5 modules (400-800ms), représentative d'un système legacy SOAP/XML
    private static void SimulerLatence()
    {
        Thread.Sleep(Random.Shared.Next(400, 800));
    }
}