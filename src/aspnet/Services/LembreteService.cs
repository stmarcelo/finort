using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class LembreteService
{
    private readonly AppDbContext _db;

    public LembreteService(AppDbContext db) => _db = db;

    public async Task<List<Lembrete>> ListarPorPessoaAsync(Guid pessoaId)
    {
        return await _db.Lembretes
            .Where(l => l.PessoaId == pessoaId)
            .OrderBy(l => l.Tipo)
            .ThenBy(l => l.Dia ?? (l.Data.HasValue ? l.Data.Value.Day : 0))
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Lembrete> CriarAsync(Lembrete lembrete)
    {
        lembrete.Id = Guid.NewGuid();
        _db.Lembretes.Add(lembrete);
        await _db.SaveChangesAsync();
        return lembrete;
    }

    public async Task AtualizarAsync(Lembrete lembrete)
    {
        var existing = await _db.Lembretes.FindAsync(lembrete.Id)
            ?? throw new InvalidOperationException("Lembrete não encontrado");

        existing.Tipo = lembrete.Tipo;
        existing.Texto = lembrete.Texto;
        existing.Dia = lembrete.Dia;
        existing.Data = lembrete.Data;

        await _db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Guid id)
    {
        var lembrete = await _db.Lembretes.FindAsync(id)
            ?? throw new InvalidOperationException("Lembrete não encontrado");

        _db.Lembretes.Remove(lembrete);
        await _db.SaveChangesAsync();
    }

    public async Task<List<Lembrete>> ObterLembretesDoMesAsync(int ano, int mes)
    {
        return await _db.Lembretes
            .Where(l => l.Pessoa != null && (
                (l.Tipo == LembreteTipo.Mensal && l.Dia.HasValue && l.Dia.Value >= 1 && l.Dia.Value <= DateTime.DaysInMonth(ano, mes)) ||
                (l.Tipo == LembreteTipo.Unico && l.Data.HasValue && l.Data.Value.Year == ano && l.Data.Value.Month == mes)
            ))
            .Include(l => l.Pessoa)
            .AsNoTracking()
            .ToListAsync();
    }

    public async Task<Dictionary<Guid, int>> ContarPorPessoasAsync(List<Guid> pessoaIds)
    {
        return await _db.Lembretes
            .Where(l => pessoaIds.Contains(l.PessoaId))
            .GroupBy(l => l.PessoaId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
    }
}
