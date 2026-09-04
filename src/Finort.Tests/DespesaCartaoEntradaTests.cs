using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class DespesaCartaoEntradaTests
{
    private static async Task<(AppDbContext Db, string File, LancamentoService Service, CartaoCredito Cartao)>
        SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var cartao = new CartaoCredito
        {
            Banco = "Teste", Ultimos4Digitos = "9999",
            MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 5000m, Ativo = true
        };
        db.CartoesCredito.Add(cartao);
        db.SaveChanges();
        return (db, file, new LancamentoService(db), cartao);
    }

    [Fact]
    public async Task Entrada_GravaValorPositivo()
    {
        var (db, file, service, cartao) = await SetupAsync();
        try
        {
            var criados = await service.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 1), 75m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 8, 10), ehEntrada: true);

            Assert.All(criados, l => Assert.True(l.Valor > 0));
            Assert.Equal(75m, criados[0].Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Entrada_RejeitaParcelasEReembolso()
    {
        var (db, file, service, cartao) = await SetupAsync();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(() =>
                service.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 1), 75m,
                    db.Categorias.First().Id, null, null,
                    parcelas: 2, reembolsoPessoaId: null, reembolsoVencimento: null,
                    vencimentoExato: new DateOnly(2026, 8, 10), ehEntrada: true));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Fluxo_EntradaReduzDespesasETotalDoCartao()
    {
        var (db, file, service, cartao) = await SetupAsync();
        try
        {
            await service.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 1), 200m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 8, 10));
            await service.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 2), 50m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 8, 11), ehEntrada: true);

            var fluxo = await new FluxoService(db).ObterCardAsync(2026, 8);
            Assert.Equal(150m, fluxo.TotalDespesas);
            Assert.Equal(150m, fluxo.TotaisPorCartao.Single(t => t.CartaoId == cartao.Id).Total);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtualizarValor_PreservaSinalDeEntrada()
    {
        var (db, file, service, cartao) = await SetupAsync();
        try
        {
            var entrada = (await service.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 8, 1), 75m,
                db.Categorias.First().Id, null, null,
                parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
                vencimentoExato: new DateOnly(2026, 8, 10), ehEntrada: true))[0];

            await service.AtualizarValorAsync(entrada.Id, 90m);

            var salvo = await service.ObterAsync(entrada.Id);
            Assert.Equal(90m, salvo!.Valor);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
