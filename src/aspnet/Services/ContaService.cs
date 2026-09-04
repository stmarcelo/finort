using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class ContaService
{
    private readonly AppDbContext _db;

    public ContaService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<Conta>> ListarAsync()
        => await _db.Contas.OrderBy(c => c.Nome).ToListAsync();

    /// <summary>Contas com saldo real (confirmados) e previsto (tudo), acumulados até o último dia do mês atual.</summary>
    public async Task<List<ContaResumo>> ListarResumoAsync()
    {
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var fimDoMes = new DateOnly(hoje.Year, hoje.Month, DateTime.DaysInMonth(hoje.Year, hoje.Month));
        var contas = await _db.Contas.OrderBy(c => c.Nome).ToListAsync();
        var saldos = await _db.Lancamentos
            .Where(l => l.ContaId != null && l.Data <= fimDoMes)
            .GroupBy(l => l.ContaId!.Value)
            .Select(g => new
            {
                ContaId = g.Key,
                Real = g.Sum(l => l.Confirmado ? l.Valor : 0m),
                Previsto = g.Sum(l => l.Valor)
            })
            .ToListAsync();

        var mapa = saldos.ToDictionary(s => s.ContaId);
        return contas.Select(c =>
        {
            mapa.TryGetValue(c.Id, out var s);
            return new ContaResumo
            {
                Id = c.Id,
                Nome = c.Nome,
                Banco = c.Banco,
                SaldoReal = s?.Real ?? 0m,
                SaldoPrevisto = s?.Previsto ?? 0m
            };
        }).ToList();
    }

    public async Task<Conta?> ObterAsync(Guid id)
        => await _db.Contas.FindAsync(id);

    public async Task<Conta> CriarAsync(string nome, string? banco, string? agencia, string? contaEDigito)
    {
        var conta = new Conta { Nome = nome, Banco = banco, Agencia = agencia, ContaEDigito = contaEDigito };
        _db.Contas.Add(conta);
        await _db.SaveChangesAsync();
        return conta;
    }

    public async Task AtualizarAsync(Conta conta, string nome, string? banco, string? agencia, string? contaEDigito)
    {
        conta.Nome = nome;
        conta.Banco = banco;
        conta.Agencia = agencia;
        conta.ContaEDigito = contaEDigito;
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Guid id)
    {
        var conta = await _db.Contas.FindAsync(id)
            ?? throw new InvalidOperationException("Conta não encontrada.");

        if (await _db.Lancamentos.AnyAsync(l => l.ContaId == id))
            throw new InvalidOperationException("Esta conta possui lançamentos vinculados e não pode ser excluída.");

        if (await _db.CartoesCredito.AnyAsync(c => c.ContaId == id))
            throw new InvalidOperationException("Esta conta está vinculada a um cartão de crédito e não pode ser excluída.");

        if (await _db.Provisoes.AnyAsync(p => p.ContaId == id))
            throw new InvalidOperationException("Esta conta possui provisões vinculadas e não pode ser excluída.");

        _db.Contas.Remove(conta);
        await _db.SaveChangesAsync();
    }
}
