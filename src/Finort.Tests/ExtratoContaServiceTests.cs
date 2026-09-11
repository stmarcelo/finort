using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ExtratoContaServiceTests
{
    private static Guid CategoriaId(Data.AppDbContext db)
        => db.Categorias.First(c => c.Nome == "Contas de casa").Id;

    private static ExtratoContaService Servico(Data.AppDbContext db)
        => new(db, new FaturaService(db));

    private static CartaoCredito NovoCartao(Data.AppDbContext db, Conta conta)
    {
        var cartao = new CartaoCredito
        {
            Banco = "Nu", Ultimos4Digitos = "1234", MelhorDiaCompra = 1,
            DiaVencimento = 10, Limite = 1000m, Ativo = true, ContaId = conta.Id
        };
        db.CartoesCredito.Add(cartao);
        db.SaveChanges();
        return cartao;
    }

    private static void NovaDespesaCartao(Data.AppDbContext db, Guid cartaoId, Guid cat,
        DateOnly compra, DateOnly vencimento, decimal valor, bool confirmado)
        => db.Lancamentos.Add(new Lancamento
        {
            Data = compra,
            DataCompra = compra,
            DataVencimentoCartao = vencimento,
            Tipo = LancamentoTipo.Despesa,
            Valor = -Math.Abs(valor),
            CartaoCreditoId = cartaoId,
            CategoriaId = cat,
            Confirmado = confirmado
        });

    [Fact]
    public async Task ObterAsync_SemFechamento_CalculaTresSaldos()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            db.Lancamentos.AddRange(
                new Lancamento { Data = new DateOnly(2026, 9, 5), Valor = 100m, Confirmado = true, CategoriaId = cat, ContaId = conta.Id },
                new Lancamento { Data = new DateOnly(2026, 9, 6), Valor = -40m, Confirmado = false, CategoriaId = cat, ContaId = conta.Id });
            await db.SaveChangesAsync();

            var r = await Servico(db).ObterAsync(conta.Id, 2026, 9);

            Assert.False(r.Fechado);
            Assert.Equal(60m, r.SaldoMes);
            Assert.Equal(100m, r.AcumuladoReal);
            Assert.Equal(60m, r.AcumuladoPrevisto);
            Assert.Equal(2, r.Lancamentos.Count);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_MesFechado_RetornaSaldoGravado()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            db.Lancamentos.Add(new Lancamento { Data = new DateOnly(2026, 8, 10), Valor = 200m, Confirmado = true, CategoriaId = cat, ContaId = conta.Id });
            await db.SaveChangesAsync();
            await new FechamentoService(db).FecharAsync(conta.Id, 2026, 8, 200m);

            var r = await Servico(db).ObterAsync(conta.Id, 2026, 8);

            Assert.True(r.Fechado);
            Assert.Equal(200m, r.AcumuladoReal);
            Assert.NotNull(r.DataFechamento);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_MesAposFechamento_AcumulaAPartirDaBase()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            db.Lancamentos.Add(new Lancamento { Data = new DateOnly(2026, 8, 10), Valor = 200m, Confirmado = true, CategoriaId = cat, ContaId = conta.Id });
            await db.SaveChangesAsync();
            await new FechamentoService(db).FecharAsync(conta.Id, 2026, 8, 200m);
            db.Lancamentos.AddRange(
                new Lancamento { Data = new DateOnly(2026, 9, 2), Valor = 50m, Confirmado = true, CategoriaId = cat, ContaId = conta.Id },
                new Lancamento { Data = new DateOnly(2026, 9, 3), Valor = -20m, Confirmado = false, CategoriaId = cat, ContaId = conta.Id });
            await db.SaveChangesAsync();

            var r = await Servico(db).ObterAsync(conta.Id, 2026, 9);

            Assert.False(r.Fechado);
            Assert.Equal(30m, r.SaldoMes);
            Assert.Equal(250m, r.AcumuladoReal);
            Assert.Equal(230m, r.AcumuladoPrevisto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_ListaSoDaContaEMes()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "A" };
            var outra = new Conta { Nome = "B" };
            db.Contas.AddRange(conta, outra);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            var cartao = new CartaoCredito { Banco = "Nu", Ultimos4Digitos = "1234", MelhorDiaCompra = 1, DiaVencimento = 10, Limite = 1000m, Ativo = true };
            db.CartoesCredito.Add(cartao);
            await db.SaveChangesAsync();
            db.Lancamentos.AddRange(
                new Lancamento { Data = new DateOnly(2026, 9, 5), Valor = 10m, Confirmado = true, CategoriaId = cat, ContaId = conta.Id },
                new Lancamento { Data = new DateOnly(2026, 9, 5), Valor = 99m, Confirmado = true, CategoriaId = cat, ContaId = outra.Id },
                new Lancamento { Data = new DateOnly(2026, 8, 5), Valor = 99m, Confirmado = true, CategoriaId = cat, ContaId = conta.Id },
                new Lancamento { Data = new DateOnly(2026, 9, 5), Valor = -50m, Confirmado = true, CategoriaId = cat, CartaoCreditoId = cartao.Id });
            await db.SaveChangesAsync();

            var r = await Servico(db).ObterAsync(conta.Id, 2026, 9);

            var unico = Assert.Single(r.Lancamentos);
            Assert.Equal(10m, unico.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public void Agrupar_MesmaDataPessoaEConfirmado_NoMesmoGrupo()
    {
        var cat = Guid.NewGuid();
        var pessoa = Guid.NewGuid();
        var lista = new List<Lancamento>
        {
            new() { Data = new DateOnly(2026, 9, 5), Valor = 10m, Confirmado = false, CategoriaId = cat, PessoaId = pessoa },
            new() { Data = new DateOnly(2026, 9, 5), Valor = 20m, Confirmado = false, CategoriaId = cat, PessoaId = pessoa },
            new() { Data = new DateOnly(2026, 9, 5), Valor = 30m, Confirmado = true, CategoriaId = cat, PessoaId = pessoa },
        };

        var grupos = ExtratoContaService.Agrupar(lista);

        Assert.Equal(2, grupos.Count);
        var g = grupos.Single(x => !x.Confirmado);
        Assert.Equal(2, g.Itens.Count);
        Assert.Equal(30m, g.Soma);
    }

    [Fact]
    public async Task ObterAsync_CartaoVinculadoAberto_PrevistoIncluiFaturaERealNao()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            var cartao = NovoCartao(db, conta);
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 9, 5), Valor = 100m, Confirmado = true,
                CategoriaId = cat, ContaId = conta.Id
            });
            NovaDespesaCartao(db, cartao.Id, cat,
                new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10), 40m, confirmado: true);
            await db.SaveChangesAsync();

            var r = await Servico(db).ObterAsync(conta.Id, 2026, 9);

            Assert.Equal(100m, r.AcumuladoReal);
            Assert.Equal(60m, r.AcumuladoPrevisto);
            Assert.Equal(60m, r.SaldoMes);
            var fatura = Assert.Single(r.Faturas);
            Assert.False(fatura.Fechada);
            Assert.Equal(-40m, fatura.ContribuicaoPrevisto);
            Assert.Equal(40m, fatura.Restante);
            Assert.Single(fatura.Itens);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_CartaoNaoVinculado_SemSecaoFatura()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            var cartao = new CartaoCredito
            {
                Banco = "Nu", Ultimos4Digitos = "9999", MelhorDiaCompra = 1,
                DiaVencimento = 10, Limite = 1000m, Ativo = true
            };
            db.CartoesCredito.Add(cartao);
            await db.SaveChangesAsync();
            NovaDespesaCartao(db, cartao.Id, cat,
                new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10), 40m, confirmado: true);
            await db.SaveChangesAsync();

            var r = await Servico(db).ObterAsync(conta.Id, 2026, 9);

            Assert.Empty(r.Faturas);
            Assert.Equal(0m, r.AcumuladoPrevisto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_FaturaFechadaPaga_ContribuicaoZero()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            var cartao = NovoCartao(db, conta);
            NovaDespesaCartao(db, cartao.Id, cat,
                new DateOnly(2026, 9, 3), new DateOnly(2026, 9, 10), 200m, confirmado: true);
            await db.SaveChangesAsync();

            var faturaSvc = new FaturaService(db);
            var inicio = new DateOnly(2026, 9, 1);
            await faturaSvc.FecharAsync(cartao.Id, 2026, 9, inicio, inicio.AddMonths(1).AddDays(-1));
            await faturaSvc.PagarAsync(cartao.Id, 2026, 9, conta.Id, new DateOnly(2026, 9, 15), 200m);

            var r = await Servico(db).ObterAsync(conta.Id, 2026, 9);

            var fatura = Assert.Single(r.Faturas);
            Assert.True(fatura.Fechada);
            Assert.True(fatura.Paga);
            Assert.Equal(200m, fatura.Pago);
            Assert.Equal(0m, fatura.ContribuicaoPrevisto);
            Assert.Equal(-200m, r.SaldoMes);
            Assert.Equal(-200m, r.AcumuladoReal);
            Assert.Equal(-200m, r.AcumuladoPrevisto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_PagamentoParcial_SemDuplicarRolloverNoPrevisto()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            var cartao = NovoCartao(db, conta);
            NovaDespesaCartao(db, cartao.Id, cat,
                new DateOnly(2026, 8, 3), new DateOnly(2026, 8, 10), 1000m, confirmado: true);
            await db.SaveChangesAsync();

            var faturaSvc = new FaturaService(db);
            var inicioAgo = new DateOnly(2026, 8, 1);
            await faturaSvc.FecharAsync(cartao.Id, 2026, 8, inicioAgo, inicioAgo.AddMonths(1).AddDays(-1));
            await faturaSvc.PagarAsync(cartao.Id, 2026, 8, conta.Id, new DateOnly(2026, 9, 10), 600m);

            var agosto = await Servico(db).ObterAsync(conta.Id, 2026, 8);
            var faturaAgo = Assert.Single(agosto.Faturas);
            Assert.True(faturaAgo.Fechada);
            Assert.Equal(-400m, faturaAgo.ContribuicaoPrevisto);

            var setembro = await Servico(db).ObterAsync(conta.Id, 2026, 9);
            var faturaSet = Assert.Single(setembro.Faturas);
            Assert.False(faturaSet.Fechada);
            Assert.Equal(-400m, faturaSet.ContribuicaoPrevisto);
            Assert.Equal(-1000m, setembro.SaldoMes);
            Assert.Equal(-1000m, setembro.AcumuladoPrevisto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterAsync_ItemSemVencimento_NaoEntraNaFatura()
    {
        var (db, file) = TestDbContext.Create();
        try
        {
            var conta = new Conta { Nome = "Banco" };
            db.Contas.Add(conta);
            await db.SaveChangesAsync();
            var cat = CategoriaId(db);
            var cartao = NovoCartao(db, conta);
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 9, 5),
                DataVencimentoCartao = null,
                Tipo = LancamentoTipo.Despesa,
                Valor = -50m,
                CartaoCreditoId = cartao.Id,
                CategoriaId = cat,
                Confirmado = true
            });
            await db.SaveChangesAsync();

            var r = await Servico(db).ObterAsync(conta.Id, 2026, 9);

            Assert.Empty(r.Faturas);
            Assert.Equal(0m, r.AcumuladoPrevisto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public void AgruparPorVencimento_ChaveEhVencimentoNaoDataCompra()
    {
        var cat = Guid.NewGuid();
        var pessoa = Guid.NewGuid();
        var lista = new List<Lancamento>
        {
            new() { Data = new DateOnly(2026, 9, 3), DataVencimentoCartao = new DateOnly(2026, 9, 10), Valor = -10m, Confirmado = false, CategoriaId = cat, PessoaId = pessoa },
            new() { Data = new DateOnly(2026, 9, 20), DataVencimentoCartao = new DateOnly(2026, 9, 10), Valor = -20m, Confirmado = false, CategoriaId = cat, PessoaId = pessoa },
            new() { Data = new DateOnly(2026, 9, 20), DataVencimentoCartao = new DateOnly(2026, 10, 10), Valor = -30m, Confirmado = false, CategoriaId = cat, PessoaId = pessoa },
        };

        var grupos = ExtratoContaService.AgruparPorVencimento(lista);

        Assert.Equal(2, grupos.Count);
        var mesmoVencimento = grupos.Single(g => g.Data == new DateOnly(2026, 9, 10));
        Assert.Equal(2, mesmoVencimento.Itens.Count);
        Assert.Equal(-30m, mesmoVencimento.Soma);
    }
}
