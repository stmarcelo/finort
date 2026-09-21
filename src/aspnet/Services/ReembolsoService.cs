using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;
namespace Finort.Services;
public class ReembolsoService
{
    private readonly AppDbContext _db;
    public ReembolsoService(AppDbContext db) => _db = db;
    public Task<List<Reembolso>> ObterDaFaturaAsync(Guid cartaoId, DateOnly inicio, DateOnly fim)
        => _db.Reembolsos.Include(r => r.Pessoa).Include(r => r.Lancamento)
            .Where(r => r.CartaoCreditoId == cartaoId && r.Lancamento.DataVencimentoCartao >= inicio && r.Lancamento.DataVencimentoCartao <= fim)
            .ToListAsync();
    public Task<List<Reembolso>> ObterPendentesAsync(DateOnly inicio, DateOnly fim)
        => _db.Reembolsos.Include(r => r.Pessoa).Include(r => r.CartaoCredito)
            .Where(r => !r.Fechado && r.Vencimento >= inicio && r.Vencimento <= fim)
            .ToListAsync();
    public async Task<Reembolso> AtualizarAsync(Guid id, decimal valor, DateOnly vencimento)
    {
        if (valor <= 0m)
            throw new ArgumentException("Informe um valor maior que zero.");
        if (vencimento == default)
            throw new ArgumentException("Informe o vencimento.");
        var r = await _db.Reembolsos.FindAsync(id)
            ?? throw new InvalidOperationException("Reembolso não encontrado.");
        if (r.Fechado)
            throw new InvalidOperationException("Reembolso já fechado; reabra a fatura para editar.");
        r.Valor = valor;
        r.Vencimento = vencimento;
        await _db.SaveChangesAsync();
        return r;
    }
}
