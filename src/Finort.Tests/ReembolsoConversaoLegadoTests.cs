using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace Finort.Tests;

public class ReembolsoConversaoLegadoTests
{
    [Fact]
    public async Task Migrator_ConverteReceitaLegada_EmReembolsoExcluindoPendente()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            var cartao = new CartaoCredito
            {
                Banco = "Nubank", Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
            };
            db.CartoesCredito.Add(cartao);
            await db.SaveChangesAsync();
            var renda = db.Categorias.First(c => c.Nome == "Receita");
            var svc = new LancamentoService(db);
            var despesa = (await svc.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 100m, renda.Id, null, null,
                parcelas: null, reembolsoPessoaId: null)).Single();
            var receita = await svc.CriarReceitaAsync(
                conta.Id, new DateOnly(2026, 8, 20), 100m, renda.Id, null, pessoa.Id);

            // simula banco legado: coluna + vínculo despesa → receita
            // (EF grava GUID maiúsculo no SQLite; comparação TEXT é case-sensitive)
            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Lancamentos ADD COLUMN ReembolsoId TEXT");
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Lancamentos SET ReembolsoId = {0} WHERE Id = {1}",
                receita.Id.ToString("D").ToUpperInvariant(), despesa.Id.ToString("D").ToUpperInvariant());

            new DatabaseMigrator(db, NullLogger<DatabaseMigrator>.Instance).Migrate();

            var reembolso = Assert.Single(db.Reembolsos.ToList());
            Assert.Equal(pessoa.Id, reembolso.PessoaId);
            Assert.Equal(cartao.Id, reembolso.CartaoCreditoId);
            Assert.Equal(despesa.Id, reembolso.LancamentoId);
            Assert.Equal(100m, reembolso.Valor);
            Assert.Equal(despesa.DataVencimentoCartao!.Value.AddDays(-1), reembolso.Vencimento);
            Assert.False(reembolso.Fechado);
            Assert.Null(reembolso.ReceitaId);
            // fatura aberta: receita pendente excluída, despesa preservada
            Assert.Null(await db.Lancamentos.AsNoTracking().FirstOrDefaultAsync(l => l.Id == receita.Id));
            Assert.NotNull(await db.Lancamentos.AsNoTracking().FirstOrDefaultAsync(l => l.Id == despesa.Id));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Migrator_PreservaReceitaQuandoFaturaFechada()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            var cartao = new CartaoCredito
            {
                Banco = "Nubank", Ultimos4Digitos = "1234",
                MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
            };
            db.CartoesCredito.Add(cartao);
            await db.SaveChangesAsync();
            var renda = db.Categorias.First(c => c.Nome == "Receita");
            var svc = new LancamentoService(db);
            var despesa = (await svc.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 100m, renda.Id, null, null,
                parcelas: null, reembolsoPessoaId: null)).Single();
            var receita = await svc.CriarReceitaAsync(
                conta.Id, new DateOnly(2026, 8, 20), 100m, renda.Id, null, pessoa.Id);

            var venc = despesa.DataVencimentoCartao!.Value;
            db.Faturas.Add(new Fatura
            {
                CartaoCreditoId = cartao.Id,
                AnoReferencia = venc.Year,
                MesReferencia = venc.Month,
                Fechada = true,
                ValorTotal = 100m,
                DataFechamento = DateTime.Now
            });
            await db.SaveChangesAsync();

            await db.Database.ExecuteSqlRawAsync("ALTER TABLE Lancamentos ADD COLUMN ReembolsoId TEXT");
            await db.Database.ExecuteSqlRawAsync(
                "UPDATE Lancamentos SET ReembolsoId = {0} WHERE Id = {1}",
                receita.Id.ToString("D").ToUpperInvariant(), despesa.Id.ToString("D").ToUpperInvariant());

            new DatabaseMigrator(db, NullLogger<DatabaseMigrator>.Instance).Migrate();

            var reembolso = Assert.Single(db.Reembolsos.ToList());
            Assert.True(reembolso.Fechado);
            Assert.Equal(receita.Id, reembolso.ReceitaId);
            Assert.Equal(pessoa.Id, reembolso.PessoaId);
            Assert.Equal(100m, reembolso.Valor);
            // fatura fechada: receita preservada, despesa preservada
            Assert.NotNull(await db.Lancamentos.AsNoTracking().FirstOrDefaultAsync(l => l.Id == receita.Id));
            Assert.NotNull(await db.Lancamentos.AsNoTracking().FirstOrDefaultAsync(l => l.Id == despesa.Id));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
