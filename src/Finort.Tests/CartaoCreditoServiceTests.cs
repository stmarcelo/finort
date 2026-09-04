using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class CartaoCreditoServiceTests
{
    private static async Task<(AppDbContext Db, string File, CartaoCreditoService Service, Conta Conta)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var contaService = new ContaService(db);
        var conta = await contaService.CriarAsync("Conta pagamento", null, null, null);
        return (db, file, new CartaoCreditoService(db), conta);
    }

    [Fact]
    public async Task CriarAsync_PersisteEPodeListar()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var cartao = await service.CriarAsync("Nubank", "1234", 5, 10, 5000m, conta.Id);

            var carregado = Assert.Single(await service.ListarAsync());
            Assert.Equal(cartao.Id, carregado.Id);
            Assert.Equal("Nubank", carregado.Banco);
            Assert.Equal("1234", carregado.Ultimos4Digitos);
            Assert.True(carregado.Ativo);
            Assert.Equal(conta.Id, carregado.ContaId);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Theory]
    [InlineData(0)]
    [InlineData(32)]
    public async Task CriarAsync_DiaInvalido_LancaArgumento(int diaInvalido)
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CriarAsync("X", "1234", diaInvalido, 10, 100m, conta.Id));
            await Assert.ThrowsAsync<ArgumentException>(
                () => service.CriarAsync("X", "1234", 5, diaInvalido, 100m, conta.Id));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Theory]
    // compra dia 1 < melhorDia 5 → vencimento mês+1
    [InlineData("2026-08-01", 2026, 9, 10)]
    // compra dia 5 >= melhorDia 5 → vencimento mês+2
    [InlineData("2026-08-05", 2026, 10, 10)]
    // compra dia 6 >= melhorDia 5 → vencimento mês+2
    [InlineData("2026-08-06", 2026, 10, 10)]
    public void CalcularVencimento_UsaMelhorDiaEVencimento(
        string compra, int anoEsperado, int mesEsperado, int diaEsperado)
    {
        var cartao = new CartaoCredito { MelhorDiaCompra = 5, DiaVencimento = 10 };

        var vencimento = CartaoCreditoService.CalcularVencimento(cartao, DateOnly.Parse(compra));

        Assert.Equal(new DateOnly(anoEsperado, mesEsperado, diaEsperado), vencimento);
    }

    [Fact]
    public void CalcularVencimento_DiaMaiorQueOMes_ClampaUltimoDia()
    {
        var cartao = new CartaoCredito { MelhorDiaCompra = 1, DiaVencimento = 31 };

        // compra 02/12/2025 (dia >= melhorDia 1) → vencimento mês+2 = fev/2026 → clamp no último dia
        var vencimento = CartaoCreditoService.CalcularVencimento(cartao, new DateOnly(2025, 12, 2));

        Assert.Equal(new DateOnly(2026, 2, 28), vencimento);
    }

    [Fact]
    public async Task ExcluirAsync_ComLancamento_LancaExcecao()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var cartao = await service.CriarAsync("X", "1234", 5, 10, 100m, conta.Id);
            var renda = db.Categorias.First(c => c.Nome == "Receita");
            db.Lancamentos.Add(new Lancamento
            {
                Data = new DateOnly(2026, 8, 10),
                Tipo = LancamentoTipo.Despesa,
                Valor = -50m,
                CartaoCreditoId = cartao.Id,
                CategoriaId = renda.Id
            });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExcluirAsync(cartao.Id));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task ExcluirAsync_ComProvisaoVinculada_LancaExcecao()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var cartao = await service.CriarAsync("X", "1234", 5, 10, 100m, conta.Id);
            db.Provisoes.Add(new Provisao
            {
                Onde = ProvisaoOnde.DebitoCartao,
                Frequencia = ProvisaoFrequencia.Mensal,
                Dia = 10,
                Valor = 100m,
                ValorVariante = false,
                CartaoCreditoId = cartao.Id,
                CategoriaId = db.Categorias.First(c => c.Nome == "Receita").Id
            });
            await db.SaveChangesAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() => service.ExcluirAsync(cartao.Id));
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task AtivarAsync_AlternaFlag()
    {
        var (db, file, service, conta) = await SetupAsync();
        try
        {
            var cartao = await service.CriarAsync("X", "1234", 5, 10, 100m, conta.Id);

            await service.AtivarAsync(cartao.Id, false);

            Assert.False((await service.ObterAsync(cartao.Id))!.Ativo);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
