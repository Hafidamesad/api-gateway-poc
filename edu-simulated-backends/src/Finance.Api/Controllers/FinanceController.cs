using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Finance.Api.Data;
using Finance.Api.Models;

namespace Finance.Api.Controllers;

// NOTE SECURITE (palier élevé) :
// En architecture cible, la validation mTLS + HMAC est effectuée EN AMONT par la Gateway
// (APIM policy "validate-client-certificate" + policy "send-request" vers l'Azure Function).
// Ce controller ne réimplémente PAS la vérification HMAC.
// Le contrat de headers ci-dessous est un PLACEHOLDER à valider :
//   X-HMAC-Signature : signature calculée côté client/Gateway
//   X-Timestamp       : timestamp de la requête (protection anti-rejeu)
//   X-Nonce           : valeur unique par requête (protection anti-rejeu)
// Pour l'instant, on vérifie uniquement leur PRESENCE (pas leur validité cryptographique)
// afin de documenter le contrat attendu et de pouvoir tester le flux de bout en bout localement.
[ApiController]
[Route("api/[controller]")]
public class FinanceController : ControllerBase
{
    private readonly AppDbContext _context;

    public FinanceController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/finance/paiements/{etudiantId}
    [HttpGet("paiements/{etudiantId}")]
    public async Task<ActionResult<IEnumerable<Paiement>>> GetPaiements(int etudiantId)
    {
        var erreurHeaders = VerifierHeadersSecurite();
        if (erreurHeaders is not null) return erreurHeaders;

        await SimulerLatence();

        var paiements = await _context.Paiements
            .Where(p => p.EtudiantId == etudiantId)
            .ToListAsync();

        return Ok(paiements);
    }

    // GET /api/finance/scolarite/{etudiantId}
    [HttpGet("scolarite/{etudiantId}")]
    public async Task<ActionResult<IEnumerable<Scolarite>>> GetScolarite(int etudiantId)
    {
        var erreurHeaders = VerifierHeadersSecurite();
        if (erreurHeaders is not null) return erreurHeaders;

        await SimulerLatence();

        var scolarites = await _context.Scolarites
            .Where(s => s.EtudiantId == etudiantId)
            .ToListAsync();

        return Ok(scolarites);
    }

    // POST /api/finance/paiements
    [HttpPost("paiements")]
    public async Task<ActionResult<Paiement>> CreatePaiement(Paiement paiement)
    {
        var erreurHeaders = VerifierHeadersSecurite();
        if (erreurHeaders is not null) return erreurHeaders;

        await SimulerLatence();

        paiement.DatePaiement = DateTime.UtcNow;
        _context.Paiements.Add(paiement);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetPaiements), new { etudiantId = paiement.EtudiantId }, paiement);
    }

    // Vérifie la présence des headers de sécurité attendus (contrat à finaliser).
    // Retourne null si ok, sinon une réponse 401 avec le format d'erreur uniforme.
    private ActionResult? VerifierHeadersSecurite()
    {
        var headersRequis = new[] { "X-HMAC-Signature", "X-Timestamp", "X-Nonce" };
        var manquants = headersRequis.Where(h => !Request.Headers.ContainsKey(h)).ToList();

        if (manquants.Any())
        {
            return Unauthorized(new
            {
                error = $"Headers de sécurité manquants : {string.Join(", ", manquants)}",
                code = "MISSING_SECURITY_HEADERS",
                timestamp = DateTime.UtcNow
            });
        }

        return null;
    }

    // Latence simulée 200-400ms, représentative d'une opération financière
    private static async Task SimulerLatence()
    {
        await Task.Delay(Random.Shared.Next(200, 400));
    }
}