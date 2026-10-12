# Minecraft Servers Manager 1.1.0

Distribution Windows x64, configuration Release. Le numéro 1.1.0 est affiché dans la fenêtre et dans les métadonnées de l'exécutable.

## Installation et lancement

1. Extraire entièrement l'archive dans un dossier accessible en écriture.
2. Fermer l'ancienne instance du gestionnaire avant de lancer `Minecraft Servers Manager.exe`.
3. Vérifier les chemins Java et serveur dans l'éditeur avant le premier démarrage.

.NET est intégré à l'exécutable. Le runtime Microsoft Edge WebView2 est nécessaire au navigateur CurseForge. Java doit être installé séparément selon les exigences des serveurs et modpacks.

Les profils existants restent dans `Documents/Minecraft Servers Manager`. Le changement de dossier du programme ne déplace pas les mondes ni les serveurs. Une sauvegarde d'un serveur actif demande confirmation avant de l'arrêter, de l'archiver puis de le relancer.

## Changements principaux

- Correction des mesures RAM/CPU pour les lancements batch et leurs processus enfants.
- Sessions indépendantes de la navigation, arrêt coordonné et relances annulables.
- Sauvegardes vérifiées, écritures atomiques, contrôle des archives importées.
- Console bornée, meilleure gestion des ressources et retrait de LiveCharts.

Le CPU est exprimé en pourcentage de la capacité totale de la machine. La RAM correspond à la mémoire résidente du groupe de processus, distincte de la limite du tas Java.

## Validation et distribution

Compilation Release : 0 avertissement, 0 erreur. Les 15 tests de régression réussissent avec des processus synthétiques et des dossiers isolés. Le rendu WPF du monitoring a été vérifié. Les intégrations Minecraft réel, CurseForge, Discord et Mojang n'ont pas été testées en ligne.

L'exécutable est autonome et compressé, avec .NET 10.0.0 et WebView2 SDK 1.0.3650.58. Il n'est pas signé Authenticode. L'archive est accompagnée d'une empreinte SHA-256 ; les licences des composants redistribués sont incluses. La production de cette archive locale ne publie pas de release distante.

Le détail des changements et des limites figure dans `REFACTOR-IMPLEMENTATION.md`.
