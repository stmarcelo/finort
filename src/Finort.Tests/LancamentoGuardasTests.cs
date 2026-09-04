using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class LancamentoGuardasTests
{
    private static async Task<(AppDbContext Db, string File)> SetupDbAsync()
        => TestDbContext.Create();

    private static CartaoCredito NovoCartao() => new()
    {
        Banco = "Teste", Ultimos4Digitos = "1234",
        MelhorDiaCompra = 5, DiaVencimento = 10, Limite = 10000m, Ativo = true
    };

    [Fact]
    public async Task AlternarConfirmado_SemContaOuCartao_Lanca()
    {
        var (db, file) = await SetupDbAsync();
        try
        {
            var conta = await new ContaService(db).CriarAsync("Conta", null, null, null);
            var service = new LancamentoService(db);

            // Lançamento "órfão": cria direto via db para não ter conta nem cartão.
            var orfao = new Lancamento
            {
                Data = new DateOnly(2026, 8, 1), Tipo = LancamentoTipo.Despesa,
                Valor = -50m, CategoriaId = db.Categorias.First().Id
            };
            db.Lancamentos.Add(orfao);
            db.SaveChanges();

            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AlternarConfirmadoAsync(orfao.Id));

            // Com conta, confirma normalmente.
            var normal = await service.CriarDespesaAsync(conta.Id, new DateOnly(2026, 8, 2), 30m,
                db.Categorias.First().Id, null, null);
            await service.AlternarConfirmadoAsync(normal.Id);
            Assert.True((await service.ObterAsync(normal.Id))!.Confirmado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    private static async Task<(Lancamento Perna, Guid ReferenciaId, AppDbContext Db, string File)>
        CriarPagamentoAsync()
    {
        var (db, file) = await SetupDbAsync();
        var cartao = NovoCartao();
        db.CartoesCredito.Add(cartao);
        db.SaveChanges();

        // Fecha a fatura do mês com uma compra confirmada.
        var lancamentos = new LancamentoService(db);
        await lancamentos.CriarDespesaCartaoAsync(cartao.Id, new DateOnly(2026, 7, 1), 100m,
            db.Categorias.First().Id, null, null,
            parcelas: null, reembolsoPessoaId: null, reembolsoVencimento: null,
            vencimentoExato: new DateOnly(2026, 7, 10));
        var compra = db.Lancamentos.Single(l => l.CartaoCreditoId == cartao.Id);
        compra.Confirmado = true;
        db.SaveChanges();
        await new FaturaService(db).FecharAsync(cartao.Id, 2026, 7);

        var conta = await new ContaService(db).CriarAsync("Banco", null, null, null);
        var pernas = await new FaturaService(db).PagarAsync(
            cartao.Id, 2026, 7, conta.Id, new DateOnly(2026, 7, 20), 100m);
        return (pernas[0], pernas[0].ReferenciaId!.Value, db, file);
    }

    [Fact]
    public async Task PagamentoFatura_NaoPodeSerExcluidoNemAlterado()
    {
        var (perna, _, db, file) = await CriarPagamentoAsync();
        try
        {
            var service = new LancamentoService(db);

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExcluirAsync(perna.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AlternarConfirmadoAsync(perna.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(
                () => service.AtualizarTransferenciaAsync(
                    perna.Id, perna.ContaId!.Value, perna.ContaId.Value, perna.Data, 10m));

            // Estorno pela fatura continua funcionando (caminho dedicado).
            await new FaturaService(db).EstornarAsync(perna.ReferenciaId!.Value);
            Assert.False(db.Lancamentos.Any(l => l.ReferenciaId == perna.ReferenciaId));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task PagamentoFatura_NaoEntraEmFluxoNemCalendario()
    {
        var (perna, _, db, file) = await CriarPagamentoAsync();
        try
        {
            var fluxo = await new FluxoService(db).ObterCardAsync(2026, 7);
            Assert.Equal(0m, fluxo.TotalReceitas);
            Assert.Equal(100m, fluxo.TotalDespesas); // só a compra do cartão; pernas de pagamento fora

            var calendario = await new CalendarioService(db, new FaturaService(db), new LembreteService(db)).ObterMesAsync(2026, 8);
            var itensCalendario = calendario.Dias.SelectMany(d => d.Itens).ToList();
            Assert.DoesNotContain(itensCalendario,
                i => i.Valor == 100m && !i.Descricao.StartsWith("Fatura "));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
