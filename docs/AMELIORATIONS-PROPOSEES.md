# Ameliorations proposees

Date : 2026-09-16.

Ce document conserve les suggestions discutees apres la publication x64. Il ne constitue pas un engagement de mise en oeuvre. Aucune de ces ameliorations n'a ete implementee pendant cette session.

## Contexte et limites

- Application WPF, .NET 10, version 1.1.0.
- Build autonome Windows x64 generee dans `artifacts/MSM-1.1.0-win-x64/`.
- Executable : `Minecraft Servers Manager.exe`, architecture AMD64 verifiee, environ 72,66 Mio.
- Publication reussie avec avertissements traites comme erreurs ; 23 tests de regression reussis.
- La build inclut les modifications locales non committees. Aucun code source modifie ni commit cree pendant cette session.
- Pas de validation avec un veritable serveur Minecraft pendant cette session.
- Les suggestions reposent sur le code actuel, y compris les fichiers non suivis. Les gains de performance restent des hypotheses a mesurer.
- Les references de lignes correspondent a cet etat du code et pourront evoluer.

## Ordre recommande

1. Gains rapides : bouton Redemarrer, protection des modifications non enregistrees, clarification de la jauge RAM.
2. Fiabilite : restauration guidee, diagnostic Java, diagnostic des crashs en mode batch.
3. Performances et confort : console, reduction du rendu lorsque la fenetre est masquee.
4. Fonctionnalites : politiques de sauvegarde et assistant de validation des modpacks.

Privilegier des evolutions ciblees : sessions independantes de la navigation, suivi des processus enfants, relances limitees, console bornee et extraction ZIP controlee existent deja.

## 1. Restauration guidee des sauvegardes

**Priorite : haute. Impact : tres eleve. Effort : eleve.**

Etat actuel : sauvegardes disponibles, mais restauration manuelle. La verification a la creation compare le nombre d'entrees ZIP sans relire integralement leur contenu.

Propositions :

- Lister les archives avec date, taille et resultat de verification.
- Verifier les chemins et lire integralement les entrees avant restauration.
- Restaurer dans un dossier temporaire ou separe, serveur arrete.
- Demander confirmation avant remplacement et conserver l'ancien dossier pour revenir en arriere.
- Envisager un manifeste d'empreintes pour les nouvelles sauvegardes.

Verification attendue : restauration reussie d'une archive valide ; rejet d'une archive corrompue ou contenant des chemins dangereux ; conservation du serveur existant en cas d'echec.

References : `Services/ServerBackup.cs:18`, `Services/ServerSession.cs:196`, `README.md:37`.

## 2. Diagnostic Java avant demarrage

**Priorite : haute. Impact : eleve. Effort : moyen pour le diagnostic, eleve pour l'installation geree.**

Etat actuel : detection et selection de Java presentes, mais pas de controle prealable de compatibilite.

Propositions :

- Resoudre le Java reellement utilise ; afficher version, fournisseur et architecture.
- Executer la sonde avec une duree limitee.
- Signaler les incompatibilites connues avec Minecraft ou le loader, sans supposer des exigences lorsque les metadonnees manquent.
- Expliquer qu'un chemin Java code en dur dans un batch peut ignorer la selection de l'application.
- Deplacer la detection hors du constructeur UI et mettre les resultats en cache.
- Dans un second temps, proposer une installation Java verifiee, geree par MSM, sans modifier le Java global.

Verification attendue : cas Java absent, compatible, incompatible, sonde bloquee et batch utilisant son propre executable.

References : `ViewModels/ServerEditorViewModel.cs:175`, `ViewModels/ServerEditorViewModel.cs:216`, `Services/ServerLauncher.cs:13`.

## 3. Diagnostic des crashs et relances

**Priorite : haute. Impact : eleve. Effort : moyen a eleve.**

Etat actuel : relances automatiques deja presentes. En mode batch, la classification depend du code de sortie du lanceur. Celui-ci peut masquer une erreur Java. Ce risque est deduit du code, pas reproduit pendant cette analyse.

Propositions :

