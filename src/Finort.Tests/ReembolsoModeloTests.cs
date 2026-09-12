using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

public class ReembolsoModeloTests
{
    [Fact]
    public async Task Reembolso_Possui_Vencimento_E_Fechado_False_Padrao()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var db = new AppDbContext(options);
        db.Reembolsos.Add(new Reembolso
        {
            PessoaId = Guid.NewGuid(),
            CartaoCreditoId = Guid.NewGuid(),
            LancamentoId = Guid.NewGuid(),
            Valor = 100m,
            Vencimento = new DateOnly(2026, 9, 9)
        });
        await db.SaveChangesAsync();
        var r = await db.Reembolsos.FirstAsync();
        Assert.False(r.Fechado);
        Assert.Equal(new DateOnly(2026, 9, 9), r.Vencimento);
    }
}
