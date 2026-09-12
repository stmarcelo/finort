using Finort.Data;
using Finort.Models.Financeiro;
using Finort.Services;

namespace Finort.Tests;

public class ReembolsoRelatorioTests
{
    private static async Task<(AppDbContext Db, string File, ReembolsoRelatorioService Service, CartaoCredito CartaoA, CartaoCredito CartaoB, Pessoa PessoaA, Pessoa PessoaB)> SetupAsync()
    {
        var (db, file) = TestDbContext.Create();
        var conta = new Conta { Nome = "Conta" };
        db.Contas.Add(conta);
        var cartaoA = new CartaoCredito
        {
            Banco = "Nubank", Ultimos4Digitos = "1234", MelhorDiaCompra = 5,
            DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
        };
        var cartaoB = new CartaoCredito
        {
            Banco = "Inter", Ultimos4Digitos = "5678", MelhorDiaCompra = 5,
            DiaVencimento = 10, Limite = 5000m, Ativo = true, ContaId = conta.Id
        };
        var pessoaA = new Pessoa { Nome = "Alice" };
        var pessoaB = new Pessoa { Nome = "Beto" };
        db.CartoesCredito.AddRange(cartaoA, cartaoB);
        db.Pessoas.AddRange(pessoaA, pessoaB);
        await db.SaveChangesAsync();
        var cat = db.Categorias.First(c => c.Nome == "Receita");

        async Task<Guid> DespesaAsync(CartaoCredito cartao)
        {
            var l = new Lancamento
            {
                Data = new DateOnly(2026, 8, 6), Tipo = LancamentoTipo.Despesa,
                Valor = -100m, CategoriaId = cat.Id, CartaoCreditoId = cartao.Id,
                DataVencimentoCartao = new DateOnly(2026, 9, 10)
            };
            db.Lancamentos.Add(l);
            await db.SaveChangesAsync();
            return l.Id;
        }

        db.Reembolsos.Add(new Reembolso
        {
            PessoaId = pessoaA.Id, CartaoCreditoId = cartaoA.Id,
            LancamentoId = await DespesaAsync(cartaoA),
            Valor = 100m, Vencimento = new DateOnly(2026, 9, 9)
        });
        db.Reembolsos.Add(new Reembolso
        {
            PessoaId = pessoaB.Id, CartaoCreditoId = cartaoB.Id,
            LancamentoId = await DespesaAsync(cartaoB),
            Valor = 50m, Vencimento = new DateOnly(2026, 9, 15), Fechado = true
        });
        await db.SaveChangesAsync();

        return (db, file, new ReembolsoRelatorioService(db, new TestWebHostEnvironment()), cartaoA, cartaoB, pessoaA, pessoaB);
    }

    [Fact]
    public async Task Relatorio_Filtra_PorPeriodo_Cartao_Pessoa()
    {
        var (db, file, service, cartaoA, _, pessoaA, _) = await SetupAsync();
        try
        {
            var r = await service.GerarAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 10), cartaoA.Id, pessoaA.Id);
            Assert.Single(r.Linhas);
            Assert.Equal(100m, r.TotalPendente);
            Assert.Equal(0m, r.TotalFechado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Relatorio_SemFiltro_Retorna_Totais_Fechado_E_Pendente()
    {
        var (db, file, service, _, _, _, _) = await SetupAsync();
        try
        {
            var r = await service.GerarAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), null, null);
            Assert.Equal(2, r.Linhas.Count);
            Assert.Equal(100m, r.TotalPendente);
            Assert.Equal(50m, r.TotalFechado);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }

    [Fact]
    public async Task Relatorio_GeraPdfBytes()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var (db, file, service, _, _, _, _) = await SetupAsync();
        try
        {
            var pdf = await service.GerarPdfBytesAsync(new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 30), null, null);
            Assert.NotNull(pdf);
            Assert.True(pdf.Length > 1000);
        }
        finally { TestDbContext.Cleanup(db, file); }
    }
}
