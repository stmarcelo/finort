using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class ReembolsoLancamentoTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Service, CartaoCredito Cartao, Categoria Cat)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartao = new CartaoCredito
        {
            Banco = "Nubank",
            Ultimos4Digitos = "1234",
            MelhorDiaCompra = 5,
            DiaVencimento = 10,
            Limite = 5000m,
            Ativo = true,
            ContaId = conta.Id
        };
        db.CartoesCredito.Add(cartao);
        await db.SaveChangesAsync();
        var cat = db.Categorias.First(c => c.Nome == "Receita");
        return (db, file, new LancamentoService(db), cartao, cat);
    }

    [Fact]
    public async Task CriarDespesaCartao_ComReembolso_GeraReembolso_SemReceita()
    {
        var (db, file, service, cartao, cat) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var criados = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 9, 10), 100m, cat.Id, null, null, null, pessoa.Id, null);

            var despesa = Assert.Single(criados);
            Assert.Empty(db.Lancamentos.Where(l => l.Tipo == LancamentoTipo.Receita));
            var r = Assert.Single(db.Reembolsos);
            Assert.Equal(pessoa.Id, r.PessoaId);
            Assert.Equal(cartao.Id, r.CartaoCreditoId);
            Assert.Equal(despesa.Id, r.LancamentoId);
            Assert.Equal(100m, r.Valor);
            Assert.Equal(despesa.DataVencimentoCartao!.Value.AddDays(-1), r.Vencimento);
            Assert.Equal(new DateOnly(2026, 10, 9), r.Vencimento);
            Assert.False(r.Fechado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarEntrada_ComReembolso_GeraValorNegativo()
    {
        var (db, file, service, cartao, cat) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amiga" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var criados = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 9, 10), 50m, cat.Id, null, null, null, pessoa.Id, null,
                ehEntrada: true);

            var entrada = Assert.Single(criados);
            Assert.True(entrada.Valor > 0);
            var r = Assert.Single(db.Reembolsos);
            Assert.Equal(-50m, r.Valor);
            Assert.Empty(db.Lancamentos.Where(l => l.Tipo == LancamentoTipo.Receita));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ExcluirDespesaCartao_ComReembolso_RemoveReembolso()
    {
        var (db, file, service, cartao, cat) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var criados = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 9, 10), 60m, cat.Id, null, null, null, pessoa.Id, null);

            Assert.Single(db.Reembolsos);
            await service.ExcluirAsync(criados[0].Id);

            Assert.Empty(db.Lancamentos.ToList());
            Assert.Empty(db.Reembolsos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarValor_SincronizaReembolso_PreservandoSinalEVencimento()
    {
        var (db, file, service, cartao, cat) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var criados = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 9, 10), 60m, cat.Id, null, null, null, pessoa.Id, null,
                vencimentoExato: new DateOnly(2026, 10, 10));
            var despesa = criados.Single();

            await service.AtualizarValorAsync(despesa.Id, 75m);

            var atualizada = await service.ObterAsync(despesa.Id);
            Assert.Equal(-75m, atualizada!.Valor);
            var r = Assert.Single(db.Reembolsos);
            Assert.Equal(75m, r.Valor);
            Assert.Equal(new DateOnly(2026, 10, 9), r.Vencimento);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
