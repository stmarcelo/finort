using Finort.Data;
using Microsoft.Data.Sqlite;

namespace Finort.Tests;

public class Fase4aMigrationTests
{
    [Fact]
    public void Migrate_CriaTabelasFase4a()
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
                    "AND name IN ('CartoesCredito','Faturas','Provisoes','MesesFechados')";
                using var reader = cmd.ExecuteReader();
                while (reader.Read()) tabelas.Add(reader.GetString(0));
            }

            foreach (var esperada in new[] { "CartoesCredito", "Faturas", "Provisoes", "MesesFechados" })
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
    public void Migrate_LancamentoAceitaContaENuloECartao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Models.Financeiro.Conta { Nome = "Conta" };
            db.Contas.Add(conta);
            var cartao = new Models.Financeiro.CartaoCredito
            {
                Banco = "Banco",
                Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5,
                DiaVencimento = 10,
                Limite = 1000m,
                Ativo = true,
                ContaId = conta.Id
            };
            db.CartoesCredito.Add(cartao);
            var renda = db.Categorias.First(c => c.Nome == "Receita");
            db.Lancamentos.Add(new Models.Financeiro.Lancamento
            {
                Data = new DateOnly(2026, 8, 10),
                Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -50m,
                ContaId = null,
                CartaoCreditoId = cartao.Id,
                CategoriaId = renda.Id,
                ParcelamentoId = Guid.NewGuid(),
                ParcelaAtual = 1,
                TotalParcelas = 3
            });
            db.SaveChanges();

            var lancamento = db.Lancamentos.Single();
            Assert.Null(lancamento.ContaId);
            Assert.Equal(cartao.Id, lancamento.CartaoCreditoId);
            Assert.Equal(1, lancamento.ParcelaAtual);
        }
        finally
        {
            TestDbContext.Cleanup(db, file);
        }
    }
}
