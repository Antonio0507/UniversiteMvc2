# Gestion universitaire

Application web de scolarité (ASP.NET Core MVC, .NET 10, MySQL/MariaDB) : étudiants, inscriptions, notes et relevés,
paiements, tarifs et impayés, enseignement, documents PDF, comptes et journal d'audit.

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

Ensuite, dans l'application : années académiques, filières, UE, tarifs, puis comptes du personnel.

## Profils

| Profil | Écrit dans | Lit |
| --- | --- | --- |
| Administrateur | tout, comptes inclus | tout, journal d'audit inclus |
| Scolarité | étudiants, inscriptions, notes, enseignement, années | tout sauf administration |
| Comptabilité | paiements, tarifs | tout sauf administration |
| Consultation | rien | tout sauf administration, sans export CSV |

Tout le monde peut générer les documents PDF. Pour un mot de passe oublié, un administrateur le réinitialise depuis **Comptes**.

## Exploitation

- **Mise à jour** : `git pull && docker compose up -d --build`. Les migrations s'appliquent au démarrage. Sauvegardez d'abord.
- **Sauvegarde** : planifier `deploy/sauvegarde.sh` (voir l'en-tête du script) et copier `sauvegardes/` hors du serveur.
- **Restauration** : `gunzip -c sauvegardes/FICHIER.sql.gz | docker compose exec -T base sh -c 'exec mariadb -uroot -p"$MARIADB_ROOT_PASSWORD" "$MARIADB_DATABASE"'`
- **Supervision** : `GET /sante` répond 200 si l'application et la base fonctionnent, 503 sinon.
- **Volume `cles`** : à conserver. Le perdre déconnecte tout le monde, sans perte de données.

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
