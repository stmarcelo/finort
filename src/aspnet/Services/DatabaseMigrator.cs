using System.Data.Common;
using System.Globalization;
using Finort.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace Finort.Services;

public class DatabaseMigrator
{
    // Initial única (squash): imagem atual do modelo em cada provedor.
    private const string InitialSqlite = "20260911232404_Initial";
    private const string InitialMySql = "20260911232421_Initial";

    private static readonly string[] ColunasLegadasReembolso =
        ["ReembolsoId", "ReembolsoCategoriaId", "ReembolsoSubcategoriaId"];

    private readonly AppDbContext _db;
    private readonly ILogger<DatabaseMigrator> _logger;

    public DatabaseMigrator(AppDbContext db, ILogger<DatabaseMigrator> logger)
    {
        _db = db;
        _logger = logger;
    }

    public void Migrate()
    {
        try
        {
            _logger.LogInformation("Applying database migrations...");
            var conn = _db.Database.GetDbConnection();
            var sqlite = conn is SqliteConnection;

            if (!TabelaExisteAberta(conn, sqlite, "Lancamentos"))
            {
                // Banco novo: aplica a Initial única.
                _db.Database.GetService<IMigrator>().Migrate();
            }
            else
            {
                // Banco existente (atual ou backup antigo): garante os objetos
                // do modelo atual, converte reembolsos legados e carimba a Initial.
                var openedHere = false;
                if (conn.State != System.Data.ConnectionState.Open)
                {
                    conn.Open();
                    openedHere = true;
                }
                try
                {
                    GarantirTabelaReembolsos(conn, sqlite);
                    GarantirColunasModelo(conn, sqlite);
                    ConverterReembolsosLegados();
                    RemoverColunasLegadas(conn, sqlite);
                    CarimbarInitialSeAusente(conn, sqlite);
                }
                finally
                {
                    if (openedHere) conn.Close();
                }
                _db.Database.GetService<IMigrator>().Migrate();
            }
            _logger.LogInformation("Migrations applied successfully");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Migrate failed, attempting recovery...");
            TryRecoverMigrationHistory(ex);
        }
    }

    private string InitialId()
    {
        var conn = _db.Database.GetDbConnection();
        return conn is SqliteConnection ? InitialSqlite : InitialMySql;
    }

