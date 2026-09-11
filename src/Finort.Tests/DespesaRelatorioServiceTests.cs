using Finort.Data;
using Finort.Services;

namespace Finort.Tests;

public class DespesaRelatorioServiceTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly DespesaRelatorioService _svc;

    public DespesaRelatorioServiceTests()
    {
        _svc = new DespesaRelatorioService(_ctx.Db, new TestWebHostEnvironment());
    }

    private static (Guid ContaId, Guid CartaoId, Guid CategoriaId) SeedBase(AppDbContext db)
    {
        var conta = db.Contas.Add(new Models.Financeiro.Conta { Nome = "Banco" }).Entity;
        var cartao = db.CartoesCredito.Add(new Models.Financeiro.CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234",
            MelhorDiaCompra = 1, DiaVencimento = 10, Limite = 5000m, Ativo = true
        }).Entity;
        var categoria = db.Categorias.Add(new Models.Financeiro.Categoria { Nome = "Mercado" }).Entity;
        db.SaveChanges();
        return (conta.Id, cartao.Id, categoria.Id);
    }

    [Fact]
    public async Task Gerar_CartaoEntraPorVencimento_NormalPorData()
    {
        var db = _ctx.Db;
        var (contaId, cartaoId, categoriaId) = SeedBase(db);
        var ana = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Ana" }).Entity;
        db.SaveChanges();
        db.Lancamentos.AddRange(
            // cartão: compra em agosto, vencimento em setembro -> entra em setembro
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 8, 28), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -200m, CartaoCreditoId = cartaoId, DataVencimentoCartao = new(2026, 9, 10),
                CategoriaId = categoriaId, PessoaId = ana.Id, Confirmado = true
            },
            // cartão: compra em setembro, vencimento em outubro -> fora de setembro
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 25), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -300m, CartaoCreditoId = cartaoId, DataVencimentoCartao = new(2026, 10, 8),
                CategoriaId = categoriaId, PessoaId = ana.Id, Confirmado = false
            },
            // normal em setembro -> entra
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 5), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -100m, ContaId = contaId,
                CategoriaId = categoriaId, PessoaId = ana.Id, Confirmado = false
            });
        db.SaveChanges();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), ana.Id, null, null);

        Assert.Equal(200m, r.TotalConfirmado);
        Assert.Equal(100m, r.TotalNaoConfirmado);
        Assert.Equal(2, r.Linhas.Count);
        // ordenado pela data-critério: normal (05/09) antes do cartão (venc. 10/09)
        Assert.Equal([new DateOnly(2026, 9, 5), new DateOnly(2026, 9, 10)], r.Linhas.Select(l => l.Data));
        Assert.Equal("Nubank", r.Linhas[1].CartaoNome);
    }

    [Fact]
    public async Task Gerar_FiltroCartao_RestringeEDetalhaPorPessoa()
    {
        var db = _ctx.Db;
        var (contaId, cartaoId, categoriaId) = SeedBase(db);
        var ana = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Ana" }).Entity;
        var bob = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Bob" }).Entity;
        db.SaveChanges();
        db.Lancamentos.AddRange(
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 2), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -200m, CartaoCreditoId = cartaoId, DataVencimentoCartao = new(2026, 9, 10),
                CategoriaId = categoriaId, PessoaId = ana.Id, Confirmado = true
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 3), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -50m, ContaId = contaId,
                CategoriaId = categoriaId, PessoaId = bob.Id, Confirmado = true
            });
        db.SaveChanges();

        var filtrado = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), null, cartaoId, null);

        Assert.Equal(200m, filtrado.TotalConfirmado);
        Assert.Single(filtrado.SubtotaisPorPessoa);
        Assert.Equal("Ana", filtrado.SubtotaisPorPessoa[0].PessoaNome);

        var tudo = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), null, null, null);

        Assert.Equal(250m, tudo.TotalConfirmado);
        Assert.Equal(2, tudo.SubtotaisPorPessoa.Count);
    }

    [Fact]
    public async Task PeriodoPadrao_ComAntecipacao_UsaJanelaDaAteDaMaisUm()
    {
        var db = _ctx.Db;
        db.Configuracoes.Add(new Models.Auth.Configuracao { Nome = "T", Email = "t@t.com", DiasAntecipacao = 5 });
        db.SaveChanges();

        var (inicio, fim) = await _svc.PeriodoPadraoDoMesAsync();

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var esperadoInicio = new DateOnly(hoje.Year, hoje.Month, Math.Min(6, DateTime.DaysInMonth(hoje.Year, hoje.Month)));
        var prox = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1);
        var esperadoFim = new DateOnly(prox.Year, prox.Month, Math.Min(5, DateTime.DaysInMonth(prox.Year, prox.Month)));
        Assert.Equal(esperadoInicio, inicio);
        Assert.Equal(esperadoFim, fim);
    }

    [Fact]
    public async Task Gerar_FiltroCategoria_IncluiDiretaESubcategoriaEAgrupa()
    {
        var db = _ctx.Db;
        var (contaId, _, _) = SeedBase(db);
        var mercado = db.Categorias.Add(new Models.Financeiro.Categoria { Nome = "Alimentação" }).Entity;
        var sup = db.Subcategorias.Add(new Models.Financeiro.Subcategoria { Nome = "Supermercado", CategoriaId = mercado.Id }).Entity;
        var outra = db.Categorias.Add(new Models.Financeiro.Categoria { Nome = "Lazer" }).Entity;
        var ana = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Ana" }).Entity;
        db.SaveChanges();
        db.Lancamentos.AddRange(
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 2), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -100m, ContaId = contaId,
                CategoriaId = mercado.Id, PessoaId = ana.Id, Confirmado = true
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 3), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -50m, ContaId = contaId,
                CategoriaId = mercado.Id, SubcategoriaId = sup.Id, PessoaId = ana.Id, Confirmado = false
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 4), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -70m, ContaId = contaId,
                CategoriaId = outra.Id, PessoaId = ana.Id, Confirmado = true
            });
        db.SaveChanges();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), ana.Id, null, mercado.Id);

        Assert.Equal(100m, r.TotalConfirmado);
        Assert.Equal(50m, r.TotalNaoConfirmado);
        Assert.Equal(2, r.Linhas.Count);
        Assert.Equal("Alimentação", r.CategoriaNome);
        Assert.Equal(2, r.SubtotaisPorCategoria.Count);
        Assert.Equal("Alimentação", r.SubtotaisPorCategoria[0].Rotulo);
        Assert.Equal(100m, r.SubtotaisPorCategoria[0].Confirmado);
        Assert.Equal("Alimentação > Supermercado", r.SubtotaisPorCategoria[1].Rotulo);
        Assert.Equal(50m, r.SubtotaisPorCategoria[1].NaoConfirmado);
    }

    [Fact]
    public async Task Gerar_FiltroSemPessoa_RetornaSomenteSemPessoa()
    {
        var db = _ctx.Db;
        var (contaId, _, categoriaId) = SeedBase(db);
        var ana = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Ana" }).Entity;
        db.SaveChanges();
        db.Lancamentos.AddRange(
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 2), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -100m, ContaId = contaId,
                CategoriaId = categoriaId, PessoaId = ana.Id, Confirmado = true
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 3), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
                Valor = -40m, ContaId = contaId,
                CategoriaId = categoriaId, PessoaId = null, Confirmado = false
            });
        db.SaveChanges();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), Guid.Empty, null, null);

        Assert.Equal("Sem pessoa", r.PessoaNome);
        Assert.Equal(0m, r.TotalConfirmado);
        Assert.Equal(40m, r.TotalNaoConfirmado);
        Assert.Single(r.Linhas);
    }

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);
}
