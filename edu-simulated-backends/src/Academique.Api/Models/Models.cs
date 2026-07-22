namespace Academique.Api.Models;

public class Note
{
    public int Id { get; set; }
    public int EtudiantId { get; set; }
    public string Matiere { get; set; } = string.Empty;
    public double Valeur { get; set; }
    public string Semestre { get; set; } = string.Empty; // ex. "S3"
}

public class EmploiDuTemps
{
    public int Id { get; set; }
    public int EtudiantId { get; set; }
    public string Jour { get; set; } = string.Empty; // ex. "Lundi"
    public string Creneau { get; set; } = string.Empty; // ex. "08:30-10:30"
    public string Matiere { get; set; } = string.Empty;
    public string Salle { get; set; } = string.Empty;
}