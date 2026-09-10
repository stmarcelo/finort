using Finort.Data;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class CartaoCreditoService
{
    private readonly AppDbContext _db;

    public CartaoCreditoService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<CartaoCredito>> ListarAsync()
        => await _db.CartoesCredito.OrderBy(c => c.Banco).ToListAsync();

    public async Task<List<CartaoResumo>> ListarComSaldoAsync()
    {
        var cartoes = await _db.CartoesCredito.OrderBy(c => c.Banco).ToListAsync();

        // Pagamentos positivos por cartão/mês (perna destino de transferência).
        var pagamentos = await _db.Lancamentos
            .Where(l => l.Tipo == LancamentoTipo.Transferencia && l.CartaoCreditoId != null && l.Valor > 0m)
            .GroupBy(l => new { l.CartaoCreditoId, l.Data.Year, l.Data.Month })
            .Select(g => new { g.Key.CartaoCreditoId, g.Key.Year, g.Key.Month, Total = g.Sum(l => l.Valor) })
            .ToListAsync();

        // Faturas fechadas: um mês é "pago" quando o total pago >= |ValorTotal| da fatura.
        var faturas = await _db.Faturas
            .Where(f => f.Fechada)
            .Select(f => new { f.CartaoCreditoId, f.AnoReferencia, f.MesReferencia, f.ValorTotal })
            .ToListAsync();
        var mesesPagos = new HashSet<(Guid, int, int)>();
        foreach (var f in faturas)
        {
            var pago = pagamentos.FirstOrDefault(p =>
                p.CartaoCreditoId == f.CartaoCreditoId && p.Year == f.AnoReferencia && p.Month == f.MesReferencia);
            if (pago is not null && pago.Total >= Math.Abs(f.ValorTotal))
                mesesPagos.Add((f.CartaoCreditoId, f.AnoReferencia, f.MesReferencia));
        }

        // Lançamentos ainda não pagos: todos do cartão, sem provisão, excluindo o pagamento
        // (perna destino positiva) e os meses de fatura já pagos.
        var naoPagos = await _db.Lancamentos
            .Where(l => l.CartaoCreditoId != null)
            .Select(l => new { l.CartaoCreditoId, l.Valor, l.Tipo, l.ProvisaoId, l.Data.Year, l.Data.Month })
            .ToListAsync();
        var mapa = naoPagos
            .Where(l => l.ProvisaoId == null &&
                        !(l.Tipo == LancamentoTipo.Transferencia && l.Valor > 0m) &&
                        !mesesPagos.Contains((l.CartaoCreditoId!.Value, l.Year, l.Month)))
            .GroupBy(l => l.CartaoCreditoId!.Value)
            .ToDictionary(g => g.Key, g => g.Sum(l => l.Valor));

        return cartoes.Select(c => new CartaoResumo
        {
            Id = c.Id,
            Banco = c.Banco,
            Ultimos4Digitos = c.Ultimos4Digitos,
            MelhorDiaCompra = c.MelhorDiaCompra,
            DiaVencimento = c.DiaVencimento,
            Limite = c.Limite,
            TotalNaoPago = mapa.GetValueOrDefault(c.Id),
            Ativo = c.Ativo,
            ContaId = c.ContaId
        }).ToList();
    }

    public async Task<CartaoCredito?> ObterAsync(Guid id)
        => await _db.CartoesCredito.FindAsync(id);

    public async Task<CartaoCredito> CriarAsync(
        string banco, string ultimos4Digitos, int melhorDiaCompra, int diaVencimento, decimal limite, Guid? contaId)
    {
        ValidarDias(melhorDiaCompra, diaVencimento);
        var cartao = new CartaoCredito
        {
            Banco = banco.Trim(),
            Ultimos4Digitos = ultimos4Digitos.Trim(),
            MelhorDiaCompra = melhorDiaCompra,
            DiaVencimento = diaVencimento,
            Limite = limite,
            Ativo = true,
            ContaId = contaId
        };
        _db.CartoesCredito.Add(cartao);
        await _db.SaveChangesAsync();
        return cartao;
    }

    public async Task AtualizarAsync(
        CartaoCredito cartao, string banco, string ultimos4Digitos, int melhorDiaCompra, int diaVencimento, decimal limite, Guid? contaId)
    {
        ValidarDias(melhorDiaCompra, diaVencimento);
        cartao.Banco = banco.Trim();
        cartao.Ultimos4Digitos = ultimos4Digitos.Trim();
        cartao.MelhorDiaCompra = melhorDiaCompra;
        cartao.DiaVencimento = diaVencimento;
        cartao.Limite = limite;
        cartao.ContaId = contaId;
        await _db.SaveChangesAsync();
    }

    public async Task AtivarAsync(Guid id, bool ativo)
    {
        var cartao = await _db.CartoesCredito.FindAsync(id)
            ?? throw new InvalidOperationException("Cartão não encontrado.");
        cartao.Ativo = ativo;
        await _db.SaveChangesAsync();
    }

    public async Task ExcluirAsync(Guid id)
    {
        var cartao = await _db.CartoesCredito.FindAsync(id)
            ?? throw new InvalidOperationException("Cartão não encontrado.");

        if (await _db.Lancamentos.AnyAsync(l => l.CartaoCreditoId == id))
            throw new InvalidOperationException("Este cartão possui lançamentos vinculados e não pode ser excluído.");

        if (await _db.Faturas.AnyAsync(f => f.CartaoCreditoId == id))
            throw new InvalidOperationException("Este cartão possui faturas vinculadas e não pode ser excluído.");

        if (await _db.Provisoes.AnyAsync(p => p.CartaoCreditoId == id))
            throw new InvalidOperationException("Este cartão possui provisões vinculadas e não pode ser excluído.");

        _db.CartoesCredito.Remove(cartao);
        await _db.SaveChangesAsync();
    }

    public static DateOnly CalcularVencimento(CartaoCredito cartao, DateOnly dataCompra)
    {
        var dl = dataCompra.Day;
        var md = cartao.MelhorDiaCompra;
        var dv = cartao.DiaVencimento;

        // pf-a: md/ml-1 até md-1/ml
        var pfAInicio = dataCompra.AddMonths(-1);
        pfAInicio = new DateOnly(pfAInicio.Year, pfAInicio.Month, md);
        var pfAFim = new DateOnly(dataCompra.Year, dataCompra.Month, md).AddDays(-1);

        // pf-b: md/ml até md-1/ml+1
        var pfBInicio = new DateOnly(dataCompra.Year, dataCompra.Month, md);
        var pfBFim = new DateOnly(dataCompra.Year, dataCompra.Month, md).AddMonths(1).AddDays(-1);

        DateOnly vcto;
        if (dataCompra >= pfAInicio && dataCompra <= pfAFim)
        {
            vcto = new DateOnly(pfAFim.Year, pfAFim.Month,
                Math.Min(dv, DateTime.DaysInMonth(pfAFim.Year, pfAFim.Month)));
        }
        else
        {
            vcto = new DateOnly(pfBFim.Year, pfBFim.Month,
                Math.Min(dv, DateTime.DaysInMonth(pfBFim.Year, pfBFim.Month)));
        }

        // Se dv < md, antecipa +1 mês
        if (dv < md)
            vcto = vcto.AddMonths(1);

        return vcto;
    }

    /// <summary>Calcula o período da fatura para um mês de vencimento informado.
    /// Retorna (inicio, fim) inclusive.</summary>
    public static (DateOnly Inicio, DateOnly Fim) CalcularPeriodoFatura(CartaoCredito cartao, int anoVcto, int mesVcto)
    {
        var md = cartao.MelhorDiaCompra;
        var dv = cartao.DiaVencimento;

        DateOnly inicio;
        DateOnly fim;

        if (dv >= md)
        {
            // Período: md/m-1 até md-1/m
            inicio = new DateOnly(anoVcto, mesVcto, 1).AddMonths(-1);
            inicio = new DateOnly(inicio.Year, inicio.Month, md);
            fim = new DateOnly(anoVcto, mesVcto, md).AddDays(-1);
        }
        else
        {
            // Período: md/m-2 até md-1/m-1
            inicio = new DateOnly(anoVcto, mesVcto, 1).AddMonths(-2);
            inicio = new DateOnly(inicio.Year, inicio.Month, md);
            fim = new DateOnly(anoVcto, mesVcto, 1).AddMonths(-1);
            fim = new DateOnly(fim.Year, fim.Month, Math.Min(md - 1, DateTime.DaysInMonth(fim.Year, fim.Month)));
        }

        return (inicio, fim);
    }

    private static void ValidarDias(int melhorDiaCompra, int diaVencimento)
    {
        if (melhorDiaCompra is < 1 or > 31)
            throw new ArgumentException("Melhor dia de compra deve estar entre 1 e 31.");
        if (diaVencimento is < 1 or > 31)
            throw new ArgumentException("Dia de vencimento deve estar entre 1 e 31.");
    }
}
