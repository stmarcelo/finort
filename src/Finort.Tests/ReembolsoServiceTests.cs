using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class ReembolsoServiceTests
{
    private static async Task<(AppDbContext Db, string File, ReembolsoService Service, CartaoCredito Cartao, Pessoa Pessoa, Categoria Cat)> SetupAsync()
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
        var pessoa = new Pessoa { Nome = "Amigo" };
        db.Pessoas.Add(pessoa);
        await db.SaveChangesAsync();
        var cat = db.Categorias.First(c => c.Nome == "Receita");
        return (db, file, new ReembolsoService(db), cartao, pessoa, cat);
    }

    private static async Task<Lancamento> NovaDespesaCartaoAsync(AppDbContext db, CartaoCredito cartao, Categoria cat, DateOnly vencimentoFatura)
    {
        var despesa = new Lancamento
        {
            Data = vencimentoFatura.AddDays(-30),
            DataCompra = vencimentoFatura.AddDays(-30),
            DataVencimentoCartao = vencimentoFatura,
            Tipo = LancamentoTipo.Despesa,
            Valor = -100m,
            CartaoCreditoId = cartao.Id,
            CategoriaId = cat.Id,
            Confirmado = true
        };
        db.Lancamentos.Add(despesa);
        await db.SaveChangesAsync();
        return despesa;
    }

    [Fact]
    public async Task ObterDaFaturaAsync_RetornaSomenteDoCartaoEPeriodo()
    {
        var (db, file, service, cartao, pessoa, cat) = await SetupAsync();
        try
        {
            var dentro = await NovaDespesaCartaoAsync(db, cartao, cat, new DateOnly(2026, 9, 10));
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = pessoa.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = dentro.Id,
                Valor = 100m,
                Vencimento = new DateOnly(2026, 9, 9)
            });
            var fora = await NovaDespesaCartaoAsync(db, cartao, cat, new DateOnly(2026, 10, 10));
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = pessoa.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = fora.Id,
                Valor = 50m,
                Vencimento = new DateOnly(2026, 10, 9)
            });
            await db.SaveChangesAsync();

            var lista = await service.ObterDaFaturaAsync(cartao.Id, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

            var unico = Assert.Single(lista);
            Assert.Equal(dentro.Id, unico.LancamentoId);
            Assert.NotNull(unico.Pessoa);
            Assert.NotNull(unico.Lancamento);
            Assert.Empty(await service.ObterDaFaturaAsync(Guid.NewGuid(), new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30)));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterPendentesAsync_RetornaSomenteNaoFechadosNoPeriodo()
    {
        var (db, file, service, cartao, pessoa, cat) = await SetupAsync();
        try
        {
            var d1 = await NovaDespesaCartaoAsync(db, cartao, cat, new DateOnly(2026, 9, 10));
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = pessoa.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = d1.Id,
                Valor = 100m,
                Vencimento = new DateOnly(2026, 9, 9)
            });
            var d2 = await NovaDespesaCartaoAsync(db, cartao, cat, new DateOnly(2026, 9, 10));
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = pessoa.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = d2.Id,
                Valor = 30m,
                Vencimento = new DateOnly(2026, 9, 9),
                Fechado = true,
                DataFechamento = new DateTime(2026, 9, 10, 12, 0, 0)
            });
            var d3 = await NovaDespesaCartaoAsync(db, cartao, cat, new DateOnly(2026, 10, 10));
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = pessoa.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = d3.Id,
                Valor = 50m,
                Vencimento = new DateOnly(2026, 10, 9)
            });
            await db.SaveChangesAsync();

            var lista = await service.ObterPendentesAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30));

            var unico = Assert.Single(lista);
            Assert.Equal(100m, unico.Valor);
            Assert.False(unico.Fechado);
            Assert.NotNull(unico.Pessoa);
            Assert.NotNull(unico.CartaoCredito);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Reembolso_LancamentoId_Unico_ViolaEmDuplicado()
    {
        var (db, file, _, cartao, pessoa, cat) = await SetupAsync();
        try
        {
            var despesa = await NovaDespesaCartaoAsync(db, cartao, cat, new DateOnly(2026, 9, 10));
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = pessoa.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = despesa.Id,
                Valor = 100m,
                Vencimento = new DateOnly(2026, 9, 9)
            });
            await db.SaveChangesAsync();

            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = pessoa.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = despesa.Id,
                Valor = 100m,
                Vencimento = new DateOnly(2026, 9, 9)
            });
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