- Documenter et verifier le contrat des scripts : Java au premier plan et propagation de son code de sortie.
- Distinguer une sortie normale connue d'un resultat inconnu lorsque le lanceur termine avant ses enfants.
- Afficher motif d'arret, code de sortie, tentative de relance et delai restant.
- Signaler un demarrage anormalement long sans tuer automatiquement les gros packs.

Verification attendue : batch propageant une erreur Java, batch masquant cette erreur, enfant detache et annulation d'une relance.

References : `Services/ServerLauncher.cs:90`, `Services/ServerSession.cs:297`, `Services/ServerSession.cs:340`.

## 4. Reduire le rendu lorsque la fenetre est masquee

**Priorite : moyenne. Impact potentiel : moyen. Effort : moyen.**

Etat actuel : mesures environ toutes les secondes, rafraichissement du ViewModel toutes les 250 ms et console toutes les 100 ms. Les timers sont nettoyes a la navigation, mais la fermeture normale masque la fenetre sans changer de vue.

Propositions :

- Suspendre les rafraichissements purement visuels lorsque la fenetre est masquee.
- Continuer le suivi des processus, la collecte des logs et les taches planifiees.
- Recharger le tampon borne a la reouverture plutot que rejouer tout le retard d'affichage.
- Envisager une notification de nouveau snapshot plutot qu'un polling UI a 250 ms.

Verification attendue : comparer CPU et allocations visible/masque avec plusieurs serveurs ; confirmer que logs, surveillance et redemarrages restent actifs.

References : `ViewModels/ServerMonitorViewModel.cs:121`, `UserControls/ServerMonitor.xaml.cs:59`, `Services/ServerSession.cs:254`, `MainWindow.xaml.cs:102`.

## 5. Console : fluidite et outils de diagnostic

**Priorite : moyenne. Impact : eleve pour le confort ; gains de fluidite a mesurer. Effort : moyen, eleve pour remplacer le composant.**

Etat actuel : traitement jusqu'a 100 lignes toutes les 100 ms, historique de 1 000 paragraphes WPF, couleurs et defilement conditionnel. Le chargement initial reconstruit les paragraphes synchroniquement et passe par une conversion tableau / chaine / tableau.

Propositions :

- Charger aussi l'historique initial par lots et limiter le temps de rendu par rafraichissement.
- Regrouper les changements du document.
- Ajouter recherche, filtres erreurs/avertissements, pause d'affichage et bouton Revenir au direct.
- Exporter le tampon en indiquant ses limites et les lignes omises.
- N'envisager une liste virtualisee qu'apres profilage, en preservant selection et copie du texte.

Verification attendue : forte sortie console, navigation repetee, selection/copie, pause puis retour au direct ; mesurer latence UI et allocations avant/apres.

References : `UserControls/ServerMonitor.xaml.cs:104`, `UserControls/ServerMonitor.xaml.cs:160`, `ViewModels/ServerMonitorViewModel.cs:59`, `Services/LogBuffer.cs:3`.

## 6. Bouton Redemarrer et compte a rebours

**Priorite : gain rapide. Impact : confort quotidien. Effort : faible a moyen.**

Etat actuel : methode `RestartAsync` et planification quotidienne presentes, mais pas de commande dediee dans le monitoring.

Propositions :

- Ajouter Redemarrer maintenant et Redemarrer avec preavis en reutilisant la logique existante.
- Afficher prochaine echeance, phase en cours et delai restant.
- Permettre l'annulation du redemarrage en attente.
- Rendre le preavis configurable ; les dix secondes actuelles sont courtes pour une maintenance annoncee.

Verification attendue : redemarrage manuel, preavis, annulation et navigation pendant le compte a rebours, sans doublon de processus.

References : `Services/ServerSession.cs:156`, `Services/ServerSession.cs:274`, `ViewModels/ServerMonitorViewModel.cs:73`.

## 7. Protection des modifications non enregistrees

**Priorite : gain rapide. Impact : prevention de perte de travail. Effort : moyen.**

Etat actuel : l'editeur utilise un brouillon, mais changer de vue peut l'abandonner sans confirmation.

Propositions :

- Suivre les changements du profil et de `server.properties`.
- Afficher un indicateur de modifications non enregistrees.
- A la navigation, proposer Enregistrer / Abandonner / Annuler.
- Distinguer les champs differes des operations whitelist/op appliquees immediatement.

