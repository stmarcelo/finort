using System.Globalization;
using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class LancamentoServiceFase4aTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Service, Conta Conta, CartaoCredito Cartao)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartao = new CartaoCredito
        {
            Banco = "Nubank",
            Ultimos4Digitos = "1234",
            MelhorDiaCompra = 5,
            DiaVencimento = 10,
            Limite = 5000m,
            Ativo = true,
            ContaId = conta.Id
        };
        db.CartoesCredito.Add(cartao);
        await db.SaveChangesAsync();
        return (db, file, new LancamentoService(db), conta, cartao);
    }

    private static Categoria Renda(AppDbContext db) => db.Categorias.First(c => c.Nome == "Receita");

    [Fact]
    public async Task CriarDespesaCartaoAsync_Simples_GravaNegativaComVencimentoCalculado()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 120m, Renda(db).Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null);

            var despesa = Assert.Single(despesas);
            Assert.Equal(new DateOnly(2026, 8, 6), despesa.Data);
            // compra dia 6 >= melhorDia 5 → vencimento calculado mês+2 = 10/10/2026
            Assert.Equal(new DateOnly(2026, 10, 10), despesa.DataVencimentoCartao);
            Assert.Equal(-120m, despesa.Valor);
            Assert.Equal(cartao.Id, despesa.CartaoCreditoId);
            Assert.Null(despesa.ParcelamentoId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarDespesaCartaoAsync_Parcelado_CriaParcelasQueSomamTotal()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 100m, Renda(db).Id, null, null,
                parcelas: 3, reembolsoPessoaId: null, reembolsoVencimento: null);

            Assert.Equal(3, despesas.Count);
            Assert.All(despesas, d => Assert.NotNull(d.ParcelamentoId));
            Assert.Equal(despesas[0].ParcelamentoId, despesas[1].ParcelamentoId);
            Assert.Equal(1, despesas[0].ParcelaAtual);
            Assert.Equal(3, despesas[2].ParcelaAtual);
            Assert.All(despesas, d => Assert.Equal(3, d.TotalParcelas));
            Assert.Equal(-100m, despesas.Sum(d => d.Valor));
            Assert.Equal(new[] { "33.33", "33.33", "33.34" },
                despesas.Select(d => Math.Abs(d.Valor).ToString("F2", CultureInfo.InvariantCulture)));
            // compra dia 6 >= melhorDia 5 → vencimento base mês+2 = out/2026; parcelas +1 mês cada
            Assert.Equal(new DateOnly(2026, 10, 10), despesas[0].DataVencimentoCartao);
            Assert.Equal(new DateOnly(2026, 12, 10), despesas[2].DataVencimentoCartao);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarDespesaCartaoAsync_ComReembolso_CriaReceitaVinculadaPorParcela()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 60m, Renda(db).Id, null, null,
                parcelas: 2, reembolsoPessoaId: pessoa.Id, reembolsoVencimento: null);

            foreach (var despesa in despesas)
            {
                Assert.NotNull(despesa.ReembolsoId);
                var reembolso = await db.Lancamentos.FindAsync(despesa.ReembolsoId!.Value);
                Assert.NotNull(reembolso);
                Assert.Equal(LancamentoTipo.Receita, reembolso!.Tipo);
                Assert.Equal(30m, reembolso.Valor);
                Assert.Equal(pessoa.Id, reembolso.PessoaId);
                // reembolso vence um dia antes do vencimento calculado da parcela
                Assert.Equal(despesa.DataVencimentoCartao!.Value.AddDays(-1), reembolso.Data);
            }
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarDespesaCartaoAsync_VencimentoExato_UsaDataInformada()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 20), 50m, Renda(db).Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 9, 15));

            var despesa = Assert.Single(despesas);
            Assert.Equal(new DateOnly(2026, 8, 20), despesa.Data);
            // vencimentoExato informado prevalece sobre o vencimento calculado pelo ciclo
            Assert.Equal(new DateOnly(2026, 9, 15), despesa.DataVencimentoCartao);
            Assert.Equal(-50m, despesa.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarParceladoAsync_Conta_AplicaSinalPorTipoEEspacaMeses()
    {
        var (db, file, service, conta, _) = await SetupAsync();
        try
        {
            var parcelas = await service.CriarParceladoAsync(
                LancamentoTipo.Despesa, conta.Id, new DateOnly(2026, 8, 1), 250m, 2, Renda(db).Id, null, null);

            Assert.Equal(2, parcelas.Count);
            Assert.Equal(-125m, parcelas[0].Valor);
            Assert.Equal(new DateOnly(2026, 9, 1), parcelas[1].Data);
            Assert.NotNull(parcelas[0].ParcelamentoId);

            var receitas = await service.CriarParceladoAsync(
                LancamentoTipo.Receita, conta.Id, new DateOnly(2026, 8, 1), 100m, 2, Renda(db).Id, null, null);

            Assert.All(receitas, r => Assert.True(r.Valor > 0));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarRecorrenteAsync_EspaciaDatasPelaFrequencia()
    {
        var (db, file, service, conta, _) = await SetupAsync();
        try
        {
            var recorrencias = await service.CriarRecorrenteAsync(
                LancamentoTipo.Despesa, conta.Id, new DateOnly(2026, 8, 15), 90m,
                RecorrenciaFrequencia.Trimestral, 3, Renda(db).Id, null, null);

            Assert.Equal(3, recorrencias.Count);
            Assert.Equal(recorrencias[0].RecorrenciaId, recorrencias[2].RecorrenciaId);
            Assert.Equal(new DateOnly(2026, 11, 15), recorrencias[1].Data);
            Assert.Equal(new DateOnly(2027, 2, 15), recorrencias[2].Data);
            Assert.All(recorrencias, r => Assert.Equal(-90m, r.Valor));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarValorAsync_AplicaSinalESincronizaReembolso()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 60m, Renda(db).Id, null, null,
                parcelas: null, reembolsoPessoaId: pessoa.Id, reembolsoVencimento: null);
            var despesa = despesas.Single();

            await service.AtualizarValorAsync(despesa.Id, 75m);

            var atualizada = await service.ObterAsync(despesa.Id);
            Assert.Equal(-75m, atualizada!.Valor);
            var reembolso = await db.Lancamentos.FindAsync(despesa.ReembolsoId!.Value);
            Assert.Equal(75m, reembolso!.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ExcluirAsync_DespesaComReembolso_ExcluiReembolsoNaoConfirmado()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 60m, Renda(db).Id, null, null,
                parcelas: null, reembolsoPessoaId: pessoa.Id, reembolsoVencimento: null);

            await service.ExcluirAsync(despesas[0].Id);

            Assert.Empty(db.Lancamentos.ToList());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ExcluirGrupoAsync_Parcelamento_RemoveSomenteNaoConfirmadas()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 100m, Renda(db).Id, null, null,
                parcelas: 3, reembolsoPessoaId: null, reembolsoVencimento: null);
            await service.AlternarConfirmadoAsync(despesas[0].Id);

            await service.ExcluirGrupoAsync(despesas[0].ParcelamentoId!.Value, ehParcelamento: true);

            var restantes = db.Lancamentos.Where(l => l.CartaoCreditoId == cartao.Id).ToList();
            var restante = Assert.Single(restantes);
            Assert.Equal(despesas[0].Id, restante.Id);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Mutacoes_FaturaFechada_LancaInvalidOperationException()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var faturaService = new FaturaService(db);
            var despesas = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 50m, Renda(db).Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null);
            var despesa = despesas.Single();
            await service.AlternarConfirmadoAsync(despesa.Id);
            // compra 06/08 (dia >= melhorDia 5) → vencimento 10/10/2026 → fatura out/2026
            await faturaService.FecharAsync(cartao.Id, 2026, 10);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AtualizarValorAsync(despesa.Id, 99m));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.ExcluirAsync(despesa.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AlternarConfirmadoAsync(despesa.Id));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task CriarDespesaCartaoAsync_FaturaFechada_LancaENaoPersiste()
    {
        var (db, file, service, _, cartao) = await SetupAsync();
        try
        {
            var faturaService = new FaturaService(db);
            var existentes = await service.CriarDespesaCartaoAsync(
                cartao.Id, new DateOnly(2026, 8, 6), 50m, Renda(db).Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null);
            await service.AlternarConfirmadoAsync(existentes[0].Id);
            // compra 06/08 (dia >= melhorDia 5) → vencimento 10/10/2026 → fatura out/2026
            await faturaService.FecharAsync(cartao.Id, 2026, 10);

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.CriarDespesaCartaoAsync(
                    cartao.Id, new DateOnly(2026, 8, 6), 50m, Renda(db).Id, null, null,
                    parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null));

            Assert.Equal(1, db.Lancamentos.Count());
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarReceitaDespesaAsync_DespesaComReembolso_PropagaParaReembolso()
    {
        var (db, file, service, conta, _) = await SetupAsync();
        try
        {
            var pessoa = new Pessoa { Nome = "Amigo" };
            db.Pessoas.Add(pessoa);
            await db.SaveChangesAsync();

            var despesas = await service.CriarParceladoAsync(
                LancamentoTipo.Despesa, conta.Id, new DateOnly(2026, 8, 1), 100m, 1, Renda(db).Id, null, null);
            var despesa = despesas.Single();
            var reembolso = new Lancamento
            {
                Data = new DateOnly(2026, 8, 20),
                Tipo = LancamentoTipo.Receita,
                Valor = 100m,
                ContaId = conta.Id,
                CategoriaId = Renda(db).Id,
                PessoaId = pessoa.Id
            };
            db.Lancamentos.Add(reembolso);
            await db.SaveChangesAsync();
            despesa.ReembolsoId = reembolso.Id;
            await db.SaveChangesAsync();

            await service.AtualizarReceitaDespesaAsync(
                despesa.Id, conta.Id, new DateOnly(2026, 8, 5), 150m, Renda(db).Id, null, pessoa.Id);

            var reembolsoAtualizado = await db.Lancamentos.FindAsync(reembolso.Id);
            Assert.Equal(150m, reembolsoAtualizado!.Valor);
            Assert.Equal(new DateOnly(2026, 8, 5), reembolsoAtualizado.Data);
            Assert.Equal(pessoa.Id, reembolsoAtualizado.PessoaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
