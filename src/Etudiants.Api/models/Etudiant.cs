namespace Etudiants.Api.Models;

public class Etudiant
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Prenom { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Filiere { get; set; } = string.Empty;
    public int Annee { get; set; }
    public DateTime DateInscription { get; set; }
}