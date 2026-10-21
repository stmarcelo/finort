using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;
using Microsoft.EntityFrameworkCore;

namespace Finort.Tests;

public class RadarFiltroTests
{
    private static (AppDbContext Db, string File, Conta Conta, CartaoCredito Cartao, Categoria Despesa, Categoria Receita) Setup()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartao = new CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234", MelhorDiaCompra = 5,
            DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
        };
        db.CartoesCredito.Add(cartao);
        var despesa = new Categoria { Nome = "Mercado" };
        var receita = new Categoria { Nome = "Receita" };
        db.Categorias.Add(despesa);
        db.Categorias.Add(receita);
        db.SaveChanges();
        return (db, file, conta, cartao, despesa, receita);
    }

    private static Lancamento Despesa(AppDbContext db, DateOnly data, decimal valor, Guid categoriaId, Guid? cartaoId = null)
    {
        var l = new Lancamento
        {
            Data = data,
            Tipo = LancamentoTipo.Despesa,
            Valor = -Math.Abs(valor),
            CartaoCreditoId = cartaoId,
            CategoriaId = categoriaId,
            Confirmado = true
        };
        db.Lancamentos.Add(l);
        db.SaveChanges();
        return l;
    }

    private static Lancamento Receita(AppDbContext db, DateOnly data, decimal valor, Guid categoriaId)
    {
        var l = new Lancamento
        {
            Data = data,
            Tipo = LancamentoTipo.Receita,
            Valor = Math.Abs(valor),
            CategoriaId = categoriaId,
            Confirmado = true
        };
        db.Lancamentos.Add(l);
        db.SaveChanges();
        return l;
    }

    [Fact]
    public void BaseRadar_DespesaNormal_Entra()
    {
        var (db, file, _, _, despesa, _) = Setup();
        try
        {
            var l = Despesa(db, new DateOnly(2026, 8, 5), 100m, despesa.Id);

            var ids = db.Lancamentos.BaseRadar(db).Select(x => x.Id).ToList();

            Assert.Contains(l.Id, ids);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public void BaseRadar_DespesaCartaoReembolsada_NaoEntra()
    {
        var (db, file, _, cartao, despesa, _) = Setup();
        try
        {
            var l = Despesa(db, new DateOnly(2026, 8, 5), 100m, despesa.Id, cartao.Id);
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = db.Pessoas.Add(new Pessoa { Nome = "A" }).Entity.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = l.Id,
                Valor = -100m,
                Vencimento = new DateOnly(2026, 8, 9)
            });
            db.SaveChanges();

            var ids = db.Lancamentos.BaseRadar(db).Select(x => x.Id).ToList();

            Assert.DoesNotContain(l.Id, ids);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public void BaseRadar_ReceitaNormal_Entra()
    {
        var (db, file, _, _, _, receita) = Setup();
        try
        {
            var l = Receita(db, new DateOnly(2026, 8, 5), 5000m, receita.Id);

            var ids = db.Lancamentos.BaseRadar(db).Select(x => x.Id).ToList();

            Assert.Contains(l.Id, ids);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public void BaseRadar_ReceitaDeReembolso_NaoEntra()
    {
        var (db, file, _, cartao, despesa, receita) = Setup();
        try
        {
            var despesaReembolsada = Despesa(db, new DateOnly(2026, 8, 5), 100m, despesa.Id, cartao.Id);
            var receitaReembolso = Receita(db, new DateOnly(2026, 8, 20), 100m, receita.Id);
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = db.Pessoas.Add(new Pessoa { Nome = "A" }).Entity.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = despesaReembolsada.Id,
                ReceitaId = receitaReembolso.Id,
                Valor = -100m,
                Vencimento = new DateOnly(2026, 8, 9)
            });
            db.SaveChanges();

            var ids = db.Lancamentos.BaseRadar(db).Select(x => x.Id).ToList();

            Assert.DoesNotContain(despesaReembolsada.Id, ids);
            Assert.DoesNotContain(receitaReembolso.Id, ids);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task BaseRadar_DespesaEReceitaDeReembolso_EquivaleANaoExistirem()
    {
        var (db, file, _, cartao, despesa, receita) = Setup();
        try
        {
            Despesa(db, new DateOnly(2026, 8, 1), 100m, despesa.Id);
            var despesaReembolsada = Despesa(db, new DateOnly(2026, 8, 5), 500m, despesa.Id, cartao.Id);
            var receitaReembolso = Receita(db, new DateOnly(2026, 8, 20), 500m, receita.Id);
            db.Reembolsos.Add(new Reembolso
            {
                PessoaId = db.Pessoas.Add(new Pessoa { Nome = "A" }).Entity.Id,
                CartaoCreditoId = cartao.Id,
                LancamentoId = despesaReembolsada.Id,
                ReceitaId = receitaReembolso.Id,
                Valor = -500m,
                Vencimento = new DateOnly(2026, 8, 9)
            });
            db.SaveChanges();

            var comReembolso = await SomaAsync(db);
            db.Lancamentos.RemoveRange(despesaReembolsada, receitaReembolso);
            db.SaveChanges();
            var semReembolso = await SomaAsync(db);

            Assert.Equal(100m, comReembolso);
            Assert.Equal(semReembolso, comReembolso);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public void BaseRadar_Transferencia_NaoEntra()
    {
        var (db, file, _, _, despesa, _) = Setup();
        try
        {
            var l = new Lancamento
            {
                Data = new DateOnly(2026, 8, 5),
                Tipo = LancamentoTipo.Transferencia,
                Valor = -100m,
                CategoriaId = despesa.Id,
                Confirmado = true
            };
            db.Lancamentos.Add(l);
            db.SaveChanges();

            var ids = db.Lancamentos.BaseRadar(db).Select(x => x.Id).ToList();

            Assert.DoesNotContain(l.Id, ids);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    private static async Task<decimal> SomaAsync(AppDbContext db)
    {
        var valores = await db.Lancamentos.BaseRadar(db).Select(l => l.Valor).ToListAsync();
        return valores.Sum(Math.Abs);
    }
}
