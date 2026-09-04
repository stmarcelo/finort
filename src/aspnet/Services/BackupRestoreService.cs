using System.Security.Cryptography;
using Finort.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public sealed record ResultadoOperacao(bool Ok, string? Erro);
public sealed record ValidacaoBackup(bool Ok, string? Erro);

public class BackupRestoreService
{
    private readonly DatabaseConfigStore _store;
    private readonly AuthService _auth;

    public BackupRestoreService(DatabaseConfigStore store, AuthService auth)
    {
        _store = store;
        _auth = auth;
    }

    public async Task<bool> PossuiSenhaBackupAsync()
    {
        var config = await _auth.GetConfiguracaoAsync();
        return config?.BackupPasswordCriptografada is not null;
    }

    public async Task<ResultadoOperacao> GerarBackupAsync(Stream destino, string senha)
    {
        var snapshot = Path.Combine(Path.GetTempPath(), $"cf-bkp-{Guid.NewGuid():N}.db");
        try
        {
            if (string.IsNullOrEmpty(senha))
                return new ResultadoOperacao(false, "Informe a senha de backup.");

            var config = await _auth.GetConfiguracaoAsync()
                ?? throw new InvalidOperationException("Configuração não encontrada.");

            if (config.BackupPasswordCriptografada is not null && !_auth.VerificarSenhaBackup(config, senha))
                return new ResultadoOperacao(false, "Senha de backup incorreta.");

            var caminhoDb = CaminhoDb();

            await using (var ctx = CriarCtx(caminhoDb))
            {
                await ctx.Database.OpenConnectionAsync();
                await using var cmd = ctx.Database.GetDbConnection().CreateCommand();
                cmd.CommandText = $"VACUUM INTO '{snapshot.Replace("'", "''")}';";
                await cmd.ExecuteNonQueryAsync();
            }

            var blob = BackupCrypto.Encrypt(await File.ReadAllBytesAsync(snapshot), senha);
            await destino.WriteAsync(blob);
            return new ResultadoOperacao(true, null);
        }
        catch (CryptographicException ex) { return new ResultadoOperacao(false, ex.Message); }
        catch (Exception ex) { return new ResultadoOperacao(false, "Falha ao gerar backup: " + ex.Message); }
        finally
        {
            if (File.Exists(snapshot)) File.Delete(snapshot);
        }
    }

