using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ReembolsoBaixaParcialTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Lancamentos, FaturaService Faturas, CartaoCredito Cartao, Conta Conta, Categoria CatReceita)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartao = new CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234", MelhorDiaCompra = 5,
            DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
        };
        db.CartoesCredito.Add(cartao);
        await db.SaveChangesAsync();
        var catReceita = db.Categorias.First(c => c.Nome == "Receita");
        return (db, file, new LancamentoService(db), new FaturaService(db), cartao, conta, catReceita);
    }

    private static async Task ConfirmarTodosAsync(LancamentoService svc, List<Lancamento> lista)
    {
        foreach (var d in lista) await svc.AlternarConfirmadoAsync(d.Id);
    }

    [Fact]
    public async Task BaixaParcial_CriaUmaReceitaPorPessoa_SoParaSelecionados()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoaA = new Pessoa { Nome = "A" };
            var pessoaB = new Pessoa { Nome = "B" };
            db.Pessoas.AddRange(pessoaA, pessoaB);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            var d2 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 60m, catReceita.Id, null, null, null, pessoaA.Id, null);
            var d3 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 30m, catReceita.Id, null, null, null, pessoaB.Id, null);
            await ConfirmarTodosAsync(lancamentos, d1.Concat(d2).Concat(d3).ToList());
            var todos = db.Reembolsos.ToList();
            var selA = todos.Single(r => r.PessoaId == pessoaA.Id && r.Valor == 100m);
            var selB = todos.Single(r => r.PessoaId == pessoaB.Id);

            var receitas = await faturas.FecharReembolsosSelecionadosAsync(
                new List<Guid> { selA.Id, selB.Id }, conta.Id, catReceita.Id, null);

            Assert.Equal(2, receitas.Count);
            Assert.Equal(100m, receitas.Single(r => r.PessoaId == pessoaA.Id).Valor);
            Assert.Equal(30m, receitas.Single(r => r.PessoaId == pessoaB.Id).Valor);
            Assert.All(receitas, r => Assert.False(r.Confirmado));
            var restante = todos.Single(r => r.PessoaId == pessoaA.Id && r.Valor == 60m);
            Assert.False(db.Reembolsos.Single(r => r.Id == restante.Id).Fechado);
            Assert.Null(db.Reembolsos.Single(r => r.Id == restante.Id).ReceitaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task BaixaParcial_FechamentoFinal_NaoDuplica()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoaA = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoaA);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            var inicio = new DateOnly(2026, 9, 1);
            var fim = new DateOnly(2026, 9, 30);
            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            var d2 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 60m, catReceita.Id, null, null, null, pessoaA.Id, null);
            await ConfirmarTodosAsync(lancamentos, d1.Concat(d2).ToList());
            var primeiro = db.Reembolsos.Single(r => r.Valor == 100m);
            await faturas.FecharReembolsosSelecionadosAsync(new List<Guid> { primeiro.Id }, conta.Id, catReceita.Id, null);
            await faturas.FecharComReembolsosAsync(cartao.Id, 2026, 9, inicio, fim, conta.Id, catReceita.Id, null);
            var receitas = db.Lancamentos.Where(l => l.Tipo == LancamentoTipo.Receita && l.ContaId == conta.Id).ToList();
            Assert.Equal(2, receitas.Count);
            Assert.Contains(receitas, r => r.Valor == 100m);
            Assert.Contains(receitas, r => r.Valor == 60m);
            Assert.True(db.Reembolsos.All(r => r.Fechado));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task BaixaParcial_Reabrir_Reverte()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoaA = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoaA);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            await ConfirmarTodosAsync(lancamentos, d1);
            var unico = db.Reembolsos.Single();
            await faturas.FecharReembolsosSelecionadosAsync(new List<Guid> { unico.Id }, conta.Id, catReceita.Id, null);
            Assert.Equal(1, db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Receita));
            // Adaptação NOTE (brief): FecharReembolsosSelecionadosAsync não cria Fatura;
            // ReabrirAsync exige fatura fechada — fechar fatura (sem reembolsos restantes) antes de reabrir.
            var inicio = new DateOnly(2026, 9, 1);
            var fim = new DateOnly(2026, 9, 30);
            await faturas.FecharComReembolsosAsync(cartao.Id, 2026, 9, inicio, fim, conta.Id, catReceita.Id, null);
            await faturas.ReabrirAsync(cartao.Id, 2026, 9);
            Assert.Empty(db.Lancamentos.Where(l => l.Tipo == LancamentoTipo.Receita && l.ContaId == conta.Id));
            Assert.True(db.Reembolsos.All(r => !r.Fechado && r.ReceitaId == null));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task BaixaParcial_JaFechado_LancaExcecao()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoaA = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoaA);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            await ConfirmarTodosAsync(lancamentos, d1);
            var unico = db.Reembolsos.Single();
            await faturas.FecharReembolsosSelecionadosAsync(new List<Guid> { unico.Id }, conta.Id, catReceita.Id, null);
            var antes = db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Receita);
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                faturas.FecharReembolsosSelecionadosAsync(new List<Guid> { unico.Id }, conta.Id, catReceita.Id, null));
            Assert.Equal(antes, db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Receita));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task BaixaParcial_DespesaNaoConfirmada_PermiteAdiantamentoSemFecharFatura()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoaA = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoaA);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            // Sem ConfirmarTodosAsync: despesa permanece não confirmada.
            await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            var unico = db.Reembolsos.Single();
            var receitas = await faturas.FecharReembolsosSelecionadosAsync(new List<Guid> { unico.Id }, conta.Id, catReceita.Id, null);
            Assert.Single(receitas);
            Assert.Equal(100m, receitas[0].Valor);
            Assert.False(receitas[0].Confirmado);
            Assert.True(db.Reembolsos.Single(r => r.Id == unico.Id).Fechado);
            // Fatura deve continuar intacta/aberta.
            Assert.False(await faturas.EhFechadaAsync(cartao.Id, 2026, 9));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task BaixaParcial_FaturaFechada_LancaExcecao()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var pessoaA = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoaA);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            var inicio = new DateOnly(2026, 9, 1);
            var fim = new DateOnly(2026, 9, 30);
            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            await ConfirmarTodosAsync(lancamentos, d1);
            await faturas.FecharComReembolsosAsync(cartao.Id, 2026, 9, inicio, fim, conta.Id, catReceita.Id, null);
            // Isola a guarda de fatura-fechada: insere direto um Reembolso não-fechado no
            // período fechado (via serviço seria barrado por GarantirFaturaAbertaAsync).
            var despesaExtra = new Lancamento
            {
                Data = new DateOnly(2026, 9, 10),
                DataVencimentoCartao = new DateOnly(2026, 9, 10),
                Tipo = LancamentoTipo.Despesa,
                Valor = -40m,
                CartaoCreditoId = cartao.Id,
                CategoriaId = catReceita.Id,
                Confirmado = true
            };
            db.Lancamentos.Add(despesaExtra);
            await db.SaveChangesAsync();
            var extra = new Reembolso
            {
                PessoaId = pessoaA.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = despesaExtra.Id,
                Valor = 40m,
                Vencimento = new DateOnly(2026, 9, 9)
            };
            db.Reembolsos.Add(extra);
            await db.SaveChangesAsync();
            var antes = db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Receita);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                faturas.FecharReembolsosSelecionadosAsync(new List<Guid> { extra.Id }, conta.Id, catReceita.Id, null));
            Assert.Equal("Fatura já fechada.", ex.Message);
            Assert.Equal(antes, db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Receita));
            Assert.False(db.Reembolsos.Single(r => r.Id == extra.Id).Fechado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task BaixaParcial_CartoesDiferentes_LancaExcecao()
    {
        var (db, file, lancamentos, faturas, cartao, conta, catReceita) = await SetupAsync();
        try
        {
            var cartao2 = new CartaoCredito
            {
                Banco = "Itaú", Ultimos4Digitos = "5678", MelhorDiaCompra = 5,
                DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
            };
            db.CartoesCredito.Add(cartao2);
            var pessoaA = new Pessoa { Nome = "A" };
            db.Pessoas.Add(pessoaA);
            await db.SaveChangesAsync();
            var compra = new DateOnly(2026, 8, 6);
            var d1 = await lancamentos.CriarDespesaCartaoAsync(cartao.Id, compra, 100m, catReceita.Id, null, null, null, pessoaA.Id, null);
            var d2 = await lancamentos.CriarDespesaCartaoAsync(cartao2.Id, compra, 50m, catReceita.Id, null, null, null, pessoaA.Id, null);
            await ConfirmarTodosAsync(lancamentos, d1.Concat(d2).ToList());
            var ids = db.Reembolsos.Select(r => r.Id).ToList();
            Assert.Equal(2, ids.Count);
            var antes = db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Receita);
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                faturas.FecharReembolsosSelecionadosAsync(ids, conta.Id, catReceita.Id, null));
            Assert.Equal("Os reembolsos selecionados pertencem a faturas diferentes.", ex.Message);
            Assert.Equal(antes, db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Receita));
            Assert.True(db.Reembolsos.All(r => !r.Fechado));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
