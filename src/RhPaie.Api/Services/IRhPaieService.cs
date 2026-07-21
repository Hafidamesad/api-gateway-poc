using System.ServiceModel;
using RhPaie.Api.Models;

namespace RhPaie.Api.Services;

// Équivalent conceptuel d'un @WebService (Spring WS) : cette interface définit
// le contrat SOAP. SoapCore génère automatiquement le WSDL à partir d'elle.
[ServiceContract]
public interface IRhPaieService
{
    [OperationContract]
    Task<List<Enseignant>> ObtenirTousLesEnseignants();

    [OperationContract]
    Task<Enseignant?> ObtenirEnseignantParId(int id);

    [OperationContract]
    Task<Enseignant> AjouterEnseignant(Enseignant enseignant);

    [OperationContract]
    Task<bool> MettreAJourSalaire(int id, decimal nouveauSalaire);
}