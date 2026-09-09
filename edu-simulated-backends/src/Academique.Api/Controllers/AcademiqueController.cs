using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Academique.Api.Data;
using Academique.Api.Models;
using Microsoft.AspNetCore.Authorization;

namespace Academique.Api.Controllers;

[ApiController]
[Authorize(Roles = "ACADEMIQUE,ADMIN")]
[Route("api/[controller]")]
public class AcademiqueController : ControllerBase
{
    private readonly AppDbContext _context;

    // Flag global en mémoire, activé/désactivé via l'endpoint de simulation de pic.
    // Volontairement simple (statique) : suffisant pour un prototype de benchmark, pas destiné à la prod.
    private static bool _modePic = false;

    public AcademiqueController(AppDbContext context)
    {
        _context = context;
    }

    // GET /api/academique/notes/{etudiantId}
    [HttpGet("notes/{etudiantId}")]
    public async Task<ActionResult<IEnumerable<Note>>> GetNotes(int etudiantId)
    {
        await SimulerLatence();

        var notes = await _context.Notes
            .Where(n => n.EtudiantId == etudiantId)
            .ToListAsync();

        return Ok(notes);
    }

    // GET /api/academique/emploi-du-temps/{etudiantId}
    [HttpGet("emploi-du-temps/{etudiantId}")]
    public async Task<ActionResult<IEnumerable<EmploiDuTemps>>> GetEmploiDuTemps(int etudiantId)
    {
        await SimulerLatence();

        var edt = await _context.EmploisDuTemps
            .Where(e => e.EtudiantId == etudiantId)
            .ToListAsync();

        return Ok(edt);
    }

    // POST /api/academique/notes
    [HttpPost("notes")]
    public async Task<ActionResult<Note>> CreateNote(Note note)
    {
        await SimulerLatence();

        _context.Notes.Add(note);
        await _context.SaveChangesAsync();

        return CreatedAtAction(nameof(GetNotes), new { etudiantId = note.EtudiantId }, note);
    }

    // POST /api/academique/simuler-pic
    // Active/désactive un mode de latence dégradée pour tester le comportement de la Gateway
    // (load balancing, circuit breaker) sous un pic de charge type "publication de résultats".
    [HttpPost("simuler-pic")]
    public IActionResult TogglePic([FromQuery] bool actif)
    {
        _modePic = actif;
        return Ok(new { modePic = _modePic, message = actif ? "Pic de charge activé" : "Pic de charge désactivé" });
    }

    // Latence normale sauf en mode pic, où elle est fortement dégradée
    // pour observer le comportement de la Gateway sous stress.
    private static async Task SimulerLatence()
    {
        if (_modePic)
        {
            await Task.Delay(Random.Shared.Next(1000, 2500));
        }
        else
        {
            await Task.Delay(Random.Shared.Next(100, 250));
        }
    }
}