    public async Task<ValidacaoBackup> ValidarBackupAsync(byte[] conteudo, string senha)
    {
        byte[] plano;
        try
        {
            plano = BackupCrypto.Decrypt(conteudo, senha);
        }
        catch (InvalidDataException)
        {
            return new ValidacaoBackup(false, "Arquivo de backup inválido.");
        }
        catch (CryptographicException)
        {
            return new ValidacaoBackup(false, "Senha de backup incorreta ou arquivo corrompido.");
        }

        var temp = Path.Combine(Path.GetTempPath(), $"cf-valid-{Guid.NewGuid():N}.db");
        try
        {
            await File.WriteAllBytesAsync(temp, plano);
            await using var ctx = CriarCtx(temp);
            await ctx.Database.OpenConnectionAsync();
            await using var cmd = ctx.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "PRAGMA integrity_check;";
            var resultado = (await cmd.ExecuteScalarAsync())?.ToString();
            if (resultado != "ok")
                return new ValidacaoBackup(false, "Backup corrompido: integrity_check falhou (" + resultado + ").");

            var temSchema = await ctx.Database
                .SqlQueryRaw<long>("SELECT COUNT(*) AS \"Value\" FROM sqlite_master WHERE type='table' AND name='Lancamentos'")
                .SingleAsync();
            return temSchema > 0
                ? new ValidacaoBackup(true, null)
                : new ValidacaoBackup(false, "Backup não contém o schema esperado.");
        }
        catch (Exception ex)
        {
            return new ValidacaoBackup(false, "Backup inválido: " + ex.Message);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public async Task<ResultadoOperacao> RestaurarAsync(byte[] conteudo, string senha)
    {
        var validacao = await ValidarBackupAsync(conteudo, senha);
        if (!validacao.Ok) return new ResultadoOperacao(false, validacao.Erro);

        var caminhoDb = CaminhoDb();
        var temp = Path.Combine(Path.GetTempPath(), $"cf-rest-{Guid.NewGuid():N}.db");
        try
        {
            var plano = BackupCrypto.Decrypt(conteudo, senha);
            await File.WriteAllBytesAsync(temp, plano);

            try
            {
                await using var ctxCheckpoint = CriarCtx(caminhoDb);
                await ctxCheckpoint.Database.OpenConnectionAsync();
                await using var cmdCheckpoint = ctxCheckpoint.Database.GetDbConnection().CreateCommand();
                cmdCheckpoint.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
                await cmdCheckpoint.ExecuteScalarAsync();
            }
            catch { }

            SqliteConnection.ClearAllPools();

            File.Copy(caminhoDb, caminhoDb + ".bak", overwrite: true);
            foreach (var ext in new[] { "-wal", "-shm" })
                if (File.Exists(caminhoDb + ext)) File.Delete(caminhoDb + ext);
            File.Move(temp, caminhoDb, overwrite: true);

            var caminhoBak = caminhoDb + ".bak";
            try { if (File.Exists(caminhoBak)) File.Delete(caminhoBak); } catch { }

            return new ResultadoOperacao(true, null);
        }
        catch (Exception ex)
        {
            return new ResultadoOperacao(false, "Falha ao restaurar: " + ex.Message);
        }
        finally
        {
            if (File.Exists(temp)) File.Delete(temp);
        }
    }

    public async Task<ResultadoOperacao> ExcluirTodosDadosAsync()
    {
        var config = _store.Get();
        var ehMySql = string.Equals(config.Provider, "MySql", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (ehMySql)
                return await ExcluirDadosMySqlAsync(config);
            else
                return await ExcluirDadosSqliteAsync();
        }
        catch (Exception ex)
        {
            return new ResultadoOperacao(false, "Falha ao excluir dados: " + ex.Message);
        }
    }

    private async Task<ResultadoOperacao> ExcluirDadosSqliteAsync()
    {
        var caminhoDb = CaminhoDb();

        await using (var ctx = CriarCtx(caminhoDb))
        {
            await ctx.Database.OpenConnectionAsync();
            await using var cmd = ctx.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "PRAGMA wal_checkpoint(TRUNCATE);";
            await cmd.ExecuteScalarAsync();
        }

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

        foreach (var ext in new[] { "", "-wal", "-shm" })
        {
            var arquivo = caminhoDb + ext;
            if (File.Exists(arquivo)) File.Delete(arquivo);
        }

        await using (var ctx = CriarCtx(caminhoDb))
        {
            await ctx.Database.MigrateAsync();
        }

        return new ResultadoOperacao(true, null);
    }

    private async Task<ResultadoOperacao> ExcluirDadosMySqlAsync(Models.Configuration.DatabaseConfig config)
    {
        var mysqlVersion = new MySqlServerVersion(new Version(8, 0, 36));

        await using (var ctx = new MySqlAppDbContext(
            DbContextOptionsBuilderFactory.Build<MySqlAppDbContext>(config, mysqlVersion)))
        {
            await ctx.Database.OpenConnectionAsync();
            await using var cmd = ctx.Database.GetDbConnection().CreateCommand();

            cmd.CommandText = "SET FOREIGN_KEY_CHECKS=0;";
            await cmd.ExecuteNonQueryAsync();

            var tabelas = await ctx.Database
                .SqlQueryRaw<string>(
                    "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE()")
                .ToListAsync();

            foreach (var tabela in tabelas)
            {
                cmd.CommandText = $"DROP TABLE IF EXISTS `{tabela}`;";
                await cmd.ExecuteNonQueryAsync();
            }

            cmd.CommandText = "SET FOREIGN_KEY_CHECKS=1;";
            await cmd.ExecuteNonQueryAsync();
        }

        await using (var ctx = new MySqlAppDbContext(
            DbContextOptionsBuilderFactory.Build<MySqlAppDbContext>(config, mysqlVersion)))
        {
            await ctx.Database.MigrateAsync();
        }

        return new ResultadoOperacao(true, null);
    }

    private string CaminhoDb()
    {
        var cs = _store.Get().Sqlite.ConnectionString;
        var builder = new SqliteConnectionStringBuilder(cs);
        return builder.DataSource;
    }

    private static AppDbContext CriarCtx(string caminho)
        => new(DbContextOptionsBuilderFactory.Build<AppDbContext>(new Models.Configuration.DatabaseConfig
        {
            Provider = "Sqlite",
            Sqlite = new() { ConnectionString = $"Data Source={caminho}" }
        }));
}