    /// <summary>Cria a tabela Reembolsos em bancos que já existiam antes dela
    /// (restaurações antigas). No-op quando já existe.</summary>
    private void GarantirTabelaReembolsos(DbConnection conn, bool sqlite)
    {
        if (TabelaExiste(conn, sqlite, "Reembolsos")) return;
        _logger.LogInformation("Tabela Reembolsos ausente; criando via SQL direto");
        if (sqlite)
        {
            ExecuteSql(conn, """
                CREATE TABLE IF NOT EXISTS Reembolsos (
                    Id TEXT NOT NULL CONSTRAINT PK_Reembolsos PRIMARY KEY,
                    PessoaId TEXT NOT NULL,
                    CartaoCreditoId TEXT NOT NULL,
                    LancamentoId TEXT NOT NULL,
                    ParcelaAtual INTEGER NULL,
                    TotalParcelas INTEGER NULL,
                    Valor TEXT NOT NULL,
                    Vencimento TEXT NOT NULL,
                    Fechado INTEGER NOT NULL,
                    DataFechamento TEXT NULL,
                    ReceitaId TEXT NULL,
                    CONSTRAINT FK_Reembolsos_Lancamentos_LancamentoId FOREIGN KEY (LancamentoId) REFERENCES Lancamentos (Id) ON DELETE CASCADE
                )
                """);
            ExecuteSql(conn, "CREATE UNIQUE INDEX IF NOT EXISTS IX_Reembolsos_LancamentoId ON Reembolsos (LancamentoId)");
            ExecuteSql(conn, "CREATE INDEX IF NOT EXISTS IX_Reembolsos_CartaoCreditoId_Vencimento ON Reembolsos (CartaoCreditoId, Vencimento)");
            ExecuteSql(conn, "CREATE INDEX IF NOT EXISTS IX_Reembolsos_PessoaId_Vencimento ON Reembolsos (PessoaId, Vencimento)");
            ExecuteSql(conn, "CREATE INDEX IF NOT EXISTS IX_Reembolsos_ReceitaId ON Reembolsos (ReceitaId)");
        }
        else
        {
            ExecuteSql(conn, """
                CREATE TABLE IF NOT EXISTS `Reembolsos` (
                    `Id` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `PessoaId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `CartaoCreditoId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `LancamentoId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NOT NULL,
                    `ParcelaAtual` int NULL,
                    `TotalParcelas` int NULL,
                    `Valor` decimal(65,30) NOT NULL,
                    `Vencimento` date NOT NULL,
                    `Fechado` tinyint(1) NOT NULL,
                    `DataFechamento` datetime(6) NULL,
                    `ReceitaId` char(36) CHARACTER SET ascii COLLATE ascii_general_ci NULL,
                    CONSTRAINT `PK_Reembolsos` PRIMARY KEY (`Id`),
                    CONSTRAINT `FK_Reembolsos_CartoesCredito_CartaoCreditoId` FOREIGN KEY (`CartaoCreditoId`) REFERENCES `CartoesCredito` (`Id`) ON DELETE RESTRICT,
                    CONSTRAINT `FK_Reembolsos_Lancamentos_LancamentoId` FOREIGN KEY (`LancamentoId`) REFERENCES `Lancamentos` (`Id`) ON DELETE CASCADE,
                    CONSTRAINT `FK_Reembolsos_Lancamentos_ReceitaId` FOREIGN KEY (`ReceitaId`) REFERENCES `Lancamentos` (`Id`) ON DELETE SET NULL,
                    CONSTRAINT `FK_Reembolsos_Pessoas_PessoaId` FOREIGN KEY (`PessoaId`) REFERENCES `Pessoas` (`Id`) ON DELETE RESTRICT
                ) CHARACTER SET=utf8mb4
                """);
            foreach (var idx in new[]
            {
                "CREATE UNIQUE INDEX `IX_Reembolsos_LancamentoId` ON `Reembolsos` (`LancamentoId`)",
                "CREATE INDEX `IX_Reembolsos_CartaoCreditoId_Vencimento` ON `Reembolsos` (`CartaoCreditoId`, `Vencimento`)",
                "CREATE INDEX `IX_Reembolsos_PessoaId_Vencimento` ON `Reembolsos` (`PessoaId`, `Vencimento`)",
                "CREATE INDEX `IX_Reembolsos_ReceitaId` ON `Reembolsos` (`ReceitaId`)"
            })
            {
                try { ExecuteSql(conn, idx); }
                catch (Exception ex) { _logger.LogWarning(ex, "Índice de Reembolsos já existente"); }
            }
        }
    }

    /// <summary>Adiciona colunas do modelo atual ausentes em backups antigos.
    /// No-op quando todas existem.</summary>
    private void GarantirColunasModelo(DbConnection conn, bool sqlite)
    {
        var marcadores = new (string Tabela, string Coluna, string DdlSqlite, string DdlMySql)[]
        {
            ("Contas", "Limite", "ALTER TABLE Contas ADD COLUMN Limite TEXT", "ALTER TABLE `Contas` ADD COLUMN `Limite` decimal(65,30) NULL"),
            ("Lancamentos", "DataCompra", "ALTER TABLE Lancamentos ADD COLUMN DataCompra TEXT", "ALTER TABLE `Lancamentos` ADD COLUMN `DataCompra` date NULL"),
            ("Configuracoes", "DiasAntecipacao", "ALTER TABLE Configuracoes ADD COLUMN DiasAntecipacao INTEGER NOT NULL DEFAULT 0", "ALTER TABLE `Configuracoes` ADD COLUMN `DiasAntecipacao` int NOT NULL DEFAULT 0"),
            ("InvestimentosMovimentos", "Taxa", "ALTER TABLE InvestimentosMovimentos ADD COLUMN Taxa TEXT", "ALTER TABLE `InvestimentosMovimentos` ADD COLUMN `Taxa` decimal(65,30) NULL")
        };
        foreach (var (tabela, coluna, ddlSqlite, ddlMySql) in marcadores)
        {
            if (!TabelaExiste(conn, sqlite, tabela) || ColunaExiste(conn, sqlite, tabela, coluna))
                continue;
            _logger.LogInformation("Coluna {Tabela}.{Coluna} ausente; adicionando", tabela, coluna);
            ExecuteSql(conn, sqlite ? ddlSqlite : ddlMySql);
        }
    }

