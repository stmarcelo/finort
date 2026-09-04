using Finort.Data;
using Microsoft.Data.Sqlite;

namespace Finort.Tests;

public class Fase3MigrationTests
{
    [Fact]
    public void Migrate_CreatesFase3Tables()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var tables = new List<string>();
            using (var conn = new SqliteConnection($"Data Source={file}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT name FROM sqlite_master WHERE type='table' " +
                    "AND name IN ('Pessoas','Categorias','Subcategorias','Contas','Lancamentos')";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) tables.Add(reader.GetString(0));
            }

            foreach (var expected in new[] { "Pessoas", "Categorias", "Subcategorias", "Contas", "Lancamentos" })
            {
                Assert.Contains(expected, tables);
            }
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public void Migrate_SeedsCategoriasEProtegidas()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var transferencia = db.Subcategorias.SingleOrDefault(s => s.Nome == "Transferência");
            Assert.NotNull(transferencia);
            Assert.True(transferencia!.IsProtected);

            var acerto = db.Subcategorias.SingleOrDefault(s => s.Nome == "Acerto");
            Assert.NotNull(acerto);
            Assert.True(acerto!.IsProtected);

            Assert.Equal(13, db.Categorias.Count());
            Assert.True(db.Categorias.Single(c => c.Nome == "Financeiro").IsProtected);
            Assert.True(db.Categorias.Single(c => c.Nome == "Acerto de saldo").IsProtected);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}
