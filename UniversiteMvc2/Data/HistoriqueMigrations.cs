using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Storage;

namespace UniversiteMvc2.Data;

// Remplace l'historique de migrations du fournisseur MySQL, qui prend son verrou avec GET_LOCK(…, -1) :
// MariaDB répond NULL à un délai négatif, ce qui fait échouer toute migration.
// Un délai positif fonctionne sur MySQL comme sur MariaDB.
public class HistoriqueMigrations(HistoryRepositoryDependencies dependances) : HistoryRepository(dependances)
{
    private const string Nom = "__EFMigrationsLock";
    private const int DelaiSecondes = 300;

    public override LockReleaseBehavior LockReleaseBehavior => LockReleaseBehavior.Connection;

    // LOWER : sous Windows, le serveur enregistre souvent les noms de tables en minuscules
    protected override string ExistsSql =>
        $"SELECT 1 FROM information_schema.tables WHERE table_schema = DATABASE() AND LOWER(table_name) = LOWER('{TableName}') LIMIT 1;";

    protected override bool InterpretExistsResult(object? value) => value != null && value != DBNull.Value;

    public override string GetCreateIfNotExistsScript() =>
        GetCreateScript().Replace("CREATE TABLE", "CREATE TABLE IF NOT EXISTS");

    // Scripts SQL idempotents : non utilisés, les migrations s'appliquent au démarrage de l'application
    public override string GetBeginIfNotExistsScript(string migrationId) => throw new NotSupportedException();

    public override string GetBeginIfExistsScript(string migrationId) => throw new NotSupportedException();

    public override string GetEndIfScript() => throw new NotSupportedException();

    public override IMigrationsDatabaseLock AcquireDatabaseLock()
    {
        Verifier(Commande($"SELECT GET_LOCK('{Nom}', {DelaiSecondes});").ExecuteScalar(Parametres()));
        return new Verrou(this);
    }

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
