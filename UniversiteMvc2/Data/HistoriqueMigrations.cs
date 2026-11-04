using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace UniversiteMvc2.Data;

/// <summary>
/// Remplace l'historique de migrations du fournisseur MySQL, qui prend son verrou avec GET_LOCK(…, -1) : MariaDB
/// répond NULL à un délai négatif, ce qui fait échouer toute migration. Un délai positif fonctionne sur MySQL comme
/// sur MariaDB.
/// </summary>
/// <param name="dependances">Services fournis par EF Core.</param>
public class HistoriqueMigrations(HistoryRepositoryDependencies dependances) : HistoryRepository(dependances)
{
    private const string Nom = "__EFMigrationsLock";
    private const int DelaiSecondes = 300;

    /// <summary>
    /// Le verrou est lié à la connexion : il tombe tout seul si l'application s'arrête en pleine migration.
    /// </summary>
    public override LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Connection;

    /// <summary>LOWER : sous Windows, le serveur enregistre souvent les noms de tables en minuscules</summary>
    protected override string ExistsSql =>
        $"SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND LOWER(table_name) = LOWER('{TableName}') LIMIT 1;";

    /// <summary>Interprète la requête ExistsSql : la table d'historique existe si une ligne est renvoyée.</summary>
    protected override bool InterpretExistsResult(object? value) => value != null && value != DBNull.Value;

    /// <summary>Script de création de la table d'historique, sans erreur si elle existe déjà.</summary>
    public override string GetCreateIfNotExistsScript() =>
        GetCreateScript().Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS");

    /// <summary>
    /// Scripts SQL idempotents : non utilisés, les migrations s'appliquent au démarrage de l'application
    /// </summary>
    public override string GetBeginIfNotExistsScript(string migrationId) => throw new NotSupportedException();

    /// <summary>Non utilisé : voir GetBeginIfNotExistsScript.</summary>
    public override string GetBeginIfExistsScript(string migrationId) => throw new NotSupportedException();

    /// <summary>Non utilisé : voir GetBeginIfNotExistsScript.</summary>
    public override string GetEndIfScript() => throw new NotSupportedException();

    /// <summary>Prend le verrou de migration avec un délai de 300 secondes, compatible MySQL et MariaDB.</summary>
    public override IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        Verifier(Commande($"SELECT GET_LOCK('{Nom}', {DelaiSecondes});").ExecuteScalar(Parametres()));
        return new Verrou(this);
    }

    /// <summary>Version asynchrone de AcquireDatabaseLock.</summary>
    /// <param name="cancellationToken">Annulation de l'attente.</param>
    public override async Task<IMigrationsDatabaseLock> AcquireDatabaseLockAsync(CancellationToken cancellationToken = default)
    {
        Verifier(await Commande($"SELECT GET_LOCK('{Nom}', {DelaiSecondes});").ExecuteScalarAsync(Parametres(), cancellationToken));
        return new Verrou(this);
    }

    private static void Verifier(object? resultat)
    {
        if (resultat is null or DBNull || Convert.ToInt64(resultat) != 1)
            throw new TimeoutException("Une autre instance applique déjà les migrations.");
    }

    private IRelationalCommand Commande(string sql) => Dependencies.RawSqlCommandBuilder.Build(sql);

    private RelationalCommandParameterObject Parametres() =>
        new(Dependencies.Connection, null, null, Dependencies.CurrentContext.Context, Dependencies.CommandLogger);

    private sealed class Verrou(HistoriqueMigrations historique) : IMigrationsDatabaseLock
    {
        public IHistoryRepository HistoryRepository => historique;

        public void Dispose() =>
            historique.Commande($"SELECT RELEASE_LOCK('{Nom}');").ExecuteScalar(historique.Parametres());

        public async ValueTask DisposeAsync() =>
            await historique.Commande($"SELECT RELEASE_LOCK('{Nom}');").ExecuteScalarAsync(historique.Parametres());
    }
}
