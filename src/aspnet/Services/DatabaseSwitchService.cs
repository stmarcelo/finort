using Finort.Data;
using Finort.Models.Configuration;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Finort.Services;

public class DatabaseSwitchService
{
    private static readonly Version MySqlVersion = new(8, 0, 36);

    private readonly AppDbContext _origem;
    private readonly DatabaseConfigStore _store;
    private readonly ILogger<DatabaseSwitchService> _logger;

    public DatabaseSwitchService(AppDbContext origem, DatabaseConfigStore store,
        ILogger<DatabaseSwitchService> logger)
    {
        _origem = origem;
        _store = store;
        _logger = logger;
    }

    public async Task TestarConexaoAsync(DatabaseConfig destino)
    {
        try
        {
            await using var ctx = CriarContexto(destino);
            await ctx.Database.OpenConnectionAsync();
            await using var cmd = ctx.Database.GetDbConnection().CreateCommand();
            cmd.CommandText = "SELECT 1";
            await cmd.ExecuteScalarAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao testar conexão de destino.");
            throw new InvalidOperationException(
                "Não foi possível conectar ao banco de destino: " + ex.Message, ex);
        }
    }

    public async Task ExecutarAsync(DatabaseConfig destino)
    {
        var ehMySql = string.Equals(destino.Provider, "MySql", StringComparison.OrdinalIgnoreCase);

        await using (var ctx = CriarContexto(destino))
        {
            var conn = ctx.Database.GetDbConnection();
            await ctx.Database.OpenConnectionAsync();

            if (ehMySql)
            {
                await LimparTabelasMySqlAsync(ctx);
                try
                {
                    await ctx.Database.MigrateAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Migrations do MySQL falharam após limpeza do schema.");
                    throw new InvalidOperationException(
                        "Falha ao aplicar migrations no MySQL: " + ex.Message, ex);
                }
            }
            else
            {
                await ctx.Database.MigrateAsync();
            }

            if (!ehMySql)
                await ExecutarAsync(conn, "PRAGMA foreign_keys=OFF;");
            else
                await ExecutarAsync(conn, "SET FOREIGN_KEY_CHECKS=0;");

            await using var transacao = await ctx.Database.BeginTransactionAsync();
            try
            {
                await ZerarBancoAsync(ctx, ehMySql, transacao.GetDbTransaction());
                await CopiarDadosAsync(ctx, ehMySql);
                await transacao.CommitAsync();
            }
            catch
            {
                await transacao.RollbackAsync();
                throw;
            }
            finally
            {
                if (!ehMySql)
                    await ExecutarAsync(conn, "PRAGMA foreign_keys=ON;");
                else
                    await ExecutarAsync(conn, "SET FOREIGN_KEY_CHECKS=1;");
            }
        }

        _store.Set(destino);
        _logger.LogInformation("Banco trocado para {Provider}.", destino.Provider);
    }

    /// <summary>Hospedagem compartilhada: sem privilégio de criar/dropar o database,
    /// removemos todas as tabelas do schema atual para que as migrations rodem do zero.</summary>
    private static async Task LimparTabelasMySqlAsync(AppDbContext ctx)
    {
        var conn = ctx.Database.GetDbConnection();
        await ExecutarAsync(conn, "SET FOREIGN_KEY_CHECKS=0;");
        try
        {
            var tabelas = new List<string>();
            await using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT TABLE_NAME FROM information_schema.TABLES " +
                    "WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE';";
                await using var reader = await cmd.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                    tabelas.Add(reader.GetString(0));
            }
            foreach (var tabela in tabelas)
                await ExecutarAsync(conn, $"DROP TABLE IF EXISTS `{tabela}`;");
        }
        finally
        {
            await ExecutarAsync(conn, "SET FOREIGN_KEY_CHECKS=1;");
        }
    }

    private static AppDbContext CriarContexto(DatabaseConfig config)
    {
        return string.Equals(config.Provider, "MySql", StringComparison.OrdinalIgnoreCase)
            ? new MySqlAppDbContext(DbContextOptionsBuilderFactory.Build<MySqlAppDbContext>(config, new MySqlServerVersion(MySqlVersion)))
            : new AppDbContext(DbContextOptionsBuilderFactory.Build<AppDbContext>(config));
    }

    private static async Task ZerarBancoAsync(AppDbContext ctx, bool ehMySql,
        System.Data.Common.DbTransaction transacao)
    {
        var conn = ctx.Database.GetDbConnection();

        var tabelas = ctx.Model.GetEntityTypes()
            .Select(t => t.GetTableName())
            .Where(n => !string.IsNullOrEmpty(n))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var tabela in tabelas)
            await ExecutarAsync(conn, transacao, ehMySql
                ? $"DELETE FROM `{tabela}`;"
                : $"DELETE FROM \"{tabela}\";");
    }

    private static Task ExecutarAsync(System.Data.Common.DbConnection conn, string sql)
        => ExecutarAsync(conn, null, sql);

    private static async Task ExecutarAsync(System.Data.Common.DbConnection conn,
        System.Data.Common.DbTransaction? transacao, string sql)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = transacao;
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    protected virtual async Task CopiarDadosAsync(AppDbContext destino, bool ehMySql)
    {
        var entityTypes = _origem.Model.GetEntityTypes().ToList();

        foreach (var entityType in entityTypes)
        {
            var tableName = entityType.GetTableName();
            if (string.IsNullOrEmpty(tableName)) continue;

            var clrType = entityType.ClrType;
            var method = typeof(DatabaseSwitchService)
                .GetMethod(nameof(CopiarTabela), System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
                .MakeGenericMethod(clrType);

            await (Task)method.Invoke(null, new object[] { _origem, destino })!;
        }
    }

    private static async Task CopiarTabela<T>(AppDbContext origem, AppDbContext destino) where T : class
    {
        var dados = await origem.Set<T>().AsNoTracking().ToListAsync();
        if (dados.Count == 0) return;
        destino.Set<T>().AddRange(dados);
        await destino.SaveChangesAsync();
    }
}
