using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ReembolsoEdicaoTests
{
    private static async Task<(AppDbContext Db, string File, ReembolsoService Svc, Reembolso R)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartao = new CartaoCredito { Banco = "Nu", Ultimos4Digitos = "1234", MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id };
        db.CartoesCredito.Add(cartao);
        var pessoa = new Pessoa { Nome = "A" };
        db.Pessoas.Add(pessoa);
        await db.SaveChangesAsync();
        var cat = db.Categorias.First(c => c.Nome == "Receita");
        var despesa = new Lancamento { Data = new DateOnly(2026, 8, 6), DataCompra = new DateOnly(2026, 8, 6), DataVencimentoCartao = new DateOnly(2026, 9, 10), Tipo = LancamentoTipo.Despesa, Valor = -100m, CartaoCreditoId = cartao.Id, CategoriaId = cat.Id, Confirmado = true };
        db.Lancamentos.Add(despesa);
        await db.SaveChangesAsync();
        var r = new Reembolso { PessoaId = pessoa.Id, CartaoCreditoId = cartao.Id, LancamentoId = despesa.Id, Valor = 100m, Vencimento = new DateOnly(2026, 9, 9) };
        db.Reembolsos.Add(r);
        await db.SaveChangesAsync();
        return (db, file, new ReembolsoService(db), r);
    }

    [Fact]
    public async Task Atualizar_AlteraValorEVencimento()
    {
        var (db, file, svc, r) = await SetupAsync();
        try
        {
            var atualizado = await svc.AtualizarAsync(r.Id, 80m, new DateOnly(2026, 9, 8));
            Assert.Equal(80m, atualizado.Valor);
            Assert.Equal(new DateOnly(2026, 9, 8), atualizado.Vencimento);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Atualizar_Fechado_LancaExcecao()
    {
        var (db, file, svc, r) = await SetupAsync();
        try
        {
            r.Fechado = true;
            await db.SaveChangesAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() => svc.AtualizarAsync(r.Id, 80m, new DateOnly(2026, 9, 8)));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Atualizar_ValorZero_LancaExcecao()
    {
        var (db, file, svc, r) = await SetupAsync();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() => svc.AtualizarAsync(r.Id, 0m, new DateOnly(2026, 9, 8)));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