    /// <summary>Remove as colunas legadas do reembolso em bancos atualizados.
    /// No-op quando já ausentes.</summary>
    private void RemoverColunasLegadas(DbConnection conn, bool sqlite)
    {
        foreach (var col in ColunasLegadasReembolso)
        {
            if (!ColunaExiste(conn, sqlite, "Lancamentos", col)) continue;
            _logger.LogInformation("Removendo coluna legada Lancamentos.{Col}", col);
            if (!sqlite && (col == "ReembolsoCategoriaId" || col == "ReembolsoSubcategoriaId"))
            {
                var fk = col == "ReembolsoCategoriaId"
                    ? "FK_Lancamentos_Categorias_ReembolsoCategoriaId"
                    : "FK_Lancamentos_Subcategorias_ReembolsoSubcategoriaId";
                try { ExecuteSql(conn, $"ALTER TABLE `Lancamentos` DROP FOREIGN KEY `{fk}`"); }
                catch (Exception ex) { _logger.LogWarning(ex, "FK legada {Fk} já ausente", fk); }
            }
            ExecuteSql(conn, sqlite
                ? $"ALTER TABLE Lancamentos DROP COLUMN {col}"
                : $"ALTER TABLE `Lancamentos` DROP COLUMN `{col}`");
        }
    }

    /// <summary>Registra a Initial como aplicada quando o schema já está no
    /// modelo atual (bancos atualizados e backups restaurados).</summary>
    private void CarimbarInitialSeAusente(DbConnection conn, bool sqlite)
    {
        CriarTabelaHistoricoSeAusente(conn, sqlite);
        using var cmd = conn.CreateCommand();
        if (sqlite)
        {
            cmd.CommandText = "INSERT OR IGNORE INTO __EFMigrationsHistory (MigrationId, ProductVersion) VALUES (@id, '9.0.0')";
        }
        else
        {
            cmd.CommandText = "INSERT IGNORE INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`) VALUES (@id, '9.0.0')";
        }
        AddParam(cmd, "@id", InitialId());
        cmd.ExecuteNonQuery();
    }

    private static void CriarTabelaHistoricoSeAusente(DbConnection conn, bool sqlite)
    {
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sqlite
            ? "CREATE TABLE IF NOT EXISTS __EFMigrationsHistory (MigrationId TEXT NOT NULL CONSTRAINT PK___EFMigrationsHistory PRIMARY KEY, ProductVersion TEXT NOT NULL)"
            : "CREATE TABLE IF NOT EXISTS `__EFMigrationsHistory` (`MigrationId` varchar(95) NOT NULL, `ProductVersion` varchar(32) NOT NULL, PRIMARY KEY (`MigrationId`))";
        cmd.ExecuteNonQuery();
    }

    private static bool SchemaAtual(DbConnection conn, bool sqlite)
        => TabelaExiste(conn, sqlite, "Lancamentos")
        && TabelaExiste(conn, sqlite, "Reembolsos")
        && !ColunaExiste(conn, sqlite, "Lancamentos", "ReembolsoId")
        && !ColunaExiste(conn, sqlite, "Lancamentos", "ReembolsoCategoriaId")
        && !ColunaExiste(conn, sqlite, "Lancamentos", "ReembolsoSubcategoriaId");