Verification attendue : navigation avec et sans changements, annulation, echec d'enregistrement et absence de double application des operations immediates.

References : `ViewModels/ServerEditorViewModel.cs:175`, `ViewModels/ServerEditorViewModel.cs:313`, `ViewModels/MainViewModel.cs:22`, `Services/ServerSession.cs:223`.

## 8. Clarifier la jauge RAM

**Priorite : gain rapide. Impact : fiabilite des informations. Effort : faible pour l'interface, eleve pour une telemetrie JVM.**

Etat actuel : les libelles distinguent memoire residente et limite du tas Java, mais `RamPercent` divise la memoire residente du groupe de processus par `-Xmx`. Ce ratio ne mesure pas le remplissage du tas et peut atteindre 100 % sans saturation du tas.

Propositions :

- Retirer la jauge ou la nommer explicitement memoire residente / budget Xmx.
- Conserver les deux valeurs separees et expliquer leur difference.
- Pour une vraie jauge du tas, envisager une telemetrie JVM optionnelle distincte.

Verification attendue : affichage non trompeur lorsque la memoire residente depasse Xmx, ou lorsqu'une mesure est indisponible.

References : `Services/ProcessGroup.cs:46`, `ViewModels/ServerMonitorViewModel.cs:38`, `UserControls/ServerMonitor.xaml:251`.

## 9. Politiques de sauvegarde configurables

**Priorite : moyenne. Impact : eleve. Effort : moyen a eleve.**

Etat actuel : sauvegardes manuelles dans le dossier serveur, suppression des archives de plus de sept jours apres succes. Une sauvegarde sur le meme disque ne protege pas contre sa panne.

Propositions :

- Destination configurable, notamment sur un autre volume.
- Retention par nombre et/ou espace avec un minimum de copies conservees.
- Afficher dernier succes, taille, progression et prochaine echeance.
- Planification optionnelle avec avertissement des joueurs : la strategie actuelle interrompt le serveur.
- Controle prealable de l'espace disponible et annulation dediee entre fichiers.

Verification attendue : disque plein, destination indisponible, annulation, retention apres succes uniquement et preservation du minimum de copies.

References : `Services/ServerBackup.cs:9`, `Models/ServerProfile.cs:17`, `ViewModels/ServerMonitorViewModel.cs:115`.

## 10. Assistant de validation des modpacks

**Priorite : moyenne. Impact : eleve pour les utilisateurs moins techniques. Effort : moyen a eleve.**

Etat actuel : telechargement, annulation, extraction controlee et ouverture de l'editeur existent. Le script ou JAR est choisi par heuristique ; une extraction reussie ne garantit pas un serveur pret a demarrer.

Propositions :

- Identifier les metadonnees disponibles : Minecraft, loader, manifest client et fichiers serveur.
- Presenter les cibles plausibles et expliquer le choix automatique.
- Distinguer import termine et serveur pret a demarrer.
- Afficher dependances et etapes manquantes, ainsi que la progression d'extraction en fichiers/octets.
- Ne pas executer automatiquement les scripts d'installation d'une archive.

Verification attendue : pack client seul, pack serveur valide, scripts multiples, dependances manquantes et annulation de l'import.

References : `Services/ServerInstaller.cs:25`, `Services/ServerInstaller.cs:73`, `UserControls/CurseForgeView.xaml.cs:97`.

## Reprise de la prochaine session

1. Relire ce document et verifier l'etat Git sans ecraser les modifications existantes.
2. Choisir une seule amelioration avec l'utilisateur ; ne pas lancer tout le catalogue automatiquement.
3. Relire les composants concernes, confirmer le comportement actuel et definir les criteres d'acceptation.
4. Implementer et verifier le changement cible avec les tests pertinents.
5. Mettre a jour ce document avec ce qui a ete realise et les limites restantes.

Commande de regression utilisee :

```powershell
dotnet run --project "Tests/Msm.RegressionTests.csproj" -c Release --verbosity quiet
```

Commande de publication utilisee :

```powershell
dotnet publish "Minecraft Server Manager.csproj" -p:PublishProfile=Official-win-x64 -p:RestoreLockedMode=true -warnaserror --verbosity quiet
```
