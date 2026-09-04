using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class ProjetosMigrationTests
{
    [Fact]
    public void Migrate_CriaTabelaProjetos_EColunaProjetoId()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            using (var conn = new SqliteConnection($"Data Source={file}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='Projetos'";
                Assert.Equal(1L, (long)(cmd.ExecuteScalar() ?? 0L));

                using var cmd2 = conn.CreateCommand();
                cmd2.CommandText =
                    "SELECT COUNT(*) FROM pragma_table_info('Lancamentos') WHERE name='ProjetoId'";
                Assert.Equal(1L, (long)(cmd2.ExecuteScalar() ?? 0L));

                using var cmd3 = conn.CreateCommand();
                cmd3.CommandText =
                    "SELECT COUNT(*) FROM pragma_table_info('Configuracoes') WHERE name='BackupPasswordCriptografada'";
                Assert.Equal(1L, (long)(cmd3.ExecuteScalar() ?? 0L));
            }

            var pessoa = new Models.Financeiro.Pessoa { Nome = "Cliente" };
            db.Pessoas.Add(pessoa);
            var projeto = new Models.Financeiro.Projeto
            {
                Descricao = "Site institucional",
                DataContratacao = new DateOnly(2026, 8, 1),
                ValorContratado = 15000m,
                PessoaId = pessoa.Id
            };
            db.Projetos.Add(projeto);
            db.SaveChanges();
            Assert.True(db.Projetos.Any(p => p.Descricao == "Site institucional"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}
