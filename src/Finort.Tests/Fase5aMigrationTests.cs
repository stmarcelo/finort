using Finort.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class Fase5aMigrationTests
{
    [Fact]
    public void Migrate_CriaTabelasInvestimento()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var tabelas = new List<string>();
            using (var conn = new SqliteConnection($"Data Source={file}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT name FROM sqlite_master WHERE type='table' " +
                    "AND name IN ('Investimentos','InvestimentosProventos','AuditoriasExclusaoInvestimento')";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) tabelas.Add(reader.GetString(0));
            }

            foreach (var esperada in new[]
                     {
                         "Investimentos", "InvestimentosProventos", "AuditoriasExclusaoInvestimento"
                     })
            {
                Assert.Contains(esperada, tabelas);
            }
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public void Migrate_ProventoExigeInvestimentoERoundTrip()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Models.Financeiro.Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            var investimento = new Models.Financeiro.Investimento
            {
                Nome = "ITSA4", Tipo = Models.Financeiro.TipoInvestimento.Acao,
                ContaVinculadaId = conta.Id
            };
            db.Investimentos.Add(investimento);
            db.InvestimentosProventos.Add(new Models.Financeiro.InvestimentoProvento
            {
                InvestimentoId = investimento.Id,
                Data = new DateOnly(2026, 8, 10),
                Valor = 25.5m,
                Tipo = Models.Financeiro.ProventoTipo.Dividendo
            });
            db.AuditoriasExclusaoInvestimento.Add(new Models.Financeiro.AuditoriaExclusaoInvestimento
            {
                NomeInvestimento = "Antigo", Tipo = Models.Financeiro.TipoInvestimento.Reserva,
                ValorCotaAtual = 100m, DataExclusao = DateTime.Now
            });
            db.SaveChanges();

            Assert.True(db.InvestimentosProventos.Any(p => p.Valor == 25.5m));
            Assert.True(db.AuditoriasExclusaoInvestimento.Any(a => a.NomeInvestimento == "Antigo"));
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }

    [Fact]
    public void Migrate_CriaTabelaInvestimentoMovimento()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            using (var conn = new SqliteConnection($"Data Source={file}"))
            {
                conn.Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT COUNT(*) FROM sqlite_master WHERE type='table' AND name='InvestimentosMovimentos'";
                Assert.Equal(1L, (long)(cmd.ExecuteScalar() ?? 0L));
            }

            var conta = new Models.Financeiro.Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            var investimento = new Models.Financeiro.Investimento
            {
                Nome = "BTC", Tipo = Models.Financeiro.TipoInvestimento.Criptomoeda,
                ContaVinculadaId = conta.Id
            };
            db.Investimentos.Add(investimento);
            db.SaveChanges();
            var lancamento = new Models.Financeiro.Lancamento
            {
                Data = new DateOnly(2026, 8, 5), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -50m, ContaId = conta.Id,
                CategoriaId = db.Categorias.First(c => c.Nome == "Investimento").Id
            };
            db.Lancamentos.Add(lancamento);
            db.InvestimentosMovimentos.Add(new Models.Financeiro.InvestimentoMovimento
            {
                InvestimentoId = investimento.Id,
                Data = new DateOnly(2026, 8, 5),
                Tipo = Models.Financeiro.MovimentoTipo.Compra,
                Quantidade = 0.00012345m,
                ValorPorCota = 350000m,
                Valor = 50m,
                LancamentoId = lancamento.Id
            });
            db.SaveChanges();

            var salvo = db.InvestimentosMovimentos.AsNoTracking().Single();
            Assert.Equal(0.00012345m, salvo.Quantidade);
            Assert.Equal(lancamento.Id, salvo.LancamentoId);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}
