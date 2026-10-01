using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

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
    public async Task Lancamento_Observacao_PersisteNoBanco()
    {
        var (db, file, _, conta) = await SetupAsync();
        try
        {
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 8, 1),
                Tipo = LancamentoTipo.Receita,
                Valor = 100m,
                ContaId = conta.Id,
                CategoriaId = Renda(db).Id,
                Observacao = "Referente a julho"
            });
            await db.SaveChangesAsync();

            db.ChangeTracker.Clear();
            var lido = await db.Lancamentos.AsNoTracking().SingleAsync();

            Assert.Equal("Referente a julho", lido.Observacao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarReceitaAsync_ComObservacao_Persiste()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarReceitaAsync(conta.Id, new DateOnly(2026, 8, 1), 100m,
                Renda(db).Id, null, null, observacao: "Salario de julho");

            Assert.Equal("Salario de julho", lancamento.Observacao);
            db.ChangeTracker.Clear();
            Assert.Equal("Salario de julho", (await db.Lancamentos.AsNoTracking().SingleAsync()).Observacao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarDespesaAsync_ComObservacao_Persiste()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var lancamento = await service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 1), 80m,
                Renda(db).Id, null, null, observacao: "Mercado do dia 10");

            Assert.Equal("Mercado do dia 10", lancamento.Observacao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarRecorrenteAsync_ComObservacao_PropagaParaTodasAsRepeticoes()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var criados = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 8, 1), 50m, RecorrenciaFrequencia.Mensal, 3,
                db.Categorias.First(c => c.Nome == "Contas de casa").Id, null, null,
                observacao: "Energia eletrica");

            Assert.Equal(3, criados.Count);
            Assert.All(criados, c => Assert.Equal("Energia eletrica", c.Observacao));
            db.ChangeTracker.Clear();
            Assert.Equal(3, (await db.Lancamentos.AsNoTracking().ToListAsync()).Count(l => l.Observacao == "Energia eletrica"));
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
            Assert.True(origem.Confirmado);
            Assert.True(destinoLancamento.Confirmado);

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
    public async Task AtualizarReceitaDespesaAsync_SemAtualizarFuturos_SoAlteraOEditado()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 3, categoria, null, null,
                observacao: "A");
            var grupoId = serie[0].RecorrenciaId!.Value;

            await service.AtualizarReceitaDespesaAsync(serie[0].Id, conta.Id, new DateOnly(2026, 9, 24), 100m,
                categoria, null, null, atualizarFuturos: false, observacao: "B");

            db.ChangeTracker.Clear();
            var daSerie = await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).OrderBy(l => l.Data).ToListAsync();
            Assert.Equal("B", daSerie[0].Observacao);
            Assert.Equal("A", daSerie[1].Observacao);
            Assert.Equal("A", daSerie[2].Observacao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_ComAtualizarFuturos_PropagaObservacao()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 3, categoria, null, null,
                observacao: "A");
            var grupoId = serie[0].RecorrenciaId!.Value;

            await service.AtualizarReceitaDespesaAsync(serie[0].Id, conta.Id, new DateOnly(2026, 9, 24), 100m,
                categoria, null, null, atualizarFuturos: true, observacao: "B");

            db.ChangeTracker.Clear();
            var daSerie = await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).ToListAsync();
            Assert.Equal(3, daSerie.Count);
            Assert.All(daSerie, l => Assert.Equal("B", l.Observacao));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_AtualizarFuturos_ReancoraPelaNovaData()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 3, categoria, null, null);
            var grupoId = serie[0].RecorrenciaId!.Value;

            await service.AtualizarReceitaDespesaAsync(serie[0].Id, conta.Id, new DateOnly(2026, 9, 24), 100m,
                categoria, null, null, atualizarFuturos: true);

            db.ChangeTracker.Clear();
            var datas = (await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).OrderBy(l => l.Data).ToListAsync())
                .Select(l => l.Data).ToList();
            Assert.Equal(new[]
            {
                new DateOnly(2026, 9, 24), new DateOnly(2026, 10, 24), new DateOnly(2026, 11, 24)
            }, datas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_SemAtualizarFuturos_SoAlteraADataDoEditado()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 3, categoria, null, null);
            var grupoId = serie[0].RecorrenciaId!.Value;

            await service.AtualizarReceitaDespesaAsync(serie[0].Id, conta.Id, new DateOnly(2026, 9, 24), 100m,
                categoria, null, null, atualizarFuturos: false);

            db.ChangeTracker.Clear();
            var datas = (await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).OrderBy(l => l.Data).ToListAsync())
                .Select(l => l.Data).ToList();
            Assert.Equal(new[]
            {
                new DateOnly(2026, 9, 24), new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 30)
            }, datas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_FuturoEmMesFechado_NaoAlteraNada()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 3, categoria, null, null);
            var grupoId = serie[0].RecorrenciaId!.Value;
            db.MesesFechados.Add(new MesFechado { ContaId = conta.Id, Ano = 2026, Mes = 10, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AtualizarReceitaDespesaAsync(serie[0].Id, conta.Id, new DateOnly(2026, 9, 24), 100m,
                    categoria, null, null, atualizarFuturos: true));
            Assert.Contains("mês está fechado", ex.Message);

            db.ChangeTracker.Clear();
            var datas = (await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).OrderBy(l => l.Data).ToListAsync())
                .Select(l => l.Data).ToList();
            Assert.Equal(new[]
            {
                new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 30)
            }, datas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_MembroApagado_ReancoraPelaMedianaDosDeltas()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 5, categoria, null, null);
            var grupoId = serie[0].RecorrenciaId!.Value;

            var apagado = await db.Lancamentos
                .FirstAsync(l => l.RecorrenciaId == grupoId && l.Data == new DateOnly(2026, 11, 30));
            db.Lancamentos.Remove(apagado);
            await db.SaveChangesAsync();

            await service.AtualizarReceitaDespesaAsync(serie[0].Id, conta.Id, new DateOnly(2026, 9, 24), 100m,
                categoria, null, null, atualizarFuturos: true);

            db.ChangeTracker.Clear();
            var datas = (await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).OrderBy(l => l.Data).ToListAsync())
                .Select(l => l.Data).ToList();
            Assert.Equal(new[]
            {
                new DateOnly(2026, 9, 24), new DateOnly(2026, 10, 24),
                new DateOnly(2026, 11, 24), new DateOnly(2026, 12, 24)
            }, datas);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_ComAtualizarFuturos_PropagaContaParaFuturos()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var conta2 = await new ContaService(db).CriarAsync("Conta 2", null, null, null);
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 3, categoria, null, null);
            var grupoId = serie[0].RecorrenciaId!.Value;

            await service.AtualizarReceitaDespesaAsync(serie[0].Id, conta2.Id, new DateOnly(2026, 9, 24), 100m,
                categoria, null, null, atualizarFuturos: true);

            db.ChangeTracker.Clear();
            var daSerie = await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).ToListAsync();
            Assert.Equal(3, daSerie.Count);
            Assert.All(daSerie, l => Assert.Equal(conta2.Id, l.ContaId));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_ContaAntigaComMesFechado_NaoBloqueiaFuturosDaContaDestino()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var conta2 = await new ContaService(db).CriarAsync("Conta 2", null, null, null);
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 3, categoria, null, null);
            var grupoId = serie[0].RecorrenciaId!.Value;
            db.MesesFechados.Add(new MesFechado { ContaId = conta.Id, Ano = 2026, Mes = 10, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            await service.AtualizarReceitaDespesaAsync(serie[0].Id, conta2.Id, new DateOnly(2026, 9, 24), 100m,
                categoria, null, null, atualizarFuturos: true);

            db.ChangeTracker.Clear();
            var daSerie = await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).OrderBy(l => l.Data).ToListAsync();
            Assert.Equal(new[]
            {
                new DateOnly(2026, 9, 24), new DateOnly(2026, 10, 24), new DateOnly(2026, 11, 24)
            }, daSerie.Select(l => l.Data).ToList());
            Assert.All(daSerie, l => Assert.Equal(conta2.Id, l.ContaId));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_ContaDestinoComMesFechado_NaoAlteraNada()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var conta2 = await new ContaService(db).CriarAsync("Conta 2", null, null, null);
            var categoria = db.Categorias.First(c => c.Nome == "Contas de casa").Id;
            var serie = await service.CriarRecorrenteAsync(LancamentoTipo.Despesa, conta.Id,
                new DateOnly(2026, 9, 30), 100m, RecorrenciaFrequencia.Mensal, 3, categoria, null, null);
            var grupoId = serie[0].RecorrenciaId!.Value;
            db.MesesFechados.Add(new MesFechado { ContaId = conta2.Id, Ano = 2026, Mes = 10, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.AtualizarReceitaDespesaAsync(serie[0].Id, conta2.Id, new DateOnly(2026, 9, 24), 100m,
                    categoria, null, null, atualizarFuturos: true));
            Assert.Contains("mês está fechado", ex.Message);

            db.ChangeTracker.Clear();
            var daSerie = await db.Lancamentos.AsNoTracking()
                .Where(l => l.RecorrenciaId == grupoId).OrderBy(l => l.Data).ToListAsync();
            Assert.Equal(new[]
            {
                new DateOnly(2026, 9, 30), new DateOnly(2026, 10, 30), new DateOnly(2026, 11, 30)
            }, daSerie.Select(l => l.Data).ToList());
            Assert.All(daSerie, l => Assert.Equal(conta.Id, l.ContaId));
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
            var (origem, destinoLancamento) = await service.CriarTransferenciaAsync(conta.Id, destino.Id, new DateOnly(2026, 8, 1), 250m);

            await service.AlternarConfirmadoAsync(origem.Id);
            await service.AlternarConfirmadoAsync(destinoLancamento.Id);
            await service.AtualizarTransferenciaAsync(origem.Id, conta.Id, outroDestino.Id, new DateOnly(2026, 8, 10), 500m);

            var pernas = await service.ObterPernasAsync(origem.Id);
            Assert.Equal(2, pernas.Count);
            Assert.Equal(-500m, pernas.Single(p => p.ContaId == conta.Id).Valor);
            Assert.Equal(500m, pernas.Single(p => p.ContaId == outroDestino.Id).Valor);
            Assert.Equal(new DateOnly(2026, 8, 10), pernas[0].Data);
            Assert.All(pernas, p => Assert.False(p.Confirmado));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarTransferencia_Confirmada_LancaComLista()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var contaService = new ContaService(db);
            var destino = await contaService.CriarAsync("Destino", null, null, null);
            var (origem, destinoLancamento) = await service.CriarTransferenciaAsync(conta.Id, destino.Id, new DateOnly(2026, 8, 1), 250m);

            var ex = await Assert.ThrowsAsync<LancamentoConfirmadoException>(
                () => service.AtualizarTransferenciaAsync(origem.Id, conta.Id, destino.Id, new DateOnly(2026, 8, 5), 300m));

            Assert.Contains(ex.Confirmados, l => l.Id == origem.Id);
            Assert.Contains(ex.Confirmados, l => l.Id == destinoLancamento.Id);
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
            var (origem, destinoLancamento) = await service.CriarTransferenciaAsync(conta.Id, destino.Id, new DateOnly(2026, 8, 1), 250m);

            await service.AlternarConfirmadoAsync(origem.Id);
            await service.AlternarConfirmadoAsync(destinoLancamento.Id);
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

    [Fact]
    public async Task CriarDespesa_ComConfirmado_CriaConfirmada()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var svc = service;
            var l = await svc.CriarDespesaAsync(conta.Id, new DateOnly(2026, 9, 10), 100m, Renda(db).Id, null, null, null, confirmado: true);
            Assert.True(l.Confirmado);
            Assert.Equal(-100m, l.Valor);
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
            db.MesesFechados.Add(new MesFechado { ContaId = conta.Id, Ano = hoje.Year, Mes = hoje.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CriarReceitaAsync(conta.Id, new DateOnly(hoje.Year, hoje.Month, 1), 10m, Renda(db).Id, null, null));

            Assert.Contains("mês está fechado", ex.Message);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarTransferencia_DestinoEmMesFechado_Lanca()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var contaService = new ContaService(db);
            var destino = await contaService.CriarAsync("Destino", null, null, null);
            var hoje = DateOnly.FromDateTime(DateTime.Today);
            db.MesesFechados.Add(new MesFechado { ContaId = destino.Id, Ano = hoje.Year, Mes = hoje.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CriarTransferenciaAsync(conta.Id, destino.Id, hoje, 100m));

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
            var lancamento = await service.CriarReceitaAsync(conta.Id, new DateOnly(hoje.Year, hoje.Month, 1), 10m, Renda(db).Id, null, null);

            db.MesesFechados.Add(new MesFechado { ContaId = conta.Id, Ano = hoje.Year, Mes = hoje.Month, DataFechamento = DateTime.Now });
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
            var lancamento = await service.CriarDespesaAsync(conta.Id, new DateOnly(hoje.Year, hoje.Month, 1), 10m, Renda(db).Id, null, null);

            db.MesesFechados.Add(new MesFechado { ContaId = conta.Id, Ano = hoje.Year, Mes = hoje.Month, DataFechamento = DateTime.Now });
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
            db.MesesFechados.Add(new MesFechado { ContaId = conta.Id, Ano = proximoMes.Year, Mes = proximoMes.Month, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CriarParceladoAsync(LancamentoTipo.Despesa, conta.Id,
                    hoje.AddDays(1), 120m, 3, Renda(db).Id, null, null));

            Assert.Empty(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
