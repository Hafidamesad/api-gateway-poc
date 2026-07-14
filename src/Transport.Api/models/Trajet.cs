namespace Transport.Api.Models;

public class Trajet
{
    public int Id { get; set; }
    public string LigneBus { get; set; } = string.Empty;
    public string Depart { get; set; } = string.Empty;
    public string Arrivee { get; set; } = string.Empty;
    public DateTime HeureDepart { get; set; }
    public int PlacesDisponibles { get; set; }
    public string Statut { get; set; } = string.Empty; // "à l'heure", "retardé", "annulé"
}