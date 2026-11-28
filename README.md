# Fresh & Care — Gestion de pressing

Application Windows C# pour gérer un pressing. Le programme de bureau ouvre une fenêtre Fresh & Care ; le gestionnaire et les clients choisissent leur espace sur le premier écran. Les deux espaces partagent la base Microsoft Access locale `Gestion pressing.accdb`.

## Démarrer

1. Vérifier que le pilote ODBC Microsoft Access 64 bits et le WebView2 Runtime sont installés.
2. Dans PowerShell ouvert dans le dossier du projet, taper `.\run.bat` — ou double-cliquer sur `run.bat` — pour démarrer l'application dans sa fenêtre Windows. Le lanceur démarre la version Windows publiée si elle existe ; .NET 10 est nécessaire uniquement pour la compilation de secours.
3. Sur l’écran de garde, choisir **Espace gérant** ou **Espace client**.
4. À la première ouverture de l’espace gérant, créer le compte gestionnaire avec l’adresse préremplie et un mot de passe privé (6 caractères minimum en local, 12 en ligne).

L’application crée les tables et la grille de démarrage dans `Gestion pressing.accdb`. Le lancement depuis le projet utilise le fichier situé dans le dossier du projet. La version publiée utilise `%LOCALAPPDATA%\FreshCare\Gestion pressing.accdb`.

## Fonctions incluses

- Fiches clients, recherche par nom ou téléphone, solde et historique.
- Tickets uniques, plusieurs articles par dépôt, dates de retrait et statuts par article.
- Supplément express configurable par catégorie.
- Grille tarifaire modifiable, tickets PDF, factures Excel (`.xlsx`) et factures Word (`.docx`).
- Paiements partiels, impayés par client, mouvements de caisse et solde espèces.
- Alertes du tableau de bord pour les articles dont la date de retrait est dépassée.
- Espace client : connexion par numéro de téléphone et code PIN à usage privé, historique, état « prêt », montant dû et reçus PDF/Excel/Word. Excel et Word s’ouvrent automatiquement après génération dans l'application Windows.
- Naya, hôtesse vocale de l’espace client : accueil personnalisé, réponses contextualisées sur les tickets, les articles prêts et le solde, et prononciation d’une phrase saisie. Sa voix française ou anglaise est générée sur l’appareil, sans envoyer les questions à un service externe.
- Sur l’accueil, Naya présente l’application à voix haute en français ou en anglais et termine par un remerciement au professeur. Son guide contextuel s’ouvre aussi sur les formulaires pour expliquer les champs, les étapes et leurs avantages. Le lancement de la visite se fait par le bouton **Démarrer la présentation**.
- Boîte de réception interne : messages échangés entre les clients et le pressing, suivi des non-lus, recherche par client/téléphone/contenu et filtre des conversations non lues.
- Comptes individuels par employé, rôles cumulables, désactivation des comptes et journal d’activité.
- File atelier par rôle : lavage, repassage/finition et contrôle qualité, avec attribution des articles par le responsable.
- Missions de collecte et de livraison attribuées à un livreur, avec compte rendu et preuve de remise facultative.

## Première utilisation

Les catégories et montants chargés dans la grille sont des exemples de démarrage. Ajuster les prix et les majorations express depuis **Grille tarifaire** avant de facturer. À la création d’un client, l’application génère un PIN client et l’affiche une seule fois dans sa fiche ; le pressing le remet directement au client.

Le total de caisse correspond aux espèces encaissées plus les entrées de caisse moins les sorties. Les paiements Mobile Money, Orange Money, carte ou virement réduisent l’impayé et apparaissent dans l’historique, mais ne modifient pas le solde espèces.

## Comptes employés et flux de travail

Le premier compte gestionnaire existant devient Administrateur général lors de la mise à jour de la base. Après connexion, ouvrez **Employés & rôles** pour créer un compte nominatif par personne, définir un mot de passe temporaire et choisir ses responsabilités. Une même personne peut cumuler plusieurs rôles. Seul l’administrateur général gère les comptes et les rôles ; le responsable supervise les opérations et attribue les tâches.

Les rôles fournis sont : Administrateur général, Responsable, Accueil/Réception, Lavage, Repassage/Finition, Caisse/Comptabilité, Livreur, Contrôle qualité et Gestionnaire de stock. Les écrans et actions sont contrôlés par le serveur selon ces rôles.

Le flux courant est : **fiche client → dépôt et ticket → lavage → repassage/finition → contrôle qualité → prêt → retrait ou livraison**. Le contrôle qualité peut valider un article ou le renvoyer en reprise. Les articles restent attribuables à un employé ; chaque changement important est inscrit dans le journal. Une collecte demandée depuis une fiche client rejoint la file des missions, puis le responsable l’attribue à un livreur.

Le numéro client reste stable pour toutes ses visites. Chaque dépôt reçoit un nouveau numéro de ticket. Le personnel retrouve ainsi le même client depuis l’accueil, l’atelier ou la caisse sans réutiliser son ticket précédent.

## Utiliser le site sur le réseau local

1. Sur le PC qui héberge la base, double-cliquer sur `run-site.bat`. Garder la fenêtre de terminal ouverte pendant l’utilisation.
2. Sur ce PC, ouvrir `http://localhost:5085`.
3. Sur un autre appareil du même réseau Wi-Fi/Ethernet, ouvrir `http://ADRESSE-IP-DU-PC:5085`. Pour trouver l’adresse IPv4 du PC serveur, exécuter `ipconfig` et repérer l’adresse de la carte réseau active.
4. Si Windows bloque la connexion, créer une règle entrante TCP pour le port `5085`, limitée au profil **Privé** du pare-feu Windows.

