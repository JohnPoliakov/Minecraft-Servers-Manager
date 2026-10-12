# Refactor 1.1.0-preview.1

Implémentation réalisée à partir de l'audit du commit `1980935`. La fenêtre affiche maintenant la version pour distinguer les builds.

## RAM et CPU : défaut reproduit et correction

Le monitoring initial lisait uniquement le PID du lanceur. Un test avec une charge synthétique d'au moins 96 Mio donne **4 Mo / 0 % CPU en batch**, contre **118 Mo / 6 % en lancement direct**. Cela démontre un défaut présent dans le code audité ; une ancienne build n'est pas nécessaire pour le rencontrer. Le test initial utilisait un processus .NET pour isoler la lecture Windows, sans serveur Minecraft ni réseau.

`ProcessGroup` utilise désormais un Job Object Windows. Il compte le CPU du groupe et additionne la mémoire résidente de ses processus, y compris les enfants détachés. Le batch attend une première entrée avant son exécution : le job est donc attribué avant que le script lance ses enfants. Les scripts ne sont plus réécrits par regex ; le Java sélectionné configure `JAVA_HOME` et `PATH` dans l'environnement enfant. Un chemin Java codé en dur dans un script reste sous le contrôle de ce script.

Le CPU utilise une horloge monotone et une décimale. 100 % représente la capacité totale de la machine : un cœur occupé sur seize correspond approximativement à 6,25 %. La mémoire est affichée en **Mio résidents**, distincts de la limite du tas Java. En batch, cette limite reste inconnue et sa jauge est masquée ; en Java direct, `-Xmx` est lu avec les suffixes k/m/g. Un tiret indique une mesure indisponible ou le premier échantillon CPU.

Les tests après correction mesurent environ **126–137 Mio et 5,5–6,3 % CPU** pour la charge batch, selon l'échantillon et le nombre de processus encore actifs. Ces chiffres illustrent la correction et ne constituent pas un benchmark Minecraft. L'interface de monitoring a également été rendue en PNG et inspectée.

## Changements fonctionnels

- `ServerRegistry` conserve une `ServerSession` par serveur. La navigation ne détruit plus le processus, sa liste de joueurs ni sa planification.
- Démarrage, arrêt, relance, suppression et fermeture sont coordonnés. Les commandes de la console, des permissions et de l'arrêt partagent un canal sérialisé. L'arrêt explicite annule les relances ; les crashs non nuls sont retentés au maximum trois fois avec délai croissant. Une sortie normale ne déclenche pas de relance.
- Le statut distingue démarrage, disponibilité, arrêt, attente de relance et erreur. Le webhook « disponible » attend le message `Done (…) … For help` du serveur. Le refus d'EULA et l'échec de lancement reviennent à un état stable.
- Une sauvegarde d'un serveur actif demande confirmation, l'arrête proprement, crée l'archive puis le relance. Toute lecture impossible fait échouer l'archive ; une archive temporaire n'est publiée qu'après création complète. La rétention de sept jours ne s'applique qu'après succès. Un ordre d'arrêt reçu pendant l'opération empêche la relance.
- Les ZIP importés passent par un dossier de préparation unique, un quota de 20 Gio décompressés et des contrôles de chemin. Une cible de lancement doit être détectée. L'éditeur s'ouvre ensuite pour vérifier les choix du pack. L'installation est annulable et n'immobilise plus la fenêtre entière.
- Le navigateur contrôle les origines HTTPS CurseForge/ForgeCDN, borne les icônes téléchargées et est libéré à la navigation. Son cache utilise le même emplacement pour création et suppression.
- Les profils ne contiennent plus de `Process`, de cache de logs ou d'état `IsRunning` persisté. Leurs chemins historiques sont conservés, les identifiants et dossiers sont validés, et les écritures atomiques conservent une copie `.bak`.
- L'éditeur travaille sur un brouillon. Les propriétés non modifiées et les commentaires sont conservés ; une modification externe de `server.properties` empêche son écrasement. Pour les permissions, le serveur actif possède ses fichiers : l'application envoie une commande et indique de vérifier sa confirmation en console. À l'arrêt, elle applique une modification à la liste relue sur disque.
- La console ne retire plus une ligne supplémentaire à la limite d'un lot. Les caches et files sont bornés, avec un indicateur des lignes non affichées en cas de surcharge. Les images sont décodées à une taille adaptée aux icônes.
- Les ressources de langue sont remplacées au changement de langue. Les anciens graphiques LiveCharts ont été retirés. Le préréglage G1 conserve le budget mémoire, les propriétés personnalisées et les options de diagnostic.
- Le projet cible .NET 10, fixe sa sélection de SDK et verrouille sa dépendance WebView2. Le build compile sans avertissement. Une CI Windows et un exécutable de tests sans dépendance à un framework de tests ont été ajoutés.