    /// <summary>
    /// Converte receitas legadas ligadas via Lancamentos.ReembolsoId em Reembolsos.
    /// Fatura fechada: preserva a receita e grava ReceitaId/Fechado; senão exclui a
    /// receita pendente. No-op quando a coluna legada não existe mais.
    /// </summary>
    private void ConverterReembolsosLegados()
    {
        var conn = _db.Database.GetDbConnection();
        var openedHere = false;
        if (conn.State != System.Data.ConnectionState.Open)
        {
            conn.Open();
            openedHere = true;
        }
        try
        {
            var sqlite = conn is SqliteConnection;
            if (!ColunaExiste(conn, sqlite, "Lancamentos", "ReembolsoId"))
            {
                _logger.LogInformation("Sem coluna legada ReembolsoId; conversão ignorada");
                return;
            }
            if (!TabelaExiste(conn, sqlite, "Reembolsos"))
            {
                _logger.LogWarning("Tabela Reembolsos ausente; conversão legada ignorada");
                return;
            }

            var pares = LerParesLegados(conn);
            if (pares.Count == 0)
            {
                _logger.LogInformation("Nenhum reembolso legado para converter");
                return;
            }

            using var tx = conn.BeginTransaction();
            var convertidos = 0;
            var excluidas = 0;
            foreach (var p in pares)
            {
                if (p.CartaoId is null)
                {
                    _logger.LogWarning("Reembolso legado órfão ignorado: DespesaId {DespesaId} sem cartão", p.DespesaId);
                    continue;
                }
                var pessoaId = p.ReceitaPessoaId ?? p.DespesaPessoaId;
                if (personIdIsNull(pessoaId))
                {
                    _logger.LogWarning("Reembolso legado órfão ignorado: DespesaId {DespesaId} ReceitaId {ReceitaId} sem pessoa", p.DespesaId, p.ReceitaId);
                    continue;
                }
                var valor = Math.Abs(p.ReceitaValor);
                if (valor <= 0)
                {
                    _logger.LogWarning("Reembolso legado órfão ignorado: DespesaId {DespesaId} ReceitaId {ReceitaId} valor {Valor}", p.DespesaId, p.ReceitaId, p.ReceitaValor);
                    continue;
                }
                var vencCartao = p.DataVencimentoCartao ?? p.ReceitaData;
                var vencimento = vencCartao.AddDays(-1);

                var (fechada, dataFechamento) = FaturaFechada(conn, tx, p.CartaoId.Value, vencCartao);
                var id = Guid.NewGuid();
                InserirReembolso(conn, tx, sqlite, id, pessoaId!.Value, p.CartaoId.Value,
                    p.DespesaId, p.ParcelaAtual, p.TotalParcelas, valor, vencimento,
                    fechada, dataFechamento, fechada ? p.ReceitaId : null);
                if (fechada)
                {
                    convertidos++;
                }
                else
                {
                    ExcluirLancamento(conn, tx, p.ReceitaId);
                    convertidos++;
                    excluidas++;
                }
            }
            tx.Commit();
            _logger.LogInformation("Reembolsos legados convertidos: {Total} (receitas pendentes excluídas: {Excluidas})",
                convertidos, excluidas);
        }
        finally
        {
            if (openedHere) conn.Close();
        }
    }

    private static bool personIdIsNull(Guid? id) => !id.HasValue || id.Value == Guid.Empty;

    /// <summary>GUID no formato do EF no SQLite (TEXT maiúsculo); no MySQL a
    /// comparação é case-insensitive, então o formato serve aos dois.</summary>
    private static string G(Guid g) => g.ToString("D").ToUpperInvariant();

