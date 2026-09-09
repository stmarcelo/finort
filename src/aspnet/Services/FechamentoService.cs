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

    public async Task<List<ConferenciaMes>> ObterConferenciasAsync(int ano, int mes)
    {
        var contas = await _db.Contas.ToListAsync();
        var result = new List<ConferenciaMes>();

        foreach (var conta in contas)
        {
            var conferencia = await ObterConferenciaAsync(conta.Id, conta.Nome, ano, mes);
            result.Add(conferencia);
        }

        return result;
    }

    public async Task<ConferenciaMes> ObterConferenciaAsync(Guid contaId, string nomeConta, int ano, int mes)
    {
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var saldoAcumulado = await _db.Lancamentos
            .Where(l => l.ContaId == contaId && l.Confirmado && l.Data <= fim)
            .SumAsync(l => (decimal?)l.Valor) ?? 0m;

        // Launches with this account, not credit card
        var temPendenciasConta = await _db.Lancamentos
            .AnyAsync(l => l.ContaId == contaId && l.CartaoCreditoId == null
                && !l.Confirmado && l.Data >= inicio && l.Data <= fim);

        // Launches without any account, not credit card
        var temPendenciasSemConta = await _db.Lancamentos
            .AnyAsync(l => l.ContaId == null && l.CartaoCreditoId == null
                && !l.Confirmado && l.Data >= inicio && l.Data <= fim);

        var mesFechado = await _db.MesesFechados
            .AnyAsync(m => m.ContaId == contaId && m.Ano == ano && m.Mes == mes);

        return new ConferenciaMes(contaId, nomeConta, ano, mes, saldoAcumulado,
            temPendenciasConta || temPendenciasSemConta, mesFechado);
    }

    public async Task FecharAsync(Guid contaId, int ano, int mes, decimal saldoReal)
    {
        if (await EstaFechadoAsync(contaId, ano, mes))
            throw new InvalidOperationException("Este mês já está fechado para esta conta.");

        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1).AddDays(-1);

        var pendentes = await _db.Lancamentos
            .CountAsync(l => l.ContaId == contaId && l.CartaoCreditoId == null
                && !l.Confirmado && l.Data >= inicio && l.Data <= fim);
        if (pendentes > 0)
            throw new InvalidOperationException($"Existem {pendentes} lançamento(s) não confirmado(s) neste mês.");

        var pendentesSemConta = await _db.Lancamentos
            .CountAsync(l => l.ContaId == null && l.CartaoCreditoId == null
                && !l.Confirmado && l.Data >= inicio && l.Data <= fim);
        if (pendentesSemConta > 0)
            throw new InvalidOperationException(
                $"Existem {pendentesSemConta} lançamento(s) sem conta vinculada não confirmado(s). " +
                "Vincule uma conta ou confirme/exclua-os antes de fechar o mês.");

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

        _db.MesesFechados.Add(new MesFechado
        {
            ContaId = contaId,
            Ano = ano,
            Mes = mes,
            DataFechamento = DateTime.Now,
            SaldoAcumulado = saldoReal
        });

        await _db.SaveChangesAsync();
    }

    public async Task ReabrirAsync(int ano, int mes)
    {
        var registros = await _db.MesesFechados
            .Where(m => m.Ano == ano && m.Mes == mes)
            .ToListAsync();

        if (registros.Count == 0)
            throw new InvalidOperationException("Nenhum fechamento encontrado para este mês.");

        _db.MesesFechados.RemoveRange(registros);
        await _db.SaveChangesAsync();
    }

    public async Task<(int Ano, int Mes)?> ObterUltimoMesFechadoAsync()
    {
        var ultimo = await _db.MesesFechados
            .OrderByDescending(m => m.Ano).ThenByDescending(m => m.Mes)
            .FirstOrDefaultAsync();

        return ultimo is null ? null : (ultimo.Ano, ultimo.Mes);
    }

    public Task<bool> EstaFechadoAsync(Guid contaId, int ano, int mes)
        => _db.MesesFechados.AnyAsync(m => m.ContaId == contaId && m.Ano == ano && m.Mes == mes);
}
