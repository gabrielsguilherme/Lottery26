using Lottery26.Models;
using Microsoft.Data.Sqlite;

namespace Lottery26.Data;

public static class DatabaseManager
{
    private static readonly string DbPath = Path.Combine(Environment.CurrentDirectory, "lottery26.db");
    private static readonly string ConnectionString = $"Data Source={DbPath}";

    public static void Initialize()
    {
        var existed = File.Exists(DbPath);

        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        EnableForeignKeys(connection);
        CreateSchema(connection);

        Console.WriteLine(existed
            ? "[DB] Banco SQLite inicializado com sucesso."
            : "[DB] Banco SQLite criado com sucesso.");
    }

    public static void UpsertConcurso(Outcome outcome)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        EnableForeignKeys(connection);

        using var transaction = connection.BeginTransaction();
        try
        {
            var insertConcurso = connection.CreateCommand();
            insertConcurso.Transaction = transaction;
            insertConcurso.CommandText = @"
INSERT OR IGNORE INTO Concursos
(ConcursoId, DataSorteio, Dezena1, Dezena2, Dezena3, Dezena4, Dezena5, Dezena6)
VALUES
(@id, @data, @d1, @d2, @d3, @d4, @d5, @d6);";

            insertConcurso.Parameters.AddWithValue("@id", outcome.Id);
            insertConcurso.Parameters.AddWithValue("@data", outcome.Date.ToString("yyyy-MM-dd"));
            insertConcurso.Parameters.AddWithValue("@d1", outcome.First);
            insertConcurso.Parameters.AddWithValue("@d2", outcome.Second);
            insertConcurso.Parameters.AddWithValue("@d3", outcome.Third);
            insertConcurso.Parameters.AddWithValue("@d4", outcome.Four);
            insertConcurso.Parameters.AddWithValue("@d5", outcome.Five);
            insertConcurso.Parameters.AddWithValue("@d6", outcome.Sixth);
            insertConcurso.ExecuteNonQuery();

            var checkDezenas = connection.CreateCommand();
            checkDezenas.Transaction = transaction;
            checkDezenas.CommandText = "SELECT COUNT(*) FROM Dezenas WHERE ConcursoId = @id;";
            checkDezenas.Parameters.AddWithValue("@id", outcome.Id);
            var dezenasCount = Convert.ToInt32(checkDezenas.ExecuteScalar());

            if (dezenasCount == 0)
            {
                var dezenas = new[]
                {
                    outcome.First, outcome.Second, outcome.Third,
                    outcome.Four, outcome.Five, outcome.Sixth
                };

                foreach (var dezena in dezenas)
                {
                    var insertDezena = connection.CreateCommand();
                    insertDezena.Transaction = transaction;
                    insertDezena.CommandText = @"
INSERT INTO Dezenas (ConcursoId, Dezena)
VALUES (@concursoId, @dezena);";
                    insertDezena.Parameters.AddWithValue("@concursoId", outcome.Id);
                    insertDezena.Parameters.AddWithValue("@dezena", dezena);
                    insertDezena.ExecuteNonQuery();
                }
            }

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    public static long GetTotalConcursos()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM Concursos;";
        return (long)(command.ExecuteScalar() ?? 0L);
    }

    public static void PrintTopDezenas(int top)
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();

        var command = connection.CreateCommand();
        command.CommandText = @"
SELECT Dezena, Ocorrencias, Percentual
FROM EstatisticasDezenas
LIMIT $top;";
        command.Parameters.AddWithValue("$top", top);

        Console.WriteLine();
        Console.WriteLine($"=== Top {top} dezenas mais sorteadas (banco) ===");
        Console.WriteLine("Dezena   Ocorrências   Percentual");
        Console.WriteLine(new string('-', 36));

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            Console.WriteLine($"{reader.GetInt32(0),-8} {reader.GetInt32(1),-13} {reader.GetDouble(2):F4}%");
        }
    }

    public static void Cleanup()
    {
        using var connection = new SqliteConnection(ConnectionString);
        connection.Open();
        EnableForeignKeys(connection);

        var removeOrphans = connection.CreateCommand();
        removeOrphans.CommandText = @"
DELETE FROM Dezenas
WHERE ConcursoId NOT IN (SELECT ConcursoId FROM Concursos);";
        var orphanRows = removeOrphans.ExecuteNonQuery();
        Console.WriteLine($"[DB] Dezenas órfãs removidas: {orphanRows}");

        var removeDuplicates = connection.CreateCommand();
        removeDuplicates.CommandText = @"
DELETE FROM Dezenas
WHERE rowid NOT IN (
    SELECT MIN(rowid)
    FROM Dezenas
    GROUP BY ConcursoId, Dezena
);";
        var duplicateRows = removeDuplicates.ExecuteNonQuery();
        Console.WriteLine($"[DB] Dezenas duplicadas removidas: {duplicateRows}");

        var vacuum = connection.CreateCommand();
        vacuum.CommandText = "VACUUM;";
        vacuum.ExecuteNonQuery();
        Console.WriteLine("[DB] VACUUM executado.");
    }

    private static void CreateSchema(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = @"
CREATE TABLE IF NOT EXISTS Concursos (
    ConcursoId  INTEGER PRIMARY KEY,
    DataSorteio TEXT NOT NULL,
    Dezena1     INTEGER NOT NULL,
    Dezena2     INTEGER NOT NULL,
    Dezena3     INTEGER NOT NULL,
    Dezena4     INTEGER NOT NULL,
    Dezena5     INTEGER NOT NULL,
    Dezena6     INTEGER NOT NULL,
    ImportadoEm TEXT NOT NULL DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS Dezenas (
    Id         INTEGER PRIMARY KEY AUTOINCREMENT,
    ConcursoId INTEGER NOT NULL,
    Dezena     INTEGER NOT NULL,
    FOREIGN KEY (ConcursoId) REFERENCES Concursos(ConcursoId) ON DELETE CASCADE
);

CREATE INDEX IF NOT EXISTS idx_dezenas_concurso ON Dezenas(ConcursoId);
CREATE INDEX IF NOT EXISTS idx_dezenas_numero   ON Dezenas(Dezena);

CREATE VIEW IF NOT EXISTS EstatisticasDezenas AS
SELECT
    d.Dezena,
    COUNT(*) AS Ocorrencias,
    ROUND(COUNT(*) * 100.0 / (SELECT COUNT(*) FROM Dezenas), 4) AS Percentual
FROM Dezenas d
GROUP BY d.Dezena
ORDER BY Ocorrencias DESC, d.Dezena ASC;";
        command.ExecuteNonQuery();
    }

    private static void EnableForeignKeys(SqliteConnection connection)
    {
        var command = connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON;";
        command.ExecuteNonQuery();
    }
}