    private sealed record ParLegado(
        Guid DespesaId, Guid? CartaoId, Guid? DespesaPessoaId,
        int? ParcelaAtual, int? TotalParcelas, DateOnly? DataVencimentoCartao,
        Guid ReceitaId, Guid? ReceitaPessoaId, decimal ReceitaValor, DateOnly ReceitaData);

    private static List<ParLegado> LerParesLegados(DbConnection conn)
    {
        var lista = new List<ParLegado>();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT D.Id, D.CartaoCreditoId, D.PessoaId, D.ParcelaAtual, D.TotalParcelas,
                   D.DataVencimentoCartao, R.Id, R.PessoaId, R.Valor, R.Data
            FROM Lancamentos D JOIN Lancamentos R ON R.Id = D.ReembolsoId
            WHERE D.ReembolsoId IS NOT NULL
            """;
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            lista.Add(new ParLegado(
                Guid.Parse(Convert.ToString(reader[0])!),
                DbNullToGuid(reader[1]),
                DbNullToGuid(reader[2]),
                DbNullToInt(reader[3]),
                DbNullToInt(reader[4]),
                DbNullToDate(reader[5]),
                Guid.Parse(Convert.ToString(reader[6])!),
                DbNullToGuid(reader[7]),
                decimal.Parse(Convert.ToString(reader[8])!, CultureInfo.InvariantCulture),
                DateOnly.Parse(Convert.ToString(reader[9])!, CultureInfo.InvariantCulture)));
        }
        return lista;
    }

    private static Guid? DbNullToGuid(object v)
        => v is null || v == DBNull.Value ? null : Guid.Parse(Convert.ToString(v)!);
    private static int? DbNullToInt(object v)
        => v is null || v == DBNull.Value ? null : int.Parse(Convert.ToString(v)!, CultureInfo.InvariantCulture);
    private static DateOnly? DbNullToDate(object v)
        => v is null || v == DBNull.Value ? null : DateOnly.Parse(Convert.ToString(v)!, CultureInfo.InvariantCulture);

    private static (bool Fechada, DateTime? DataFechamento) FaturaFechada(
        DbConnection conn, DbTransaction tx, Guid cartaoId, DateOnly vencCartao)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "SELECT Fechada, DataFechamento FROM Faturas WHERE CartaoCreditoId = @c AND AnoReferencia = @a AND MesReferencia = @m";
        AddParam(cmd, "@c", G(cartaoId));
        AddParam(cmd, "@a", vencCartao.Year);
        AddParam(cmd, "@m", vencCartao.Month);
        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return (false, null);
        var fechada = int.Parse(Convert.ToString(reader[0])!, CultureInfo.InvariantCulture) == 1;
        DateTime? data = reader[1] is null || reader[1] == DBNull.Value
            ? null : DateTime.Parse(Convert.ToString(reader[1])!, CultureInfo.InvariantCulture);
        return (fechada, data ?? (fechada ? DateTime.Now : null));
    }

    private static void InserirReembolso(DbConnection conn, DbTransaction tx, bool sqlite,
        Guid id, Guid pessoaId, Guid cartaoId, Guid lancamentoId,
        int? parcelaAtual, int? totalParcelas, decimal valor, DateOnly vencimento,
        bool fechado, DateTime? dataFechamento, Guid? receitaId)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = """
            INSERT INTO Reembolsos (Id, PessoaId, CartaoCreditoId, LancamentoId, ParcelaAtual,
                TotalParcelas, Valor, Vencimento, Fechado, DataFechamento, ReceitaId)
            VALUES (@id, @p, @c, @l, @pa, @tp, @v, @venc, @f, @df, @r)
            """;
        AddParam(cmd, "@id", G(id));
        AddParam(cmd, "@p", G(pessoaId));
        AddParam(cmd, "@c", G(cartaoId));
        AddParam(cmd, "@l", G(lancamentoId));
        AddParam(cmd, "@pa", parcelaAtual);
        AddParam(cmd, "@tp", totalParcelas);
        AddParam(cmd, "@v", valor);
        AddParam(cmd, "@venc", vencimento.ToString("yyyy-MM-dd"));
        AddParam(cmd, "@f", sqlite ? (fechado ? 1 : 0) : (object)fechado);
        AddParam(cmd, "@df", dataFechamento.HasValue
            ? (sqlite ? dataFechamento.Value.ToString("yyyy-MM-dd HH:mm:ss") : (object)dataFechamento.Value)
            : DBNull.Value);
        AddParam(cmd, "@r", receitaId.HasValue ? G(receitaId.Value) : (object)DBNull.Value);
        cmd.ExecuteNonQuery();
    }

    private static void ExcluirLancamento(DbConnection conn, DbTransaction tx, Guid id)
    {
        using var cmd = conn.CreateCommand();
        cmd.Transaction = tx;
        cmd.CommandText = "DELETE FROM Lancamentos WHERE Id = @id";
        AddParam(cmd, "@id", G(id));
        cmd.ExecuteNonQuery();
    }

    private static void AddParam(DbCommand cmd, string name, object? value)
    {
        var p = cmd.CreateParameter();
        p.ParameterName = name;
        p.Value = value ?? DBNull.Value;
        cmd.Parameters.Add(p);
    }

    private static bool TabelaExisteAberta(DbConnection conn, bool sqlite, string tabela)
    {
        var openedHere = false;
        if (conn.State != System.Data.ConnectionState.Open)
        {
            conn.Open();
            openedHere = true;
        }
        try
        {
            return TabelaExiste(conn, sqlite, tabela);
        }
        finally
        {
            if (openedHere) conn.Close();
        }
    }

    private static bool TabelaExiste(DbConnection conn, bool sqlite, string tabela)
    {
        using var cmd = conn.CreateCommand();
        if (sqlite)
        {
            cmd.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @t";
            AddParam(cmd, "@t", tabela);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t";
        AddParam(cmd, "@t", tabela);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private static bool ColunaExiste(DbConnection conn, bool sqlite, string tabela, string coluna)
    {
        using var cmd = conn.CreateCommand();
        if (sqlite)
        {
            cmd.CommandText = $"SELECT COUNT(*) FROM pragma_table_info('{tabela}') WHERE name = @c";
            AddParam(cmd, "@c", coluna);
            return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
        }
        cmd.CommandText = "SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = DATABASE() AND TABLE_NAME = @t AND COLUMN_NAME = @c";
        AddParam(cmd, "@t", tabela);
        AddParam(cmd, "@c", coluna);
        return Convert.ToInt32(cmd.ExecuteScalar()) > 0;
    }

    private void TryRecoverMigrationHistory(Exception originalException)
    {
        try
        {
            _logger.LogInformation("Attempting to sync migration history...");
            var conn = _db.Database.GetDbConnection();
            var sqlite = conn is SqliteConnection;
            var openedHere = false;
            if (conn.State != System.Data.ConnectionState.Open)
            {
                conn.Open();
                openedHere = true;
            }
            try
            {
                // Backups com schema já atualizado mas histórico divergente:
                // carimba a Initial e conclui.
                if (SchemaAtual(conn, sqlite))
                {
                    _logger.LogInformation("Schema já atualizado; carimbando Initial como aplicada");
                    CarimbarInitialSeAusente(conn, sqlite);
                }
            }
            finally
            {
                if (openedHere) conn.Close();
            }

            _db.Database.GetService<IMigrator>().Migrate();
            if (!_db.Database.GetPendingMigrations().Any())
            {
                _logger.LogInformation("All migrations resolved after recovery");
                return;
            }
            throw originalException;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration recovery failed");
            throw;
        }
    }

    private void ExecuteSql(DbConnection conn, string sql)
    {
        _logger.LogInformation("Executing: {Sql}", sql);
        using var cmd = conn.CreateCommand();
        cmd.CommandText = sql;
        cmd.ExecuteNonQuery();
    }
}
