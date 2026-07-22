namespace Finance.Api.Models;

public class Paiement
{
    public int Id { get; set; }
    public int EtudiantId { get; set; }
    public decimal Montant { get; set; }
    public DateTime DatePaiement { get; set; }
    public string Statut { get; set; } = string.Empty; // "en attente", "validé", "rejeté"
    public string ModePaiement { get; set; } = string.Empty; // "carte", "virement", "espèces"
}

public class Scolarite
{
    public int Id { get; set; }
    public int EtudiantId { get; set; }
    public decimal MontantDu { get; set; }
    public decimal MontantPaye { get; set; }
    public DateTime Echeance { get; set; }
}