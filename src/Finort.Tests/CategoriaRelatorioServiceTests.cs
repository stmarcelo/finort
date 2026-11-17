using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class CategoriaRelatorioServiceTests : IDisposable
{
    private readonly (AppDbContext Db, string File) _ctx = TestDbContext.Create();
    private readonly CategoriaRelatorioService _svc;

    public CategoriaRelatorioServiceTests() => _svc = new CategoriaRelatorioService(_ctx.Db);

    private Guid CategoriaId(string nome)
    {
        var categoria = _ctx.Db.Categorias.Add(new Categoria { Nome = nome }).Entity;
        _ctx.Db.SaveChanges();
        return categoria.Id;
    }

    private Guid ContaId()
    {
        var conta = _ctx.Db.Contas.Add(new Conta { Nome = "Banco" }).Entity;
        _ctx.Db.SaveChanges();
        return conta.Id;
    }

    private Guid PessoaId(string nome)
    {
        var pessoa = _ctx.Db.Pessoas.Add(new Pessoa { Nome = nome }).Entity;
        _ctx.Db.SaveChanges();
        return pessoa.Id;
    }

    private void Lancar(DateOnly data, decimal valor, Guid categoriaId, Guid? contaId, Guid? pessoaId = null,
        Guid? subcategoriaId = null, bool confirmado = true, Guid? cartaoId = null, DateOnly? vencimento = null)
        => _ctx.Db.Lancamentos.Add(new Lancamento
        {
            Data = data,
            Tipo = valor >= 0 ? LancamentoTipo.Receita : LancamentoTipo.Despesa,
            Valor = valor,
            CategoriaId = categoriaId,
            SubcategoriaId = subcategoriaId,
            ContaId = contaId,
            PessoaId = pessoaId,
            Confirmado = confirmado,
            CartaoCreditoId = cartaoId,
            DataVencimentoCartao = vencimento
        });

    [Fact]
    public async Task PeriodoPadrao_VaiDeSeisMesesAtrasAteOUltimoDiaDoMesAtual()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var esperadoInicio = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(-6);
        var esperadoFim = new DateOnly(hoje.Year, hoje.Month, 1).AddMonths(1).AddDays(-1);

        var (inicio, fim) = await _svc.PeriodoPadraoAsync();

        Assert.Equal(esperadoInicio, inicio);
        Assert.Equal(esperadoFim, fim);
    }

    [Fact]
    public async Task Gerar_ReceitaMenosDespesa_CompensaValores()
    {
        var categoriaId = CategoriaId("Alimentação");
        var contaId = ContaId();
        var ana = PessoaId("Ana");
        Lancar(new(2026, 9, 2), 200m, categoriaId, contaId, ana, confirmado: true);
        Lancar(new(2026, 9, 3), -80m, categoriaId, contaId, ana, confirmado: false);
        await _ctx.Db.SaveChangesAsync();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), categoriaId, null);

        Assert.Equal(120m, r.TotalGeral);
        Assert.Equal("Alimentação", r.CategoriaNome);
        Assert.Null(r.SubcategoriaNome);
    }

    [Fact]
    public async Task Gerar_FiltroSubcategoria_RestringeAoLancamentoDaSubcategoria()
    {
        var categoriaId = CategoriaId("Alimentação");
        var contaId = ContaId();
        var sup = _ctx.Db.Subcategorias.Add(new Subcategoria { Nome = "Supermercado", CategoriaId = categoriaId }).Entity;
        var rest = _ctx.Db.Subcategorias.Add(new Subcategoria { Nome = "Restaurante", CategoriaId = categoriaId }).Entity;
        await _ctx.Db.SaveChangesAsync();
        Lancar(new(2026, 9, 2), -100m, categoriaId, contaId, subcategoriaId: sup.Id);
        Lancar(new(2026, 9, 3), -50m, categoriaId, contaId, subcategoriaId: rest.Id);
        Lancar(new(2026, 9, 4), -20m, categoriaId, contaId);
        await _ctx.Db.SaveChangesAsync();

        var filtrado = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), categoriaId, sup.Id);
        var todos = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), categoriaId, null);

        Assert.Equal(-100m, filtrado.TotalGeral);
        Assert.Equal("Supermercado", filtrado.SubcategoriaNome);
        Assert.Equal(-170m, todos.TotalGeral);
        Assert.Null(todos.SubcategoriaNome);
    }

    [Fact]
    public async Task Gerar_FiltraCategoriaEIgnoraTransferencia()
    {
        var categoriaId = CategoriaId("Alimentação");
        var outraId = CategoriaId("Lazer");
        var contaId = ContaId();
        Lancar(new(2026, 9, 2), -100m, categoriaId, contaId);
        Lancar(new(2026, 9, 3), -70m, outraId, contaId);
        _ctx.Db.Lancamentos.Add(new Lancamento
        {
            Data = new(2026, 9, 4), Tipo = LancamentoTipo.Transferencia, Valor = -500m,
            CategoriaId = categoriaId, ContaId = contaId
        });
        await _ctx.Db.SaveChangesAsync();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), categoriaId, null);

        Assert.Equal(-100m, r.TotalGeral);
    }

    [Fact]
    public async Task Gerar_AgrupaPorMesEPessoa_ComSemPessoaEOrdenado()
    {
        var categoriaId = CategoriaId("Alimentação");
        var contaId = ContaId();
        var ana = PessoaId("Ana");
        var bob = PessoaId("Bob");
        Lancar(new(2026, 9, 2), -100m, categoriaId, contaId, ana);
        Lancar(new(2026, 9, 3), 40m, categoriaId, contaId, bob);
        Lancar(new(2026, 9, 4), -10m, categoriaId, contaId);
        Lancar(new(2026, 10, 1), -60m, categoriaId, contaId, ana);
        await _ctx.Db.SaveChangesAsync();

        var r = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 10, 31), categoriaId, null);

        Assert.Equal(-130m, r.TotalGeral);
        Assert.Equal(2, r.Meses);
        Assert.Equal(-65m, r.MediaMensal);
        Assert.Equal(
            [(new DateOnly(2026, 9, 1), "Ana", -100m),
             (new DateOnly(2026, 9, 1), "Bob", 40m),
             (new DateOnly(2026, 9, 1), "Sem pessoa", -10m),
             (new DateOnly(2026, 10, 1), "Ana", -60m)],
            r.Linhas.Select(l => (l.Mes, l.PessoaNome, l.Total)));
    }

    [Fact]
    public async Task Gerar_MediaMensal_ConsideraSomenteMesesComLancamentos()
    {
        var categoriaId = CategoriaId("Alimentação");
        var contaId = ContaId();
        Lancar(new(2026, 10, 5), -100m, categoriaId, contaId);
        Lancar(new(2026, 11, 5), -50m, categoriaId, contaId);
        await _ctx.Db.SaveChangesAsync();

        var r = await _svc.GerarAsync(new(2026, 5, 1), new(2026, 11, 30), categoriaId, null);

        Assert.Equal(-150m, r.TotalGeral);
        Assert.Equal(2, r.Meses);
        Assert.Equal(-75m, r.MediaMensal);
    }

    [Fact]
    public async Task Gerar_SemLancamentos_MediaZero()
    {
        var categoriaId = CategoriaId("Alimentação");

        var r = await _svc.GerarAsync(new(2026, 5, 1), new(2026, 11, 30), categoriaId, null);

        Assert.Equal(0, r.Meses);
        Assert.Equal(0m, r.MediaMensal);
    }

    [Fact]
    public async Task Gerar_DespesaDeCartao_EntraPeloVencimento()
    {
        var categoriaId = CategoriaId("Alimentação");
        var cartao = _ctx.Db.CartoesCredito.Add(new CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234", MelhorDiaCompra = 1,
            DiaVencimento = 10, Limite = 5000m, Ativo = true
        }).Entity;
        await _ctx.Db.SaveChangesAsync();
        Lancar(new(2026, 8, 28), -200m, categoriaId, null, cartaoId: cartao.Id, vencimento: new(2026, 9, 10));
        await _ctx.Db.SaveChangesAsync();

        var setembro = await _svc.GerarAsync(new(2026, 9, 1), new(2026, 9, 30), categoriaId, null);
        var agosto = await _svc.GerarAsync(new(2026, 8, 1), new(2026, 8, 31), categoriaId, null);

        Assert.Equal(-200m, setembro.TotalGeral);
        Assert.Equal(0m, agosto.TotalGeral);
    }

    public void Dispose() => TestDbContext.Cleanup(_ctx.Db, _ctx.File);
}
