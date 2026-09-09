using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Transport.Api.Data;
using Transport.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace Transport.Api.Controllers;

[ApiController]
[Authorize(Roles = "TRANSPORT,ADMIN")]
[Route("api/[controller]")]
public class TransportController : ControllerBase
{
    private readonly AppDbContext _context;

    public TransportController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/transport
    [HttpGet]
    public async Task<ActionResult<IEnumerable<Trajet>>> GetAll()
    {
        await SimulerLatencePolling();
        return Ok(await _context.Trajets.ToListAsync());
    }

    // GET /api/transport/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<Trajet>> GetById(int id)
    {
        await SimulerLatencePolling();

        var trajet = await _context.Trajets.FindAsync(id);
        if (trajet is null)
        {
            return NotFound(new { error = "Trajet introuvable", code = "TRAJET_NOT_FOUND", timestamp = DateTime.UtcNow });
        }

        return Ok(trajet);
    }

    // GET /api/transport/position/{ligneBus}
    // Endpoint pensé pour être appelé en polling fréquent (toutes les 5-10s côté client réel)
    [HttpGet("position/{ligneBus}")]
    public async Task<ActionResult<IEnumerable<Trajet>>> GetPositionParLigne(string ligneBus)
    {
        await SimulerLatencePolling();

        var trajets = await _context.Trajets
            .Where(t => t.LigneBus == ligneBus)
            .ToListAsync();

        return Ok(trajets);
    }

    // POST /api/transport
    [HttpPost]
    public async Task<ActionResult<Trajet>> Create(Trajet trajet)
    {
        await SimulerLatencePolling();

        _context.Trajets.Add(trajet);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetById), new { id = trajet.Id }, trajet);
    }

    // PUT /api/transport/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Trajet trajet)
    {
        await SimulerLatencePolling();

        if (id != trajet.Id)
        {
            return BadRequest(new { error = "L'id de l'URL ne correspond pas à l'id de l'objet", code = "ID_MISMATCH", timestamp = DateTime.UtcNow });
        }

        var existant = await _context.Trajets.FindAsync(id);
        if (existant is null)
        {
            return NotFound(new { error = "Trajet introuvable", code = "TRAJET_NOT_FOUND", timestamp = DateTime.UtcNow });
        }

        existant.LigneBus = trajet.LigneBus;
        existant.Depart = trajet.Depart;
        existant.Arrivee = trajet.Arrivee;
        existant.HeureDepart = trajet.HeureDepart;
        existant.PlacesDisponibles = trajet.PlacesDisponibles;
        existant.Statut = trajet.Statut;

        await _context.SaveChangesAsync();
        return NoContent();
    }

    // DELETE /api/transport/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        await SimulerLatencePolling();

        var trajet = await _context.Trajets.FindAsync(id);
        if (trajet is null)
        {
            return NotFound(new { error = "Trajet introuvable", code = "TRAJET_NOT_FOUND", timestamp = DateTime.UtcNow });
        }

        _context.Trajets.Remove(trajet);
        await _context.SaveChangesAsync();
        return NoContent();
    }

    // Latence basse et volontairement variable, représentative d'un polling fréquent (30-80ms)
    private static async Task SimulerLatencePolling()
    {
        await Task.Delay(Random.Shared.Next(30, 80));
    }
}
