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
}
