using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Finance.Api.Data;
using Finance.Api.Models;

namespace Finance.Api.Controllers;

// SECURITE : mTLS (Gateway<->backend), JWT et HMAC sont valides EN AMONT
// dans le pipeline (Program.cs : UseAuthentication -> UseAuthorization ->
// HmacVerificationMiddleware) avant que ce controller ne soit atteint.
// [Authorize(Roles=...)] applique la contrainte de role definie par le
// role-matrix cote Gateway (RoleAuthorizationFilter), en profondeur
// (defense-in-depth) au cas ou ce service serait atteint directement.
[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "FINANCE,ADMIN")]
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
        await SimulerLatence();
        paiement.DatePaiement = DateTime.UtcNow;
        _context.Paiements.Add(paiement);
        await _context.SaveChangesAsync();
        return CreatedAtAction(nameof(GetPaiements), new { etudiantId = paiement.EtudiantId }, paiement);
    }

    // Latence simulée 200-400ms, représentative d'une opération financière
    private static async Task SimulerLatence()
    {
        await Task.Delay(Random.Shared.Next(200, 400));
    }
}