La base Access reste sur le PC serveur ; les appareils utilisent le site et ne doivent pas ouvrir ni partager directement le fichier `.accdb`. Fermer l’application bureau avant de lancer le site pour éviter deux moteurs écrivant simultanément dans la même base. Cette configuration est réservée au réseau local de confiance : ne pas exposer le port `5085` sur Internet. Pour un accès depuis l’extérieur, utiliser un hébergement HTTPS avec MySQL/MariaDB, comme décrit ci-dessous.

Le PC central doit rester allumé avec le site en cours d’exécution. Chaque employé ouvre l’adresse réseau dans son navigateur et se connecte avec son compte ; les changements sont alors visibles par les autres. L’application de bureau locale utilise sa propre base : ne la lancez pas en parallèle du serveur central.

Pour utiliser la fenêtre Windows sur les postes employés tout en gardant une seule base, configurez Pressings.Desktop/appsettings.json sur chaque poste avec OnlineUrl pointant vers l’adresse privée du PC central, par exemple http://192.168.1.20:5085. Le PC central seul lance le site. L’application de bureau accepte les adresses HTTPS publiques et les adresses HTTP du réseau privé ; elle refuse une adresse HTTP publique.

## Base MySQL et mise en ligne

Le lancement Windows garde Access par défaut. Le moteur sait aussi utiliser MySQL/MariaDB : configurez `DatabaseProvider=MySql` et `MYSQL_URL` (ou `ConnectionStrings__PressingDb`) dans l’environnement **du serveur hébergé**. Au premier démarrage, il crée les tables et la grille tarifaire. `MYSQL_URL` accepte une URL `mysql://...` ou une chaîne de connexion .NET. `Cookies__Secure=true` force les cookies de connexion à utiliser HTTPS.

Le serveur MariaDB de XAMPP détecté sur ce PC est local : il n’est joignable que depuis cette machine ou le réseau autorisé, ce n’est pas une base Internet. Ne l’exposez pas directement sur le port 3306. Pour partager les dossiers, hébergez le moteur ASP.NET et MySQL chez un fournisseur, définissez les variables secrètes sur le serveur, puis mettez l’adresse HTTPS du moteur dans `Pressings.Desktop/appsettings.json` (`OnlineUrl`). La fenêtre Windows reste l’interface utilisée ; le mot de passe MySQL reste côté serveur. Le fichier `Dockerfile` prépare le moteur web pour un hébergeur Linux. Aucun compte d’hébergement ni domaine distant n’est encore configuré.

## Publication Windows

Double-cliquer sur `publish.bat` pour produire `release\FreshCare\Pressings.Desktop.exe` et son moteur local. Le programme publié crée une base privée dans `%LOCALAPPDATA%\FreshCare` et ne copie pas les fiches clients du dossier de développement. Le programme reste une application de bureau, sans onglet ni barre d’adresse de navigateur. Pour une utilisation en ligne par plusieurs appareils, connecter ce même client Windows à l'application hébergée après création du compte cloud.

La recherche de marché et ses limites sont décrites dans [RECHERCHE_MARCHE.md](RECHERCHE_MARCHE.md).
# E-mails de disponibilité

Le menu **Communication** est réservé au compte gérant. Il rassemble les tickets contenant au moins un article prêt, ouvre une conversation WhatsApp avec un texte prérempli et permet de composer une notification e-mail. Les envois réussis ou échoués sont conservés dans l’historique gérant.

Deux façons d’envoyer un e-mail sont proposées :

- **Ouvrir Gmail** prépare un brouillon dans Gmail, et **Autre messagerie** utilise l’application de courrier configurée sur Windows ; vérifiez le texte et cliquez sur Envoyer.
- **Envoyer par Fresh & Care** transmet directement le message lorsque le SMTP est configuré.

Pour activer l’envoi direct, ouvrez **Communication → Configurer l'e-mail**, choisissez votre fournisseur, saisissez l’adresse d’envoi et vos identifiants SMTP, puis enregistrez. La fenêtre permet aussi d’envoyer un message de test à l’adresse de votre choix. Avec Gmail, activez la validation en deux étapes puis utilisez une clé d’application de 16 caractères (et non le mot de passe habituel) ; [créez-la dans les paramètres de sécurité Google](https://myaccount.google.com/apppasswords). Les paramètres sont enregistrés dans la base locale privée et ne doivent pas être ajoutés à GitHub.

```json
{
  "Email": {
    "Smtp": {
      "Host": "smtp.votre-fournisseur.example",
      "Port": 587,
      "EnableSsl": true,
      "User": "votre-adresse@exemple.com",
      "Password": "mot-de-passe-smtp-ou-mot-de-passe-application",
      "FromAddress": "votre-adresse@exemple.com",
      "FromName": "Fresh & Care — Pressing"
    }
  }
}
```

En déploiement serveur, configurez les clés SMTP comme variables d’environnement, en remplaçant les deux-points par des doubles traits de soulignement, par exemple `Email__Smtp__Host`. Sans identifiant et mot de passe SMTP valides, l’application ne peut pas transmettre directement des e-mails ; elle peut toujours préparer un brouillon dans Gmail ou le logiciel de courrier de Windows.
