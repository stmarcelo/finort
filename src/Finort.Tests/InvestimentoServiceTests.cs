using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class InvestimentoServiceTests
{
    private static async Task<(AppDbContext Db, string File, Conta Conta, InvestimentoService Service)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Banco" };
        db.Contas.Add(conta);
        await db.SaveChangesAsync();
        return (db, file, conta, new InvestimentoService(db, new LancamentoService(db)));
    }

    [Fact]
    public async Task Criar_ComDadosValidos_Persiste()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("Tesouro Selic", TipoInvestimento.Reserva,
                conta.Id, null, "Reserva de emergência", null, null);

            Assert.NotEqual(Guid.Empty, investimento.Id);
            Assert.Equal("Tesouro Selic", investimento.Nome);
            Assert.Equal(TipoInvestimento.Reserva, investimento.Tipo);
            Assert.Equal(conta.Id, investimento.ContaVinculadaId);
            Assert.Equal(0m, investimento.ValorCotaAtual);
            Assert.Null(investimento.DataCotacao);
            Assert.True(investimento.Ativo);
            Assert.Single(db.Investimentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Criar_SemNome_Lanca()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CriarAsync(" ", TipoInvestimento.Acao, conta.Id, null, null, null, null));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarCotacao_GravaValorEData()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("BTC", TipoInvestimento.Criptomoeda,
                conta.Id, null, null, null, null);
            var data = new DateTime(2026, 8, 24, 10, 30, 0);

            await service.AtualizarCotacaoAsync(investimento.Id, 350000.12345678m, data);

            var salvo = await db.Investimentos.AsNoTracking().SingleAsync(i => i.Id == investimento.Id);
            Assert.Equal(350000.12345678m, salvo.ValorCotaAtual);
            Assert.Equal(data, salvo.DataCotacao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Excluir_RemoveProventosEAudita()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("MXRF11", TipoInvestimento.Fii,
                conta.Id, null, null, 10m, DateTime.Today);
            db.InvestimentosProventos.Add(new InvestimentoProvento
            {
                InvestimentoId = investimento.Id,
                Data = new DateOnly(2026, 8, 5), Valor = 0.50m, Tipo = ProventoTipo.Dividendo
            });
            await db.SaveChangesAsync();

            await service.ExcluirAsync(investimento.Id);

            Assert.Empty(db.Investimentos.ToList());
            Assert.Empty(db.InvestimentosProventos.ToList());
            var auditoria = Assert.Single(db.AuditoriasExclusaoInvestimento.ToList());
            Assert.Equal("MXRF11", auditoria.NomeInvestimento);
            Assert.Equal(TipoInvestimento.Fii, auditoria.Tipo);
            Assert.Equal(10m, auditoria.ValorCotaAtual);
            Assert.True(auditoria.DataExclusao <= DateTime.Now);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Excluir_SemProventos_AuditaIgualmente()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("Dolar", TipoInvestimento.Dolar,
                conta.Id, null, null, null, null);

            await service.ExcluirAsync(investimento.Id);

            Assert.Empty(db.Investimentos.ToList());
            Assert.Single(db.AuditoriasExclusaoInvestimento.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Excluir_Inexistente_Lanca()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExcluirAsync(Guid.NewGuid()));
            Assert.Contains("não encontrado", ex.Message);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    // ---------- Proventos ----------

    [Fact]
    public async Task RegistrarProvento_Dividendo_CriaReceitaNaVinculada()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("ITSA4", TipoInvestimento.Acao,
                conta.Id, null, null, null, null);

            await service.RegistrarProventoAsync(investimento.Id,
                new DateOnly(2026, 8, 10), 25.5m, ProventoTipo.Dividendo);

            var receita = Assert.Single(db.Lancamentos
                .Include(l => l.Categoria)
                .Include(l => l.Subcategoria)
                .ToList());
            Assert.Equal(LancamentoTipo.Receita, receita.Tipo);
            Assert.Equal(25.5m, receita.Valor);
            Assert.Equal(conta.Id, receita.ContaId);
            Assert.Equal("Investimento", receita.Categoria!.Nome);
            Assert.Equal("Dividendos / Rendimentos", receita.Subcategoria!.Nome);

            var provento = Assert.Single(db.InvestimentosProventos.ToList());
            Assert.Equal(investimento.Id, provento.InvestimentoId);
            Assert.Equal(new DateOnly(2026, 8, 10), provento.Data);
            Assert.Equal(25.5m, provento.Valor);
            Assert.Equal(ProventoTipo.Dividendo, provento.Tipo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarProvento_Rendimento_NaoGeraLancamento()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("Tesouro Selic", TipoInvestimento.Reserva,
                conta.Id, null, null, null, null);

            await service.RegistrarProventoAsync(investimento.Id,
                new DateOnly(2026, 8, 15), 100m, ProventoTipo.Rendimento);

            Assert.Empty(db.Lancamentos.ToList());
            Assert.Single(db.InvestimentosProventos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarProvento_ValorInvalido_Lanca()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("BTC", TipoInvestimento.Criptomoeda,
                conta.Id, null, null, null, null);

            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => service.RegistrarProventoAsync(investimento.Id,
                    new DateOnly(2026, 8, 15), 0m, ProventoTipo.Rendimento));

            Assert.Contains("maior que zero", ex.Message);
            Assert.Empty(db.InvestimentosProventos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ListarProventos_RetornaOrdenadoDesc()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("HGLG11", TipoInvestimento.Fii,
                conta.Id, null, null, null, null);
            await service.RegistrarProventoAsync(investimento.Id,
                new DateOnly(2026, 7, 10), 10m, ProventoTipo.Dividendo);
            await service.RegistrarProventoAsync(investimento.Id,
                new DateOnly(2026, 8, 10), 12m, ProventoTipo.Dividendo);

            var proventos = await service.ListarProventosAsync(investimento.Id);

            Assert.Equal(2, proventos.Count);
            Assert.Equal(new DateOnly(2026, 8, 10), proventos[0].Data);
            Assert.Equal(new DateOnly(2026, 7, 10), proventos[1].Data);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ObterSaldoReserva_SomaSomenteRendimentos()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var reserva = await service.CriarAsync("Reserva", TipoInvestimento.Reserva,
                conta.Id, null, null, null, null);
            await service.RegistrarProventoAsync(reserva.Id,
                new DateOnly(2026, 7, 1), 80m, ProventoTipo.Rendimento);
            await service.RegistrarProventoAsync(reserva.Id,
                new DateOnly(2026, 8, 1), 20m, ProventoTipo.Rendimento);
            // dividendo de outro investimento NÃO entra no saldo de reserva:
            var fii = await service.CriarAsync("FII", TipoInvestimento.Fii,
                conta.Id, null, null, null, null);
            await service.RegistrarProventoAsync(fii.Id,
                new DateOnly(2026, 8, 1), 999m, ProventoTipo.Dividendo);

            var cards = await service.ListarParaCardsAsync();

            Assert.Equal(100m, cards.Single(c => c.Investimento.Id == reserva.Id).SaldoReserva);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    // ---------- Movimentações ----------

    private static async Task<(AppDbContext Db, string File, Conta Conta, InvestimentoService Service,
        Investimento Ativo, Investimento Reserva)> SetupMovimentosAsync()
    {
        var (db, file, conta, service) = await SetupAsync();
        var ativo = await service.CriarAsync("ITSA4", TipoInvestimento.Acao,
            conta.Id, null, null, null, null);
        var reserva = await service.CriarAsync("Reserva", TipoInvestimento.Reserva,
            conta.Id, null, null, null, null);
        return (db, file, conta, service, ativo, reserva);
    }

    [Fact]
    public async Task RegistrarMovimento_Compra_CriaDespesaEMovimento()
    {
        var (db, file, conta, service, ativo, _) = await SetupMovimentosAsync();
        try
        {
            var movimento = await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 10m, 5m, null);

            var despesa = Assert.Single(db.Lancamentos
                .Include(l => l.Categoria)
                .Include(l => l.Subcategoria)
                .ToList());
            Assert.Equal(LancamentoTipo.Despesa, despesa.Tipo);
            Assert.Equal(-50m, despesa.Valor);
            Assert.Equal(conta.Id, despesa.ContaId);
            Assert.Equal("Investimento", despesa.Categoria!.Nome);
            Assert.Equal("Compra/Aporte", despesa.Subcategoria!.Nome);

            Assert.Equal(despesa.Id, movimento.LancamentoId);
            Assert.Equal(10m, movimento.Quantidade);
            Assert.Equal(5m, movimento.ValorPorCota);
            Assert.Equal(50m, movimento.Valor);
            Assert.Equal(MovimentoTipo.Compra, movimento.Tipo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarMovimento_Venda_CriaReceita()
    {
        var (db, file, conta, service, ativo, _) = await SetupMovimentosAsync();
        try
        {
            await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 10m, 5m, null);

            var movimento = await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 20), MovimentoTipo.Venda, 4m, 6m, null);

            var receita = db.Lancamentos
                .Include(l => l.Categoria)
                .Include(l => l.Subcategoria)
                .OrderBy(l => l.Data).ToList()[1];
            Assert.Equal(LancamentoTipo.Receita, receita.Tipo);
            Assert.Equal(24m, receita.Valor);
            Assert.Equal("Venda / Resgate", receita.Subcategoria!.Nome);
            Assert.Equal(24m, movimento.Valor);
            Assert.Equal(4m, movimento.Quantidade);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarMovimento_AporteReserva_GeraDespesaSemQuantidade()
    {
        var (db, file, conta, service, _, reserva) = await SetupMovimentosAsync();
        try
        {
            var movimento = await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 1000m);

            var despesa = Assert.Single(db.Lancamentos.ToList());
            Assert.Equal(-1000m, despesa.Valor);
            Assert.Null(movimento.Quantidade);
            Assert.Null(movimento.ValorPorCota);
            Assert.Equal(1000m, movimento.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarMovimento_ResgateReserva_GeraReceita()
    {
        var (db, file, conta, service, _, reserva) = await SetupMovimentosAsync();
        try
        {
            await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 1000m);

            var movimento = await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 20), MovimentoTipo.Resgate, null, null, 300m);

            var receita = db.Lancamentos.OrderBy(l => l.Data).ToList()[1];
            Assert.Equal(300m, receita.Valor);
            Assert.Equal(MovimentoTipo.Resgate, movimento.Tipo);
            Assert.Equal(300m, movimento.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarMovimento_VendaAcimaDaPosicao_Lanca()
    {
        var (db, file, conta, service, ativo, _) = await SetupMovimentosAsync();
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RegistrarMovimentoAsync(ativo.Id,
                    new DateOnly(2026, 8, 5), MovimentoTipo.Venda, 1m, 5m, null));

            Assert.Contains("Quantidade insuficiente", ex.Message);
            Assert.Empty(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarMovimento_ResgateAcimaDoSaldo_Lanca()
    {
        var (db, file, conta, service, _, reserva) = await SetupMovimentosAsync();
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.RegistrarMovimentoAsync(reserva.Id,
                    new DateOnly(2026, 8, 5), MovimentoTipo.Resgate, null, null, 1m));

            Assert.Contains("Saldo insuficiente", ex.Message);
            Assert.Empty(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarMovimento_Compra_AtualizaCotacaoEData()
    {
        var (db, file, conta, service, ativo, _) = await SetupMovimentosAsync();
        try
        {
            await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 10m, 5m, null);

            var salvo = await db.Investimentos.AsNoTracking().SingleAsync(i => i.Id == ativo.Id);
            Assert.Equal(5m, salvo.ValorCotaAtual);
            Assert.Equal(new DateTime(2026, 8, 5), salvo.DataCotacao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task RegistrarMovimento_QuantidadeInvalida_Lanca()
    {
        var (db, file, conta, service, ativo, _) = await SetupMovimentosAsync();
        try
        {
            var ex = await Assert.ThrowsAsync<ArgumentException>(
                () => service.RegistrarMovimentoAsync(ativo.Id,
                    new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 0m, 5m, null));

            Assert.Contains("quantidade", ex.Message);
            Assert.Empty(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task EstornarMovimento_RemoveLancamentoEMovimentoEReabreMeses()
    {
        var (db, file, conta, service, ativo, _) = await SetupMovimentosAsync();
        try
        {
            var movimento = await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 20), MovimentoTipo.Compra, 10m, 5m, null);
            foreach (var mes in new[] { 8, 9 })
                db.MesesFechados.Add(new MesFechado { Ano = 2026, Mes = mes, DataFechamento = DateTime.Now });
            await db.SaveChangesAsync();

            await service.EstornarMovimentoAsync(movimento.Id);

            Assert.Empty(db.InvestimentosMovimentos.ToList());
            Assert.Empty(db.Lancamentos.ToList());
            Assert.Empty(db.MesesFechados.Where(mf => mf.Ano == 2026 && mf.Mes >= 8).ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ListarParaCards_QuantidadeTotal_SomaComprasMenosVendas()
    {
        var (db, file, conta, service, ativo, reserva) = await SetupMovimentosAsync();
        try
        {
            await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 10m, 5m, null);
            await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 10), MovimentoTipo.Compra, 5m, 6m, null);
            await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 15), MovimentoTipo.Venda, 3m, 7m, null);
            // movimentos de reserva não alteram quantidade do ativo:
            await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 15), MovimentoTipo.Aporte, null, null, 500m);

            var cards = await service.ListarParaCardsAsync();

            Assert.Equal(12m, cards.Single(c => c.Investimento.Id == ativo.Id).QuantidadeTotal);
            Assert.Equal(0m, cards.Single(c => c.Investimento.Id == reserva.Id).QuantidadeTotal);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ListarParaCards_SaldoReserva_IncluiAportesRendimentosMenosResgates()
    {
        var (db, file, conta, service, _, reserva) = await SetupMovimentosAsync();
        try
        {
            await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 1000m);
            await service.RegistrarProventoAsync(reserva.Id,
                new DateOnly(2026, 8, 10), 50m, ProventoTipo.Rendimento);
            await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 15), MovimentoTipo.Resgate, null, null, 200m);

            var cards = await service.ListarParaCardsAsync();

            Assert.Equal(850m, cards.Single(c => c.Investimento.Id == reserva.Id).SaldoReserva);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ListarMovimentos_RetornaOrdenadoDesc()
    {
        var (db, file, conta, service, ativo, _) = await SetupMovimentosAsync();
        try
        {
            await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 10m, 5m, null);
            await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 20), MovimentoTipo.Venda, 2m, 6m, null);

            var movimentos = await service.ListarMovimentosAsync(ativo.Id);

            Assert.Equal(2, movimentos.Count);
            Assert.Equal(MovimentoTipo.Venda, movimentos[0].Tipo);
            Assert.Equal(MovimentoTipo.Compra, movimentos[1].Tipo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task EstornarMovimento_Inexistente_Lanca()
    {
        var (db, file, conta, service, _, _) = await SetupMovimentosAsync();
        try
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.EstornarMovimentoAsync(Guid.NewGuid()));

            Assert.Contains("não encontrado", ex.Message);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    // ---------- lancarNaConta opcional ----------

    [Fact]
    public async Task Aporte_SemLancarNaConta_NaoCriaDespesa()
    {
        var (db, file, conta, service, _, reserva) = await SetupMovimentosAsync();
        try
        {
            var movimento = await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 500m,
                lancarNaConta: false);

            Assert.Null(movimento.LancamentoId);
            Assert.False(db.Lancamentos.Any(l => l.Categoria!.Nome == "Investimento"));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Aporte_Padrao_CriaDespesaComoAntes()
    {
        var (db, file, conta, service, _, reserva) = await SetupMovimentosAsync();
        try
        {
            var movimento = await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 500m);

            Assert.NotNull(movimento.LancamentoId);
            Assert.Equal(db.Lancamentos.Single().Id, movimento.LancamentoId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Estorno_MovimentoSemLancamento_NaoFalha()
    {
        var (db, file, conta, service, _, reserva) = await SetupMovimentosAsync();
        try
        {
            var movimento = await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 500m,
                lancarNaConta: false);

            await service.EstornarMovimentoAsync(movimento.Id);

            Assert.Null(db.InvestimentosMovimentos.FirstOrDefault(m => m.Id == movimento.Id));
            Assert.Empty(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Excluir_RemoveMovimentosELancamentosBancarios()
    {
        var (db, file, conta, service, ativo, reserva) = await SetupMovimentosAsync();
        try
        {
            await service.RegistrarMovimentoAsync(ativo.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Compra, 10m, 5m, null);
            await service.RegistrarMovimentoAsync(reserva.Id,
                new DateOnly(2026, 8, 6), MovimentoTipo.Aporte, null, null, 800m);

            await service.ExcluirAsync(ativo.Id);

            Assert.Empty(db.InvestimentosMovimentos.Where(m => m.InvestimentoId == ativo.Id).ToList());
            // lançamento bancário da COMPRA removido junto; o APORTE da reserva permanece:
            Assert.Single(db.Lancamentos.ToList());
            Assert.Single(db.AuditoriasExclusaoInvestimento.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Excluir_MovimentosComESemLancamento_RemoveTodosEOVinculado()
    {
        var (db, file, conta, service) = await SetupAsync();
        try
        {
            var investimento = await service.CriarAsync("Reserva", TipoInvestimento.Reserva,
                conta.Id, null, null, null, null);
            var comLancamento = await service.RegistrarMovimentoAsync(investimento.Id,
                new DateOnly(2026, 8, 5), MovimentoTipo.Aporte, null, null, 500m);
            var semLancamento = await service.RegistrarMovimentoAsync(investimento.Id,
                new DateOnly(2026, 8, 6), MovimentoTipo.Aporte, null, null, 300m,
                lancarNaConta: false);

            // sanity: um movimento gerou lançamento bancário, outro não:
            Assert.NotNull(comLancamento.LancamentoId);
            Assert.Null(semLancamento.LancamentoId);

            await service.ExcluirAsync(investimento.Id);

            Assert.Empty(db.InvestimentosMovimentos.Where(m => m.InvestimentoId == investimento.Id).ToList());
            Assert.Empty(db.Lancamentos.ToList());
            Assert.Single(db.AuditoriasExclusaoInvestimento.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
