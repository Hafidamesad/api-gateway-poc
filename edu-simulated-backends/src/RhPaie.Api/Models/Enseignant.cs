namespace RhPaie.Api.Models;

public class Enseignant
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Matricule { get; set; } = string.Empty;
    public decimal Salaire { get; set; }
    public string Grade { get; set; } = string.Empty; // ex. "Professeur Assistant", "Professeur Habilité"
    public DateTime DateEmbauche { get; set; }
}