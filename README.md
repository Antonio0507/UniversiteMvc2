# Gestion universitaire

Application web de scolarité (ASP.NET Core MVC, .NET 10, MySQL/MariaDB) : étudiants, inscriptions, notes et relevés,
délibérations, assiduité, paiements, tarifs, bourses et impayés, enseignement, emploi du temps et salles,
documents PDF, inscription en ligne et espace étudiant, comptes, permissions et journal d'audit.

Une installation sert un seul établissement, avec sa propre base.

## Installer chez un client

Prérequis : un serveur Linux avec Docker, et un nom de domaine qui pointe dessus (ports 80 et 443 ouverts).

```sh
cp .env.example .env      # puis remplir chaque valeur
docker compose up -d --build
docker compose logs application | grep "Premier administrateur"
```

Le journal affiche une seule fois le mot de passe provisoire de l'administrateur, à changer à la première connexion.
Pour le fixer soi-même, définir `Admin__MotDePasse` dans l'environnement du service `application`.

Ensuite, dans l'application : années académiques, filières, UE, salles, tarifs, puis comptes du personnel.

## Profils et permissions

Droits par défaut :

| Profil | Écrit dans | Lit |
| --- | --- | --- |
| Administrateur | tout, comptes et permissions inclus | tout, journal d'audit inclus |
| Scolarité | étudiants, inscriptions, notes, délibérations, assiduité, enseignement, emploi du temps, salles, années | tout sauf administration |
| Comptabilité | paiements, tarifs, bourses et exonérations | tout sauf administration |
| Consultation | rien | tout sauf administration, sans export CSV |
| Étudiant | sa demande d'inscription | son espace uniquement |

Tout le personnel peut générer les documents PDF. Pour un mot de passe oublié, un administrateur le réinitialise depuis **Comptes**.

L'administrateur règle ces droits dans **Administration › Permissions** : pour chaque profil du personnel (Scolarité,
Comptabilité, Consultation) et chaque section, *Aucun accès*, *Lecture* ou *Écriture* (écriture = créer, modifier,
supprimer et exporter). Les changements s'appliquent dès la page suivante, sans reconnexion, et sont tracés dans le
journal d'audit ; **Rétablir les valeurs par défaut** revient au tableau ci-dessus. L'administrateur garde toujours
tous les droits, et Comptes, Permissions et Journal ne se délèguent pas.

## Étudiants

- **Inscription en ligne** (lien sur la page de connexion) : l'étudiant saisit son identité, la filière et le niveau
  demandés et choisit son mot de passe. Son dossier est créé avec un matricule, son inscription est *En attente* pour
  l'année en cours (sinon la prochaine), et il est connecté à son espace. La scolarité valide l'inscription depuis sa fiche.
- **Activation** : un étudiant dont le dossier a été créé par la scolarité se crée un compte avec son matricule, sa
  date de naissance et l'e-mail de son dossier.
- **Mon espace** : statut de l'inscription, frais, aides, paiements et reste à payer, notes et décision, absences,
  emploi du temps de la semaine ; attestation et relevé de notes PDF une fois l'inscription validée, relevé des
  paiements ; demande d'inscription pour l'année suivante. Un étudiant ne voit que son propre dossier.
- Les comptes étudiants apparaissent dans **Comptes › Étudiants**, où l'administrateur peut les désactiver ou
  réinitialiser leur mot de passe. L'adresse e-mail n'est pas vérifiée par envoi de message.

Si plus aucun administrateur ne peut se connecter, depuis le serveur :
`docker compose exec application dotnet UniversiteMvc2.dll reinitialiser-mot-de-passe admin@exemple.tg`
(en développement : `dotnet run -- reinitialiser-mot-de-passe admin@exemple.tg`). Le mot de passe provisoire s'affiche sur la console.

## Règles de gestion

- **Délibérations** : une UE est validée à partir de 10/20 (meilleure des deux sessions). Toutes les UE validées : *Admis* ;
  sinon une moyenne pondérée d'au moins 10 : *Admis par compensation* ; sinon *Ajourné*. Une UE sans note : *Incomplet*.
  Les ex aequo partagent le même rang. Le procès-verbal PDF se génère par promotion (année, filière, niveau).
