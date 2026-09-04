using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class FaturaServiceTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService LancamentoService, FaturaService Service, CartaoCredito Cartao)> SetupAsync()
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
        return (db, file, new LancamentoService(db), new FaturaService(db), cartao);
    }

    private static Categoria Renda(AppDbContext db) => db.Categorias.First(c => c.Nome == "Receita");

    [Fact]
    public async Task ObterLancamentosAsync_RetornaSomenteDoCartaoEMes()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 50m,
                Renda(db).Id, null, null, null, null, null);

            // compra dia 6 >= melhorDia 5 → vencimento mês+2 = out/2026
            var outubro = await service.ObterLancamentosAsync(cartao.Id, 2026, 10);
            var agosto = await service.ObterLancamentosAsync(cartao.Id, 2026, 8);

            Assert.Single(outubro);
            Assert.Empty(agosto);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task SomarAsync_SomaAssinada()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 30.25m,
                Renda(db).Id, null, null, null, null, null);

            // compra dia 6 >= melhorDia 5 → vencimento mês+2 = out/2026
            var total = await service.SomarAsync(cartao.Id, 2026, 10);

            Assert.Equal(-30.25m, total);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task FecharAsync_ComPendentes_LancaComListaENaoPersiste()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 50m,
                Renda(db).Id, null, null, null, null, null);

            // compra dia 6 >= melhorDia 5 → vencimento mês+2 = out/2026
            var ex = await Assert.ThrowsAsync<FaturaComPendentesException>(
                () => service.FecharAsync(cartao.Id, 2026, 10));

            Assert.Single(ex.Pendentes);
            Assert.False(await service.EhFechadaAsync(cartao.Id, 2026, 10));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task FecharAsync_TudoConfirmado_PersisteValorTotalEFechada()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            var despesas = await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6),
                75m, Renda(db).Id, null, null, null, null, null);
            foreach (var d in despesas) await lancamentoService.AlternarConfirmadoAsync(d.Id);

            // compra dia 6 >= melhorDia 5 → vencimento mês+2 = out/2026
            var fatura = await service.FecharAsync(cartao.Id, 2026, 10);

            Assert.True(fatura.Fechada);
            Assert.NotNull(fatura.DataFechamento);
            Assert.Equal(-75m, fatura.ValorTotal);
            Assert.True(await service.EhFechadaAsync(cartao.Id, 2026, 10));
            Assert.NotNull(await service.ObterFechadaAsync(cartao.Id, 2026, 10));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ListarFechadasAsync_RetornaHistoricoOrdenadoDesc()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            for (var mes = 7; mes <= 9; mes++)
            {
                var despesas = await lancamentoService.CriarDespesaCartaoAsync(
                    cartao.Id, new DateOnly(2026, mes - 1, 6), 10m * mes,
                    Renda(db).Id, null, null, null, null, null);
                foreach (var d in despesas) await lancamentoService.AlternarConfirmadoAsync(d.Id);
                await service.FecharAsync(cartao.Id, 2026, mes);
            }

            var historico = await service.ListarFechadasAsync(cartao.Id);

            Assert.Equal(new[] { 9, 8, 7 }, historico.Select(f => f.MesReferencia));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    // ---------- Pagamento de fatura ----------

    private static async Task<Fatura> FecharFaturaDeTesteAsync(
        LancamentoService lancamentoService, FaturaService service, CartaoCredito cartao,
        Categoria renda, int anoCompra, int mesCompra, decimal valorDespesa)
    {
        var compra = new DateOnly(anoCompra, mesCompra, 6);
        var despesas = await lancamentoService.CriarDespesaCartaoAsync(
            cartao.Id, compra, valorDespesa, renda.Id, null, null, null, null, null);
        foreach (var d in despesas) await lancamentoService.AlternarConfirmadoAsync(d.Id);
        var vencimento = CartaoCreditoService.CalcularVencimento(cartao, compra);
        return await service.FecharAsync(cartao.Id, vencimento.Year, vencimento.Month);
    }

    private static Guid ContaUnica(AppDbContext db) => db.Contas.First().Id;

    [Fact]
    public async Task Pagar_FaturaFechada_CriaGrupoComMesmoReferenciaId()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);

            // fatura out/2026 (vencimento 10/10); pagamento em novembro → destino clampado ao mês de referência
            var pernas = await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db),
                new DateOnly(2026, 11, 5), 75m);

            Assert.Equal(2, pernas.Count);
            Assert.NotNull(pernas[0].ReferenciaId);
            Assert.Equal(pernas[0].ReferenciaId, pernas[1].ReferenciaId);

            var origem = pernas.Single(p => p.ContaId != null);
            Assert.Equal(-75m, origem.Valor);
            Assert.Equal(new DateOnly(2026, 11, 5), origem.Data);
            Assert.Null(origem.CartaoCreditoId);

            var destino = pernas.Single(p => p.CartaoCreditoId != null);
            Assert.Equal(75m, destino.Valor);
            Assert.Null(destino.ContaId);
            Assert.Equal(2026, destino.Data.Year);
            Assert.Equal(10, destino.Data.Month);

            Assert.All(pernas, p =>
            {
                Assert.Equal(LancamentoTipo.Transferencia, p.Tipo);
                Assert.True(p.Confirmado);
                Assert.Equal("Financeiro", p.Categoria!.Nome);
                Assert.Equal("Cartão de crédito", p.Subcategoria!.Nome);
            });
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Pagar_FaturaAberta_Lanca()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await lancamentoService.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 6), 50m,
                Renda(db).Id, null, null, null, null, null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.PagarAsync(cartao.Id, 2026, 9, ContaUnica(db), new DateOnly(2026, 10, 5), 50m));

            Assert.Contains("Somente faturas fechadas", ex.Message);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Pagar_ValorInferior_CriaRolloverNaFaturaSeguinte()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);

            // fatura out/2026; rollover cai na fatura seguinte (nov/2026)
            var pernas = await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db),
                new DateOnly(2026, 11, 5), 50m);

            var rollover = pernas.Single(p => p.CartaoCreditoId != null && p.Valor < 0m);
            Assert.Equal(-25m, rollover.Valor);
            Assert.True(rollover.Confirmado);
            Assert.Equal(2026, rollover.Data.Year);
            Assert.Equal(11, rollover.Data.Month);
            Assert.Equal(pernas[0].ReferenciaId, rollover.ReferenciaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Pagar_ValorExato_NaoCriaRollover()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);

            // fatura out/2026; pagamento integral em novembro, sem rollover
            var pernas = await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db),
                new DateOnly(2026, 11, 5), 75m);

            Assert.DoesNotContain(pernas, p => p.CartaoCreditoId != null && p.Valor < 0m);
            Assert.Equal(2, db.Lancamentos.Count(l => l.Tipo == LancamentoTipo.Transferencia));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Pagar_MesesFechados_ReabreEmCascataAPartirDoPiso()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);
            foreach (var mes in new[] { 7, 8, 9, 10 })
                db.MesesFechados.Add(new MesFechado { Ano = 2026, Mes = mes, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            // data em novembro, ref outubro → piso = outubro → reabre out e mantém jul/ago/set
            await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db), new DateOnly(2026, 11, 5), 75m);

            var fechados = db.MesesFechados.ToList().Select(m => m.Mes).OrderBy(m => m).ToList();
            Assert.Equal(new[] { 7, 8, 9 }, fechados);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Pagar_ProximaFaturaFechada_RolloverPulaParaMesAberto()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);
            // fatura seguinte (nov/2026) já fechada → rollover pula para dez/2026
            db.Faturas.Add(new Fatura
            {
                CartaoCreditoId = cartao.Id, AnoReferencia = 2026, MesReferencia = 11,
                ValorTotal = -999m, Fechada = true, DataFechamento = DateTime.Now
            });
            await db.SaveChangesAsync();

            var pernas = await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db),
                new DateOnly(2026, 11, 5), 50m);

            var rollover = pernas.Single(p => p.CartaoCreditoId != null && p.Valor < 0m);
            Assert.Equal(12, rollover.Data.Month);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Pagar_PagamentosSucessivos_GruposIndependentesESomaCorreta()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);

            // fatura out/2026; dois pagamentos parciais sucessivos em novembro
            var grupo1 = await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db),
                new DateOnly(2026, 11, 5), 30m);
            var grupo2 = await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db),
                new DateOnly(2026, 11, 15), 20m);

            Assert.NotEqual(grupo1[0].ReferenciaId, grupo2[0].ReferenciaId);

            var pagamentos = await service.ObterPagamentosAsync(cartao.Id, 2026, 10);
            Assert.Equal(2, pagamentos.Count);
            Assert.Equal(50m, pagamentos.Sum(p => p.ValorPago));

            var rollovers = db.Lancamentos
                .Where(l => l.CartaoCreditoId != null && l.Valor < 0m && l.Tipo == LancamentoTipo.Transferencia)
                .OrderBy(l => l.Data)
                .ToList();
            Assert.Equal(2, rollovers.Count);
            Assert.Equal(-45m, rollovers[0].Valor);
            Assert.Equal(-25m, rollovers[1].Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterPagamentos_RetornaGruposComContaOrigemOrdenados()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            var conta = db.Contas.First();
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);
            // fatura out/2026; pagamentos em 05/11 e 15/11
            await service.PagarAsync(cartao.Id, 2026, 10, conta.Id, new DateOnly(2026, 11, 5), 30m);
            await service.PagarAsync(cartao.Id, 2026, 10, conta.Id, new DateOnly(2026, 11, 15), 20m);

            var pagamentos = await service.ObterPagamentosAsync(cartao.Id, 2026, 10);

            Assert.Equal(2, pagamentos.Count);
            Assert.Equal(new DateOnly(2026, 11, 15), pagamentos[0].DataPagamento);
            Assert.Equal(new DateOnly(2026, 11, 5), pagamentos[1].DataPagamento);
            Assert.All(pagamentos, p => Assert.Equal("Conta", p.ContaOrigem));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Estornar_RemoveTodasAsPernasEReabreMeses()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);
            foreach (var mes in new[] { 10, 11 })
                db.MesesFechados.Add(new MesFechado { Ano = 2026, Mes = mes, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            // fatura out/2026; pagamento em novembro → pernas origem + destino + rollover
            var pernas = await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db),
                new DateOnly(2026, 11, 5), 50m);
            Assert.Equal(3, pernas.Count);

            await service.EstornarAsync(pernas[0].ReferenciaId!.Value);

            Assert.Empty(db.Lancamentos.Where(l => l.Tipo == LancamentoTipo.Transferencia).ToList());
            Assert.Single(db.Lancamentos.ToList());
            Assert.Empty(db.MesesFechados.Where(m => m.Ano == 2026 && m.Mes >= 9).ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterSituacoes_ClassificaPagaParcialENaoPaga()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            // faturas fechadas: ago -100, set -50, out -40 (compra dia 6 >= melhorDia 5 → venc mês+2)
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 6, 100m);
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 7, 50m);
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 40m);
            // ago paga integral; set parcial (rollover cai em nov pois out já tem fatura fechada)
            await service.PagarAsync(cartao.Id, 2026, 8, ContaUnica(db), new DateOnly(2026, 9, 1), 100m);
            await service.PagarAsync(cartao.Id, 2026, 9, ContaUnica(db), new DateOnly(2026, 10, 1), 20m);

            var situacoes = await service.ObterSituacoesAsync(cartao.Id);

            var agosto = situacoes.Single(s => s.MesReferencia == 8);
            var setembro = situacoes.Single(s => s.MesReferencia == 9);
            var outubro = situacoes.Single(s => s.MesReferencia == 10);
            Assert.True(agosto.Paga);
            Assert.False(agosto.Parcial);
            Assert.Equal(100m, agosto.Pago);
            Assert.False(setembro.Paga);
            Assert.True(setembro.Parcial);
            Assert.Equal(20m, setembro.Pago);
            Assert.False(outubro.Paga);
            Assert.False(outubro.Parcial);
            Assert.Equal(0m, outubro.Pago);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Excluir_PernaDePagamento_BloqueadaPelosGuardas()
    {
        var (db, file, lancamentoService, service, cartao) = await SetupAsync();
        try
        {
            await FecharFaturaDeTesteAsync(lancamentoService, service, cartao, Renda(db), 2026, 8, 75m);
            // fatura out/2026; pagamento em novembro
            var pernas = await service.PagarAsync(cartao.Id, 2026, 10, ContaUnica(db),
                new DateOnly(2026, 11, 5), 75m);
            var destino = pernas.Single(p => p.CartaoCreditoId != null);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => lancamentoService.ExcluirAsync(destino.Id));

            Assert.Equal(3, db.Lancamentos.ToList().Count);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
