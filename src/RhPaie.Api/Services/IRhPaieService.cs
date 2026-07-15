using System.ServiceModel;
using RhPaie.Api.Models;

namespace RhPaie.Api.Services;

// le contrat SOAP. SoapCore génère automatiquement le WSDL à partir d'elle.
[ServiceContract]
public interface IRhPaieService
{
    [OperationContract]
    List<Enseignant> ObtenirTousLesEnseignants();

    [OperationContract]
    Enseignant? ObtenirEnseignantParId(int id);

    [OperationContract]
    Enseignant AjouterEnseignant(Enseignant enseignant);

    [OperationContract]
    bool MettreAJourSalaire(int id, decimal nouveauSalaire);
}