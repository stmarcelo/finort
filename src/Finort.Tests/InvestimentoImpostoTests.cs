using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class InvestimentoImpostoTests
{
    private static (AppDbContext Db, string File, Conta Conta, Categoria Impostos, Subcategoria Sub) Setup()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        db.SaveChanges();
        var impostos = db.Categorias.First(c => c.Nome == "Impostos");
        var sub = db.Subcategorias.First(s => s.Nome == "Imposto de resgate");
        return (db, file, conta, impostos, sub);
    }

    /// <summary>A guarda de saldo exige lastro; um rendimento não gera lançamento nem
    /// movimento e preserva as contagens verificadas pelos testes.</summary>
    private static Task GarantirSaldoAsync(InvestimentoService servico, Guid investimentoId)
        => servico.RegistrarProventoAsync(
            investimentoId, new DateOnly(2026, 10, 1), 1000m, ProventoTipo.Rendimento);

    [Fact]
    public async Task Resgate_ComImposto_LancaReceitaComSomaEDespesaDeImposto()
    {
        var (db, file, conta, impostos, sub) = Setup();
        try
        {
            var lancamentos = new LancamentoService(db);
            var servico = new InvestimentoService(db, lancamentos);
            var investimento = new Investimento
            {
                Nome = "CDB", Tipo = TipoInvestimento.Cdb, ContaVinculadaId = conta.Id, Ativo = true
            };
            db.Investimentos.Add(investimento);
            db.SaveChanges();
            await GarantirSaldoAsync(servico, investimento.Id);

            var movimento = await servico.RegistrarMovimentoAsync(
                investimento.Id, new DateOnly(2026, 10, 20), MovimentoTipo.Resgate,
                null, null, 1000m, lancarNaConta: true, imposto: 150m);

            Assert.Equal(1000m, movimento.Valor);
            Assert.Equal(150m, movimento.Imposto);
            Assert.NotNull(movimento.LancamentoImpostoId);

            var receita = await db.Lancamentos.SingleAsync(l => l.Id == movimento.LancamentoId);
            Assert.Equal(LancamentoTipo.Receita, receita.Tipo);
            // Receita = valor do resgate + imposto.
            Assert.Equal(1150m, receita.Valor);

            var despesa = await db.Lancamentos.SingleAsync(l => l.Id == movimento.LancamentoImpostoId);
            Assert.Equal(LancamentoTipo.Despesa, despesa.Tipo);
            Assert.Equal(150m, Math.Abs(despesa.Valor));
            Assert.Equal(impostos.Id, despesa.CategoriaId);
            Assert.Equal(sub.Id, despesa.SubcategoriaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Resgate_ComImposto_SemDestinoDeImposto_NaoCriaReceitaOrfa()
    {
        var (db, file, conta, _, _) = Setup();
        try
        {
            var lancamentos = new LancamentoService(db);
            var servico = new InvestimentoService(db, lancamentos);
            var investimento = new Investimento
            {
                Nome = "CDB", Tipo = TipoInvestimento.Cdb, ContaVinculadaId = conta.Id, Ativo = true
            };
            db.Investimentos.Add(investimento);
            db.SaveChanges();
            await GarantirSaldoAsync(servico, investimento.Id);

            // Simula a subcategoria protegida renomeada: o destino do imposto deixa de existir.
            var sub = db.Subcategorias.First(s => s.Nome == "Imposto de resgate");
            sub.Nome = "Renomeada";
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(() => servico.RegistrarMovimentoAsync(
                investimento.Id, new DateOnly(2026, 10, 20), MovimentoTipo.Resgate,
                null, null, 1000m, lancarNaConta: true, imposto: 150m));

            // Nenhuma perna pode ter sido gravada: nem a receita inflada, nem o movimento.
            Assert.Equal(0, await db.Lancamentos.CountAsync());
            Assert.Equal(0, await db.InvestimentosMovimentos.CountAsync());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Resgate_SemImposto_NaoLancaDespesaDeImposto()
    {
        var (db, file, conta, _, _) = Setup();
        try
        {
            var lancamentos = new LancamentoService(db);
            var servico = new InvestimentoService(db, lancamentos);
            var investimento = new Investimento
            {
                Nome = "Reserva", Tipo = TipoInvestimento.Reserva, ContaVinculadaId = conta.Id, Ativo = true
            };
            db.Investimentos.Add(investimento);
            db.SaveChanges();
            await GarantirSaldoAsync(servico, investimento.Id);

            var movimento = await servico.RegistrarMovimentoAsync(
                investimento.Id, new DateOnly(2026, 10, 20), MovimentoTipo.Resgate,
                null, null, 1000m, lancarNaConta: true);

            Assert.Null(movimento.Imposto);
            Assert.Null(movimento.LancamentoImpostoId);
            Assert.Equal(1, await db.Lancamentos.CountAsync());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Resgate_ComImposto_EstornoRemoveAsDuasPernas()
    {
        var (db, file, conta, _, _) = Setup();
        try
        {
            var lancamentos = new LancamentoService(db);
            var servico = new InvestimentoService(db, lancamentos);
            var investimento = new Investimento
            {
                Nome = "CDB", Tipo = TipoInvestimento.Cdb, ContaVinculadaId = conta.Id, Ativo = true
            };
            db.Investimentos.Add(investimento);
            db.SaveChanges();
            await GarantirSaldoAsync(servico, investimento.Id);

            var movimento = await servico.RegistrarMovimentoAsync(
                investimento.Id, new DateOnly(2026, 10, 20), MovimentoTipo.Resgate,
                null, null, 1000m, lancarNaConta: true, imposto: 150m);

            await servico.EstornarMovimentoAsync(movimento.Id);

            Assert.Equal(0, await db.Lancamentos.CountAsync());
            Assert.Equal(0, await db.InvestimentosMovimentos.CountAsync());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SubcategoriaDeImpostoDeResgate_EstaProtegida()
    {
        var (db, file, _, _, _) = Setup();
        try
        {
            var sub = db.Subcategorias.First(s => s.Nome == "Imposto de resgate");
            Assert.True(sub.IsProtected);

            var servico = new CategoriaService(db);
            await Assert.ThrowsAsync<InvalidOperationException>(() => servico.ExcluirSubcategoriaAsync(sub.Id));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ExcluirInvestimento_ApagaTambemADespesaDoImposto()
    {
        var (db, file, conta, _, _) = Setup();
        try
        {
            var lancamentos = new LancamentoService(db);
            var servico = new InvestimentoService(db, lancamentos);
            var investimento = new Investimento
            {
                Nome = "CDB", Tipo = TipoInvestimento.Cdb, ContaVinculadaId = conta.Id, Ativo = true
            };
            db.Investimentos.Add(investimento);
            db.SaveChanges();
            await GarantirSaldoAsync(servico, investimento.Id);

            await servico.RegistrarMovimentoAsync(
                investimento.Id, new DateOnly(2026, 10, 20), MovimentoTipo.Resgate,
                null, null, 1000m, lancarNaConta: true, imposto: 150m);

            await servico.ExcluirAsync(investimento.Id);

            Assert.Equal(0, await db.Lancamentos.CountAsync());
            Assert.Equal(0, await db.InvestimentosMovimentos.CountAsync());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
