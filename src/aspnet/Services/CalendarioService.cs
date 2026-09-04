using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class CalendarioService
{
    private readonly AppDbContext _db;
    private readonly FaturaService _faturaService;
    private readonly LembreteService _lembreteService;

    public CalendarioService(AppDbContext db, FaturaService faturaService, LembreteService lembreteService)
    {
        _db = db;
        _faturaService = faturaService;
        _lembreteService = lembreteService;
    }

    /// <summary>Mês de compromissos: lançamentos reais + provisões futuras projetadas em memória.</summary>
    public async Task<CalendarioMes> ObterMesAsync(int ano, int mes)
    {
        var inicio = new DateOnly(ano, mes, 1);
        var fim = inicio.AddMonths(1);

        Guid? pagamentoSubId = await _db.Subcategorias.AsNoTracking()
            .Where(s => s.IsProtected && s.Nome == "Cartão de crédito")
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync();

        var lancamentos = await _db.Lancamentos
            .Include(l => l.Conta)
            .Include(l => l.CartaoCredito)
            .Include(l => l.Pessoa)
            .Include(l => l.Categoria)
            .Where(l => l.Data >= inicio && l.Data < fim && l.CartaoCreditoId == null
                && (pagamentoSubId == null || l.SubcategoriaId == null || l.SubcategoriaId != pagamentoSubId)
                && !_db.InvestimentosMovimentos.Any(m => m.LancamentoId == l.Id))
            .ToListAsync();

        var entradas = lancamentos.Select(l => (Data: l.Data, Item: ParaItem(l))).ToList();
        entradas.AddRange(await ProjetarProvisoesAsync(inicio, fim.AddDays(-1)));

        var lembretes = await _lembreteService.ObterLembretesDoMesAsync(ano, mes);

        foreach (var lembrete in lembretes)
        {
            var dia = lembrete.Tipo == LembreteTipo.Mensal
                ? lembrete.Dia!.Value
                : lembrete.Data!.Value.Day;

            var data = new DateOnly(ano, mes, Math.Min(dia, DateTime.DaysInMonth(ano, mes)));

            entradas.Add((data, new CompromissoItem(
                Valor: 0,
                Tipo: LancamentoTipo.Receita,
                Confirmado: false,
                Descricao: $"{lembrete.Texto} ({lembrete.Pessoa?.Nome})",
                Origem: "Lembrete",
                Projetada: false,
                LancamentoId: null,
                Riscada: false,
                IsLembrete: true)));
        }

        // Totais ANTES das faturas: compromissos de fatura são informativos e não entram.
        var totalApagar = -entradas.Where(e => e.Item.Valor < 0).Sum(e => e.Item.Valor);
        var totalAreceber = entradas.Where(e => e.Item.Valor > 0).Sum(e => e.Item.Valor);

        var cartoes = await _db.CartoesCredito.AsNoTracking().ToListAsync();
        var resumos = await _faturaService.ObterResumosParaCalendarioAsync(cartoes, ano, mes);
        foreach (var resumo in resumos)
        {
            var cartao = cartoes.First(c => c.Id == resumo.CartaoId);
            entradas.Add((resumo.Vencimento, new CompromissoItem(
                Valor: -resumo.ValorExibido,
                Tipo: LancamentoTipo.Despesa,
                Confirmado: resumo.Paga,
                Descricao: $"Fatura {cartao.Banco} ••{cartao.Ultimos4Digitos}",
                Origem: null,
                Projetada: false,
                LancamentoId: null,
                Riscada: resumo.Paga,
                IsFatura: true)));
        }

        var dias = entradas
            .GroupBy(e => e.Data)
            .OrderBy(g => g.Key)
            .Select(g => new CompromissoDia(g.Key, g.Select(e => e.Item)
                .OrderBy(i => i.IsLembrete || i.IsFatura ? 0 : 1)
                .ToList()))
            .ToList();

        return new CalendarioMes(ano, mes, totalApagar, totalAreceber, totalAreceber - totalApagar, dias);
    }

    private static CompromissoItem ParaItem(Lancamento l)
        => new(
            Valor: l.Valor,
            Tipo: l.Tipo,
            Confirmado: l.Confirmado,
            Descricao: l.Pessoa?.Nome ?? l.Categoria.Nome,
            Origem: l.Conta?.Nome ?? (l.CartaoCredito is null ? null : $"Cartão {l.CartaoCredito.Banco}"),
            Projetada: false,
            LancamentoId: l.Id);

    /// <summary>Mapeia projeções da régua compartilhada (ProvisaoAgenda) para itens do calendário.</summary>
    private async Task<List<(DateOnly Data, CompromissoItem Item)>> ProjetarProvisoesAsync(DateOnly inicio, DateOnly fim)
    {
        var projecoes = await ProvisaoAgenda.ProjetarAsync(_db, inicio, fim);

        return projecoes
            .Where(e => e.Provisao.Onde != ProvisaoOnde.DebitoCartao)
            .Select(e =>
        {
            var receita = e.Provisao.Onde == ProvisaoOnde.Receita;
            return (e.Data, new CompromissoItem(
                Valor: receita ? e.Provisao.Valor : -e.Provisao.Valor,
                Tipo: receita ? LancamentoTipo.Receita : LancamentoTipo.Despesa,
                Confirmado: false,
                Descricao: e.Provisao.Pessoa?.Nome ?? e.Provisao.Categoria.Nome,
                Origem: e.Provisao.Conta?.Nome ?? (e.Provisao.CartaoCredito is null ? null : $"Cartão {e.Provisao.CartaoCredito.Banco}"),
                Projetada: true,
                LancamentoId: null));
        }).ToList();
    }
}
