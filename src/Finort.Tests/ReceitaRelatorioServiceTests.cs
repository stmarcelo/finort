using Finort.Data;
using Finort.Services;

namespace Finort.Tests;

public class ReceitaRelatorioServiceTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly ReceitaRelatorioService _svc;

    public ReceitaRelatorioServiceTests()
    {
        _svc = new ReceitaRelatorioService(_ctx.Db, new TestWebHostEnvironment());
    }

    [Fact]
    public async Task Gerar_PessoaUnica_TotaisLinhasESubtotaisOrigem()
    {
        var db = _ctx.Db;
        var ana = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Ana" }).Entity;
        var bob = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Bob" }).Entity;
        var conta = db.Contas.Add(new Models.Financeiro.Conta { Nome = "Banco" }).Entity;
        db.SaveChanges();
        var renda = db.Categorias.First(c => c.Nome == "Receita");
        db.Lancamentos.AddRange(
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 2), Tipo = Models.Financeiro.LancamentoTipo.Receita,
                Valor = 1000m, ContaId = conta.Id, CategoriaId = renda.Id,
                PessoaId = ana.Id, Confirmado = true
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 5), Tipo = Models.Financeiro.LancamentoTipo.Receita,
                Valor = 500m, ContaId = conta.Id, CategoriaId = renda.Id,
                PessoaId = ana.Id, Confirmado = false
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 6), Tipo = Models.Financeiro.LancamentoTipo.Receita,
                Valor = 700m, ContaId = conta.Id, CategoriaId = renda.Id,
                PessoaId = bob.Id, Confirmado = true
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 8, 20), Tipo = Models.Financeiro.LancamentoTipo.Receita,
                Valor = 999m, ContaId = conta.Id, CategoriaId = renda.Id,
                PessoaId = ana.Id, Confirmado = true
            });
        db.SaveChanges();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), ana.Id);

        Assert.Equal(1000m, r.TotalConfirmado);
        Assert.Equal(500m, r.TotalNaoConfirmado);
        Assert.Equal(2, r.Linhas.Count);
        Assert.Equal([new DateOnly(2026, 9, 2), new DateOnly(2026, 9, 5)], r.Linhas.Select(l => l.Data));
        var origem = Assert.Single(r.SubtotaisPorOrigem);
        Assert.Equal(1000m, origem.Confirmado);
        Assert.Equal(500m, origem.NaoConfirmado);
        Assert.Empty(r.SubtotaisPorPessoa);
    }

    [Fact]
    public async Task Gerar_Todas_AgrupaPorPessoaSemDetalhe()
    {
        var db = _ctx.Db;
        var ana = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Ana" }).Entity;
        var bob = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Bob" }).Entity;
        var conta = db.Contas.Add(new Models.Financeiro.Conta { Nome = "Banco" }).Entity;
        db.SaveChanges();
        var renda = db.Categorias.First(c => c.Nome == "Receita");
        db.Lancamentos.AddRange(
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 2), Tipo = Models.Financeiro.LancamentoTipo.Receita,
                Valor = 100m, ContaId = conta.Id, CategoriaId = renda.Id,
                PessoaId = ana.Id, Confirmado = true
            },
            new Models.Financeiro.Lancamento
            {
                Data = new(2026, 9, 3), Tipo = Models.Financeiro.LancamentoTipo.Receita,
                Valor = 200m, ContaId = conta.Id, CategoriaId = renda.Id,
                PessoaId = bob.Id, Confirmado = false
            });
        db.SaveChanges();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), null);

        Assert.Equal(100m, r.TotalConfirmado);
        Assert.Equal(200m, r.TotalNaoConfirmado);
        Assert.Empty(r.Linhas);
        Assert.Equal(2, r.SubtotaisPorPessoa.Count);
        Assert.Equal("Ana", r.SubtotaisPorPessoa[0].PessoaNome);
        Assert.Equal(100m, r.SubtotaisPorPessoa[0].Confirmado);
        Assert.Equal(200m, r.SubtotaisPorPessoa[1].NaoConfirmado);
    }

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);

    [Fact]
    public async Task Gerar_ReceitaDeReembolso_ExibeCartaoDeOrigemNoDetalheENoAgrupamento()
    {
        var db = _ctx.Db;
        var ana = db.Pessoas.Add(new Models.Financeiro.Pessoa { Nome = "Ana" }).Entity;
        var conta = db.Contas.Add(new Models.Financeiro.Conta { Nome = "Banco" }).Entity;
        var cartao = db.CartoesCredito.Add(new Models.Financeiro.CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234",
            MelhorDiaCompra = 1, DiaVencimento = 10, Limite = 5000m, Ativo = true
        }).Entity;
        db.SaveChanges();
        var renda = db.Categorias.First(c => c.Nome == "Receita");

        var receita = db.Lancamentos.Add(new Models.Financeiro.Lancamento
        {
            Data = new(2026, 9, 10), Tipo = Models.Financeiro.LancamentoTipo.Receita,
            Valor = 250m, ContaId = conta.Id, CategoriaId = renda.Id,
            PessoaId = ana.Id, Confirmado = false
        }).Entity;
        db.SaveChanges();
        db.Lancamentos.Add(new Models.Financeiro.Lancamento
        {
            Data = new(2026, 9, 2), Tipo = Models.Financeiro.LancamentoTipo.Despesa,
            Valor = -250m, CartaoCreditoId = cartao.Id, DataVencimentoCartao = new(2026, 9, 10),
            CategoriaId = renda.Id, PessoaId = ana.Id, Confirmado = false,
            ReembolsoId = receita.Id
        });
        db.SaveChanges();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), ana.Id);

        var linha = Assert.Single(r.Linhas);
        Assert.Equal("Nubank", linha.CartaoNome);
        var origem = Assert.Single(r.SubtotaisPorOrigem);
        Assert.Contains("Nubank", origem.Rotulo);
        Assert.Equal(250m, origem.NaoConfirmado);
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
    public async Task PeriodoPadrao_SemAntecipacao_UsaMesCheio()
    {
        var db = _ctx.Db;
        db.Configuracoes.Add(new Models.Auth.Configuracao { Nome = "T", Email = "t@t.com", DiasAntecipacao = 0 });
        db.SaveChanges();

        var (inicio, fim) = await _svc.PeriodoPadraoDoMesAsync();

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        Assert.Equal(new DateOnly(hoje.Year, hoje.Month, 1), inicio);
        Assert.Equal(new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1), fim);
    }

    [Fact]
    public async Task PeriodoPadrao_SemConfiguracao_UsaMesCheio()
    {
        var (inicio, fim) = await _svc.PeriodoPadraoDoMesAsync();

        var hoje = DateOnly.FromDateTime(DateTime.Today);
        Assert.Equal(new DateOnly(hoje.Year, hoje.Month, 1), inicio);
        Assert.Equal(new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1), fim);
    }
}
