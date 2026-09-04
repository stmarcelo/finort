using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class FechamentoService
{
    private readonly AppDbContext _db;

    public FechamentoService(AppDbContext db)
    {
        _db = db;
    }

    /// <summary>Estado de conferência: saldo confirmado da conta, pendências do mês e situação de fecho.</summary>
    public async Task<ConferenciaMes> ObterConferenciaAsync(Guid contaId, int ano, int mes)
    {
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var saldoAcumulado = await _db.Lancamentos
            .Where(l => l.ContaId == contaId && l.Confirmado && l.Data <= fim)
            .SumAsync(l => (decimal?)l.Valor) ?? 0m;

        var temPendencias = await _db.Lancamentos
            .AnyAsync(l => !l.Confirmado && l.Data >= inicio && l.Data <= fim);

        return new ConferenciaMes(contaId, ano, mes, saldoAcumulado, temPendencias,
            await EstaFechadoAsync(ano, mes));
    }

    /// <summary>
    /// Fecha o mês (e os anteriores abertos, em cascata). Com diferença entre saldo real e
    /// acumulado, cria lançamento de acerto confirmado na categoria "Acerto de saldo".
    /// </summary>
    public async Task FecharAsync(Guid contaId, int ano, int mes, decimal saldoReal)
    {
        if (await EstaFechadoAsync(ano, mes))
            throw new InvalidOperationException("Este mês já está fechado.");

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var pendentes = await _db.Lancamentos
            .CountAsync(l => !l.Confirmado && l.Data >= inicio && l.Data <= fim);
        if (pendentes > 0)
            throw new InvalidOperationException($"Existem {pendentes} lançamento(s) não confirmado(s) neste mês.");

        var saldoAcumulado = await _db.Lancamentos
            .Where(l => l.ContaId == contaId && l.Confirmado && l.Data <= fim)
            .SumAsync(l => (decimal?)l.Valor) ?? 0m;

        var diferenca = saldoReal - saldoAcumulado;
        if (diferenca != 0m)
        {
            var categoria = await _db.Categorias.AsNoTracking()
                .SingleAsync(c => c.IsProtected && c.Nome == "Acerto de saldo");
            var subcategoria = await _db.Subcategorias.AsNoTracking()
                .SingleAsync(s => s.IsProtected && s.CategoriaId == categoria.Id && s.Nome == "Acerto");

            _db.Lancamentos.Add(new Lancamento
            {
                Data = fim,
                Tipo = diferenca > 0 ? LancamentoTipo.Receita : LancamentoTipo.Despesa,
                Valor = diferenca,
                ContaId = contaId,
                CategoriaId = categoria.Id,
                SubcategoriaId = subcategoria.Id,
                Confirmado = true
            });
        }

        var pisoBruto = await _db.Lancamentos.MinAsync(l => (DateOnly?)l.Data);
        var periodo = pisoBruto is null ? new DateOnly(ano, mes, 1) : new DateOnly(pisoBruto.Value.Year, pisoBruto.Value.Month, 1);
        var limite = new DateOnly(ano, mes, 1);

        while (periodo <= limite)
        {
            if (!await EstaFechadoAsync(periodo.Year, periodo.Month))
                _db.MesesFechados.Add(new MesFechado { Ano = periodo.Year, Mes = periodo.Month, DataFechamento = DateTime.Now, SaldoAcumulado = saldoAcumulado });

            periodo = periodo.AddMonths(1);
        }

        await _db.SaveChangesAsync();
    }

    private Task<bool> EstaFechadoAsync(int ano, int mes)
        => _db.MesesFechados.AnyAsync(m => m.Ano == ano && m.Mes == mes);
}
