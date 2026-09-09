using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Etudiants.Api.Data;
using Etudiants.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace Etudiants.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Roles = "ETUDIANT,ADMIN")]
public class EtudiantsController : ControllerBase
{
    private readonly AppDbContext _context;

    // Injection de dépendances via constructeur 
    public EtudiantsController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/etudiants
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Etudiant>>> GetAll()
    {
        await SimulerLatenceBaseline();
        return Ok(await _context.Etudiants.ToListAsync());
    }

    // GET /api/etudiants/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Etudiant>> GetById(int id)
    {
        await SimulerLatenceBaseline();

        var etudiant = await _context.Etudiants.FindAsync(id);
        if (etudiant is null)
        {
            return NotFound(new { error = "Étudiant introuvable", code = "ETUDIANT_NOT_FOUND", timestamp = DateTime.UtcNow });
        }

        return Ok(etudiant);
    }

    // POST /api/etudiants
    [HttpPost]
    public async Task<ActionResult<Etudiant>> Create(Etudiant etudiant)
    {
        await SimulerLatenceBaseline();

        etudiant.DateInscription = DateTime.UtcNow;
        _context.Etudiants.Add(etudiant);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = etudiant.Id }, etudiant);
    }

    // PUT /api/etudiants/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Etudiant etudiant)
    {
        await SimulerLatenceBaseline();

        if (id != etudiant.Id)
        {
            return BadRequest(new { error = "L'id de l'URL ne correspond pas à l'id de l'objet", code = "ID_MISMATCH", timestamp = DateTime.UtcNow });
        }

        var existant = await _context.Etudiants.FindAsync(id);
        if (existant is null)
        {
            return NotFound(new { error = "Étudiant introuvable", code = "ETUDIANT_NOT_FOUND", timestamp = DateTime.UtcNow });
        }

        // Mise à jour manuelle des champs (on garde le contrôle explicite plutôt qu'un Entry(etudiant).State = Modified global)
        existant.Nom = etudiant.Nom;
        existant.Prenom = etudiant.Prenom;
        existant.Email = etudiant.Email;
        existant.Filiere = etudiant.Filiere;
        existant.Annee = etudiant.Annee;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/etudiants/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await SimulerLatenceBaseline();

        var etudiant = await _context.Etudiants.FindAsync(id);
        if (etudiant is null)
        {
            return NotFound(new { error = "Étudiant introuvable", code = "ETUDIANT_NOT_FOUND", timestamp = DateTime.UtcNow });
        }

        _context.Etudiants.Remove(etudiant);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Simule la latence baseline du module Étudiants (~100ms, plage 80-120ms)
    // Sert de référence de mesure sans sécurité (mTLS/HMAC) pour les benchmarks de la Gateway
    private static async Task SimulerLatenceBaseline()
    {
        await Task.Delay(Random.Shared.Next(80, 120));
    }
}
