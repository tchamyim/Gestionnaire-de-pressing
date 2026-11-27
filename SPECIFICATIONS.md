# Application de gestion de pressing

## Objectif

Créer une application desktop pour gérer les clients, les dépôts d’articles, leur traitement, les retraits et les paiements, avec un site permettant à chaque client de consulter sa situation.

## 1. Application desktop du pressing

### Clients

- Créer et modifier une fiche client avec : nom, prénom, numéro de téléphone et numéro WhatsApp.
- Rechercher rapidement un client.
- Consulter l’historique des dépôts, retraits et opérations associés à un client.
- Voir le solde du client et le détail des montants impayés.

### Dépôts et articles

- Enregistrer un dépôt pour un client et générer un numéro de ticket unique.
- Ajouter plusieurs articles à un dépôt. Chaque article est associé à une catégorie de la grille tarifaire.
- Enregistrer les dates de dépôt et de retrait prévues pour chaque article. Conserver également la date du retrait effectif.
- Permettre de sélectionner le service de lavage express.
- Suivre chaque article avec l’un des statuts suivants : déposé, en traitement, prêt, retiré.
- Consulter l’historique des changements de statut et des opérations de dépôt/retrait.

### Tarifs et caisse

- Afficher sur l’écran d’accueil la grille tarifaire, organisée par catégories d’articles.
- Calculer les montants dus à partir des articles, des tarifs applicables et du service express, selon les règles tarifaires à définir.
- Afficher le total en caisse après chaque retrait/paiement.
- Afficher les impayés, avec le montant dû par client.

### Écrans principaux envisagés

- Accueil : grille tarifaire, recherche rapide, résumé de caisse et accès aux impayés.
- Clients : création, recherche et consultation des fiches.
- Nouveau dépôt : client, articles, dates, option express et ticket.
- Suivi des articles : statut, dates et historique.
- Retrait et paiement : articles remis, règlement reçu et solde restant.

## 2. Site / espace client

- Permettre au client de se connecter à son espace.
- Afficher son historique.
- Afficher le montant qu’il doit payer.
- Indiquer quels vêtements sont prêts.
- Générer et télécharger un reçu au format PDF.

## 3. Données principales

- **Client** : identité et coordonnées.
- **Dépôt / ticket** : numéro unique, client, date et état général éventuel.
- **Article** : catégorie, prix appliqué, option express, dates et statut.
- **Opération** : dépôt, retrait, paiement ou changement de statut, avec date et historique.
- **Paiement** : montant reçu, date, moyen de paiement à définir et solde restant.
- **Tarif** : catégorie d’article et prix en vigueur.

## 4. Règles métier de la première version

- Un ticket unique est attribué à chaque dépôt, qui peut contenir plusieurs articles.
- Les articles peuvent être suivis et retirés individuellement.
- Le montant dû correspond au total des articles et options express, moins les paiements déjà reçus.
- La caisse espèces augmente avec les règlements en espèces et les entrées, et diminue avec les sorties manuelles. Mobile Money, virements et cartes diminuent l’impayé sans augmenter le tiroir espèces.
- L’express est un pourcentage par catégorie, modifiable dans la grille tarifaire. La valeur de démarrage de 30 % est un exemple.
- Le client se connecte avec son numéro de téléphone et un PIN créé par le pressing. Le PIN est haché dans la base et ne s’affiche qu’une fois lors de sa création ou réinitialisation.
- La base utilise le FCFA. L’application produit un PDF de ticket de dépôt et un PDF pour chaque paiement.
- Les montants préchargés dans la grille sont des exemples à remplacer par la grille réelle du pressing. Les sources de marché consultées n’ont pas fourni de tarifs vérifiables ; voir [RECHERCHE_MARCHE.md](RECHERCHE_MARCHE.md).

## 5. Périmètre initial proposé

La première version peut couvrir la gestion des clients, dépôts, articles, statuts, tarifs, paiements, impayés et tickets. L’espace client et les reçus PDF pourront ensuite s’appuyer sur ces mêmes données.