## Vérification

Validation locale du 10 septembre 2026 : compilation Release avec **0 avertissement et 0 erreur**, **15 tests de régression réussis**, puis publication de la build. Le dernier échantillon batch mesure 137 Mio et 6,3 % de CPU ; le lancement direct mesure 132 Mio et 6,3 %. Le rendu WPF du monitoring a été inspecté.

```powershell
dotnet restore "Minecraft Server Manager.csproj" --locked-mode
dotnet build "Minecraft Server Manager.sln" -c Release --no-restore -warnaserror
dotnet run --project Tests/Msm.RegressionTests.csproj -c Release
dotnet publish "Minecraft Server Manager.csproj" -c Release --no-restore --self-contained false -o artifacts/MSM-1.1.0-preview.1
```

Le projet de tests doit également être restauré lors d'un premier build de la solution (`dotnet restore "Minecraft Server Manager.sln"`). La CI restaure le projet de tests par `dotnet run` après avoir vérifié l'application.

Les régressions couvrent lancement direct et batch, enfants détachés, RAM/CPU, navigation, 100 commandes concurrentes, arrêt après crash, planification avec horloge fixe, refus d'EULA, lancement manquant, annulation d'un démarrage, fermeture de plusieurs serveurs, suppression active, sauvegardes et contenu restauré d'une fixture, propriétés, catalogue, permissions, ZIP invalides, console et dictionnaires de langue.

## Limites à garder explicites

- Aucun serveur Minecraft réel, pack CurseForge téléchargé, webhook Discord ni appel Mojang n'a été utilisé pour les tests. Le navigateur WebView2 complet et le comportement de chaque pack restent à vérifier en usage réel. La CI a été ajoutée, sans exécution distante pendant cette session.
- La build publiée est **dépendante du .NET 10 Desktop Runtime x64**. Conserver tout le dossier publié ; copier le seul `.exe` ne suffit pas.
- Le gestionnaire conserve le contrôle du groupe de processus. Une fermeture normale demande un arrêt propre, puis force la terminaison après délai. Une terminaison forcée du gestionnaire ferme également son job Windows et ses processus enfants.
- La détection automatique JAR/script reste une heuristique à contrôler dans l'éditeur. Elle n'installe pas Java ni les dépendances manquantes d'un pack.
- L'écriture est atomique par fichier. L'enregistrement du profil et celui de `server.properties` ne constituent pas une transaction unique entre plusieurs fichiers. Les erreurs sont signalées et les copies précédentes sont conservées.
- Le cœur reste dans le projet WPF et `ServerProfile` conserve une projection pour les cartes. L'extraction dans une bibliothèque indépendante est maintenant possible, mais n'est pas nécessaire au fonctionnement corrigé.
- Les URL de webhook restent stockées dans les profils existants ; elles ne sont pas émises dans les diagnostics HTTP. Un stockage de secrets chiffré peut faire l'objet d'une migration distincte.
- Les sondes du dossier `docs/audit` documentent exclusivement l'ancienne architecture du commit audité. Pour cette version, utiliser `Tests/Msm.RegressionTests.csproj`.
