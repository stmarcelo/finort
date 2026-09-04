using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class LancamentoServiceTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Service, Conta Conta)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var contaService = new ContaService(db);
        var conta = await contaService.CriarAsync("Conta", null, null, null);
        return (db, file, new LancamentoService(db), conta);
    }

    private static Categoria Renda(AppDbContext db) => db.Categorias.First(c => c.Nome == "Receita");
    private static Categoria Financeiro(AppDbContext db) => db.Categorias.First(c => c.Nome == "Financeiro");

    [Fact]
    public async Task CriarReceitaAsync_ValorPositivo()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarReceitaAsync(conta.Id, new DateOnly(2026, 8, 1), 100m,
                Renda(db).Id, null, null);

            Assert.Equal(LancamentoTipo.Receita, lancamento.Tipo);
            Assert.Equal(100m, lancamento.Valor);
            Assert.False(lancamento.Confirmado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarDespesaAsync_ValorNegativo()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 1), 80m,
                Renda(db).Id, null, null);

            Assert.Equal(-80m, lancamento.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarTransferenciaAsync_CriaDuasPernasComReferencia()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var contaService = new ContaService(db);
            var destino = await contaService.CriarAsync("Destino", null, null, null);

            var (origem, destinoLancamento) = await service.CriarTransferenciaAsync(conta.Id, destino.Id, new DateOnly(2026, 8, 1), 250m);

            Assert.Equal(-250m, origem.Valor);
            Assert.Equal(250m, destinoLancamento.Valor);
            Assert.Equal(origem.ReferenciaId, destinoLancamento.ReferenciaId);
            Assert.NotNull(origem.ReferenciaId);
            Assert.Equal(LancamentoTipo.Transferencia, origem.Tipo);
            Assert.Equal(LancamentoTipo.Transferencia, destinoLancamento.Tipo);
            Assert.Equal(Financeiro(db).Id, origem.CategoriaId);
            Assert.Equal("Transferência", (await db.Subcategorias.FindAsync(origem.SubcategoriaId))!.Nome);

            var soma = db.Lancamentos.Sum(l => l.Valor);
            Assert.Equal(0m, soma);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_AtualizaSinal()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 1), 80m, Renda(db).Id, null, null);

            await service.AtualizarReceitaDespesaAsync(lancamento.Id, conta.Id, new DateOnly(2026, 8, 5), 30m, Renda(db).Id, null, null);

            var carregado = await service.ObterAsync(lancamento.Id);
            Assert.NotNull(carregado);
            Assert.Equal(new DateOnly(2026, 8, 5), carregado!.Data);
            Assert.Equal(-30m, carregado.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarTransferenciaAsync_AtualizaPernas()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var contaService = new ContaService(db);
            var destino = await contaService.CriarAsync("Destino", null, null, null);
            var outroDestino = await contaService.CriarAsync("Outro destino", null, null, null);
            var (origem, _) = await service.CriarTransferenciaAsync(conta.Id, destino.Id, new DateOnly(2026, 8, 1), 250m);

            await service.AtualizarTransferenciaAsync(origem.Id, conta.Id, outroDestino.Id, new DateOnly(2026, 8, 10), 500m);

            var pernas = await service.ObterPernasAsync(origem.Id);
            Assert.Equal(2, pernas.Count);
            Assert.Equal(-500m, pernas.Single(p => p.ContaId == conta.Id).Valor);
            Assert.Equal(500m, pernas.Single(p => p.ContaId == outroDestino.Id).Valor);
            Assert.Equal(new DateOnly(2026, 8, 10), pernas[0].Data);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Atualizar_Confirmado_LancaComLista()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 1), 80m, Renda(db).Id, null, null);
            await service.AlternarConfirmadoAsync(lancamento.Id);

            var ex = await Assert.ThrowsAsync<LancamentoConfirmadoException>(
                () => service.AtualizarReceitaDespesaAsync(lancamento.Id, conta.Id, new DateOnly(2026, 8, 2), 30m, Renda(db).Id, null, null));

            Assert.Contains(ex.Confirmados, l => l.Id == lancamento.Id);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ExcluirTransferencia_RemovePernas()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var contaService = new ContaService(db);
            var destino = await contaService.CriarAsync("Destino", null, null, null);
            var (origem, _) = await service.CriarTransferenciaAsync(conta.Id, destino.Id, new DateOnly(2026, 8, 1), 250m);

            await service.ExcluirAsync(origem.Id);

            Assert.Empty(db.Lancamentos.Where(l => l.ReferenciaId == origem.ReferenciaId));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Excluir_Confirmado_LancaComLista()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 1), 80m, Renda(db).Id, null, null);
            await service.AlternarConfirmadoAsync(lancamento.Id);

            var ex = await Assert.ThrowsAsync<LancamentoConfirmadoException>(
                () => service.ExcluirAsync(lancamento.Id));

            Assert.Contains(ex.Confirmados, l => l.Id == lancamento.Id);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ListarAsync_FiltraPorContaTipoMes()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            await service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 1), 10m, Renda(db).Id, null, null);
            await service.CriarReceitaAsync(conta.Id, new DateOnly(2026, 7, 15), 20m, Renda(db).Id, null, null);

            var agosto = await service.ListarAsync(mes: 8, ano: 2026);
            Assert.Single(agosto);

            var despesas = await service.ListarAsync(tipo: LancamentoTipo.Despesa);
            Assert.Single(despesas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarDespesa_ValorZeroOuNegativo_Lanca()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 1), 0m, Renda(db).Id, null, null));
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CriarReceitaAsync(conta.Id, new DateOnly(2026, 8, 1), -1m, Renda(db).Id, null, null));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AlternarConfirmado_Toggle()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarReceitaAsync(conta.Id, new DateOnly(2026, 8, 1), 100m, Renda(db).Id, null, null);

            await service.AlternarConfirmadoAsync(lancamento.Id);
            Assert.True((await service.ObterAsync(lancamento.Id))!.Confirmado);

            await service.AlternarConfirmadoAsync(lancamento.Id);
            Assert.False((await service.ObterAsync(lancamento.Id))!.Confirmado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    // ---------- Guardas de mês fechado ----------

    [Fact]
    public async Task CriarReceita_DataEmMesFechado_Lanca()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            db.MesesFechados.Add(new MesFechado { Ano = hoje.Year, Mes = hoje.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CriarReceitaAsync(conta.Id, hoje.AddDays(1), 10m, Renda(db).Id, null, null));

            Assert.Contains("mês está fechado", ex.Message);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AlternarConfirmado_EmMesFechado_Lanca()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lancamento = await service.CriarReceitaAsync(conta.Id, hoje.AddDays(1), 10m, Renda(db).Id, null, null);

            db.MesesFechados.Add(new MesFechado { Ano = hoje.Year, Mes = hoje.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AlternarConfirmadoAsync(lancamento.Id));

            Assert.False((await service.ObterAsync(lancamento.Id))!.Confirmado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Excluir_EmMesFechado_Lanca()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var lancamento = await service.CriarDespesaAsync(conta.Id, hoje.AddDays(1), 10m, Renda(db).Id, null, null);

            db.MesesFechados.Add(new MesFechado { Ano = hoje.Year, Mes = hoje.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExcluirAsync(lancamento.Id));

            Assert.Single(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarParcelado_ComParcelaEmMesFechado_FalhaSemInserirNada()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            var proximoMes = hoje.AddMonths(1);
            db.MesesFechados.Add(new MesFechado { Ano = proximoMes.Year, Mes = proximoMes.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CriarParceladoAsync(LancamentoTipo.Despesa, conta.Id,
                    hoje.AddDays(1), 120m, 3, Renda(db).Id, null, null));

            Assert.Empty(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
