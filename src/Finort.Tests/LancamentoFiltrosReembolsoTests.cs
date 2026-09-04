using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class LancamentoFiltrosReembolsoTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Service, Conta Conta, CartaoCredito Cartao)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartao = new CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234", MelhorDiaCompra = 5,
            DiaVencimento = 10, Limite = 1000m, Ativo = true, ContaId = conta.Id
        };
        db.CartoesCredito.Add(cartao);
        await db.SaveChangesAsync();
        return (db, file, new LancamentoService(db), conta, cartao);
    }

    private static Categoria Renda(AppDbContext db) => db.Categorias.First(c => c.Nome == "Receita");

    [Fact]
    public async Task CriarDespesaCartaoAsync_ReembolsoComConta_GravaContaNoReembolso()
    {
        var (db, file, service, conta, cartao) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 60m, Renda(db).Id, null, null,
                parcelas: null, reembolsoPessoaId: pessoa.Id, reembolsoVencimento: null,
                vencimentoExato: null, reembolsoContaId: conta.Id);

            var despesa = Assert.Single(despesas);
            var reembolso = await db.Lancamentos.FindAsync(despesa.ReembolsoId!.Value);
            Assert.NotNull(reembolso);
            Assert.Equal(conta.Id, reembolso!.ContaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ListarAsync_FiltraPorPessoaECartao()
    {
        var (db, file, service, conta, cartao) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "João" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            await service.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 50m,
                Renda(db).Id, null, pessoa.Id, null, null, null);
            await service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 7), 20m, Renda(db).Id, null, null);

            var daPessoa = await service.ListarAsync(pessoaId: pessoa.Id);
            Assert.Single(daPessoa);
            Assert.NotNull(daPessoa[0].CartaoCreditoId);

            var doCartao = await service.ListarAsync(cartaoId: cartao.Id);
            Assert.Single(doCartao);

            var tudo = await service.ListarAsync();
            Assert.Equal(2, tudo.Count);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