- **Bourses et exonérations** : déduites du tarif pour le reste à payer, les impayés, les reçus et les relevés ; leur total ne peut pas dépasser le tarif.
- **Emploi du temps** : une salle, un enseignant ou une promotion ne peuvent pas avoir deux séances qui se chevauchent.
  Un cours ou une UE qui a des séances programmées ne peut plus être supprimé.
- **Assiduité** : l'appel se fait depuis une séance ; les étudiants attendus sont les inscriptions validées de l'année
  qui contient la date de la séance, dans la filière et le niveau de l'UE.

## Exploitation

- **Mise à jour** : `git pull && docker compose up -d --build`. Les migrations s'appliquent au démarrage. Sauvegardez d'abord.
- **Sauvegarde** : planifier `deploy/sauvegarde.sh` (voir l'en-tête du script) et copier `sauvegardes/` hors du serveur.
- **Restauration** : `gunzip -c sauvegardes/FICHIER.sql.gz | docker compose exec -T base sh -c 'exec mariadb -uroot -p"$MARIADB_ROOT_PASSWORD" "$MARIADB_DATABASE"'`
- **Supervision** : `GET /sante` répond 200 si l'application et la base fonctionnent, 503 sinon.
- **Logs** : `docker compose logs application`. Connexions, refus d'accès et pannes y figurent avec l'heure UTC et l'adresse IP ; la « Référence » affichée sur une page d'erreur 500 s'y retrouve. Le journal d'audit, lui, se consulte dans l'application.
- **Volume `cles`** : à conserver. Le perdre déconnecte tout le monde, sans perte de données.

## Tester sans Docker

Prérequis : le [SDK .NET 10](https://dotnet.microsoft.com/download) et un serveur MySQL ou MariaDB.
Sous Windows, le plus simple est XAMPP (ou WAMP, Laragon), qui installe MariaDB et phpMyAdmin.

1. Démarrer MySQL depuis le panneau XAMPP (port 3306, utilisateur `root` sans mot de passe par défaut).
2. Dans phpMyAdmin, créer une base vide `gestion_universitaire` (interclassement `utf8mb4_general_ci`).
3. Dans PowerShell, depuis le dossier `UniversiteMvc2` :
   ```powershell
   $env:ConnectionStrings__DefaultConnection = "Server=localhost;Port=3306;Database=gestion_universitaire;User=root;Password=;"
   $env:Admin__MotDePasse = "Test-admin-2026"
   dotnet run
   ```
4. Ouvrir http://localhost:5267 et se connecter avec `admin@universite.local` / `Test-admin-2026`
   (le mot de passe est à changer à la première connexion).

Les tables sont créées au premier lancement, et une base vide reçoit le jeu d'essai fictif : tout se teste immédiatement.
La chaîne de connexion de `appsettings.json` vise le port 3307 du conteneur Docker : sans la variable ci-dessus, la base est introuvable.

Pour les tests automatisés, définir `TEST_MYSQL` vers le même serveur (`Server=127.0.0.1;Port=3306;User=root;Password=;`), puis `dotnet test` (voir [Tests](#tests)).

## Développer

```sh
cd UniversiteMvc2
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Server=localhost;Port=3307;Database=gestion_universitaire;User=universite;Password=..."
dotnet run
```

En développement, une base vide reçoit un jeu d'essai fictif (`Demarrage:DonneesDemo`). Ce réglage reste à `false` en production.

Nouvelle migration : `dotnet tool restore`, puis `dotnet tool run dotnet-ef migrations add NomDeLaMigration --output-dir Data/Migrations`.

### Tests

```sh
cd UniversiteMvc2.Tests
TEST_MYSQL='Server=127.0.0.1;Port=3306;User=root;Password=...;' dotnet test
```

`TEST_MYSQL` désigne un serveur où le compte peut créer des bases : chaque test crée puis supprime sa propre base temporaire.
Sans cette variable, les tests de parcours sont signalés comme ignorés et seules les règles métier s'exécutent.
