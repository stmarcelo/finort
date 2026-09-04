using Finort.Data;
using Finort.Models.Auth;
using Finort.Models.Financeiro;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Finort.Services;

public class SeedDataService
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<Configuracao> _passwordHasher = new();

    public SeedDataService(AppDbContext db)
    {
        _db = db;
    }

    public async Task SeedAsync()
    {
        if (await _db.Configuracoes.AnyAsync())
            return;

        // 1. Configuracao (login user)
        var config = new Configuracao
        {
            Nome = "Usuário Teste",
            Email = "teste@finort.com",
            SenhaHash = _passwordHasher.HashPassword(null!, "123456"),
            CreatedAt = DateTime.Now,
            UpdatedAt = DateTime.Now
        };
        config.BackupPasswordCriptografada =
            AuthService.PrefixoHashBackup + _passwordHasher.HashPassword(config, "backup123");
        _db.Configuracoes.Add(config);

        // 2. Contas (bank accounts)
        var contaCorrente = new Conta { Id = Guid.NewGuid(), Nome = "Nubank Conta Corrente", Banco = "Nubank", Agencia = "0001", ContaEDigito = "12345-6" };
        var contaPoupanca = new Conta { Id = Guid.NewGuid(), Nome = "Itaú Poupança", Banco = "Itaú", Agencia = "0001", ContaEDigito = "67890-1" };
        var contaSalario = new Conta { Id = Guid.NewGuid(), Nome = "Bradesco Conta Salário", Banco = "Bradesco", Agencia = "0001", ContaEDigito = "11111-2" };
        var contaDinheiro = new Conta { Id = Guid.NewGuid(), Nome = "Dinheiro" };
        _db.Contas.AddRange(contaCorrente, contaPoupanca, contaSalario, contaDinheiro);

        // 3. Cartões de Crédito (credit cards)
        var cartaoNubank = new CartaoCredito
        {
            Id = Guid.NewGuid(),
            Banco = "Nubank",
            Ultimos4Digitos = "4321",
            MelhorDiaCompra = 15,
            DiaVencimento = 10,
            Limite = 12000m,
            Ativo = true,
            ContaId = contaCorrente.Id
        };
        var cartaoItau = new CartaoCredito
        {
            Id = Guid.NewGuid(),
            Banco = "Itaú",
            Ultimos4Digitos = "8765",
            MelhorDiaCompra = 20,
            DiaVencimento = 25,
            Limite = 8000m,
            Ativo = true,
            ContaId = contaPoupanca.Id
        };
        _db.CartoesCredito.AddRange(cartaoNubank, cartaoItau);

        // 4. Pessoas (people)
        var pessoa1 = new Pessoa { Id = Guid.NewGuid(), Nome = "Maria Silva", CorDeExibicao = "#4CAF50" };
        var pessoa2 = new Pessoa { Id = Guid.NewGuid(), Nome = "João Santos", CorDeExibicao = "#2196F3" };
        var pessoa3 = new Pessoa { Id = Guid.NewGuid(), Nome = "Ana Oliveira", CorDeExibicao = "#FF9800" };
        _db.Pessoas.AddRange(pessoa1, pessoa2, pessoa3);

        // 5. Projetos (projects)
        var projeto1 = new Projeto { Id = Guid.NewGuid(), Descricao = "Viagem Europa", Concluido = false, DataContratacao = new DateOnly(2026, 1, 15), ValorContratado = 15000m, PessoaId = pessoa1.Id };
        var projeto2 = new Projeto { Id = Guid.NewGuid(), Descricao = "Reforma Casa", Concluido = false, DataContratacao = new DateOnly(2026, 3, 1), ValorContratado = 25000m, PessoaId = pessoa2.Id };
        _db.Projetos.AddRange(projeto1, projeto2);

        await _db.SaveChangesAsync();

        // 6. Lançamentos (transactions) - multiple months
        var hoje = DateOnly.FromDateTime(DateTime.Today);
        var lancamentos = new List<Lancamento>();

        // Get category IDs
        var catContasCasa = Guid.Parse("10000000-0000-0000-0000-000000000001");
        var catRenda = Guid.Parse("10000000-0000-0000-0000-000000000002");
        var catAlimentacao = Guid.Parse("10000000-0000-0000-0000-000000000003");
        var catTransporte = Guid.Parse("10000000-0000-0000-0000-000000000004");
        var catSaude = Guid.Parse("10000000-0000-0000-0000-000000000005");
        var catCompras = Guid.Parse("10000000-0000-0000-0000-000000000006");
        var catLazer = Guid.Parse("10000000-0000-0000-0000-000000000008");
        var catFinanceiro = Guid.Parse("10000000-0000-0000-0000-000000000009");

        // Subcategory IDs
        var subEnergia = Guid.Parse("20000000-0000-0000-0000-000000000001");
        var subAgua = Guid.Parse("20000000-0000-0000-0000-000000000002");
        var subInternet = Guid.Parse("20000000-0000-0000-0000-000000000003");
        var subContratoMensal = Guid.Parse("20000000-0000-0000-0000-000000000004");
        var subMercado = Guid.Parse("20000000-0000-0000-0000-000000000006");
        var subRestaurante = Guid.Parse("20000000-0000-0000-0000-000000000009");
        var subCombustivel = Guid.Parse("20000000-0000-0000-0000-000000000010");
        var subUber = Guid.Parse("20000000-0000-0000-0000-000000000013");
        var subPlanoSaude = Guid.Parse("20000000-0000-0000-0000-000000000019");
        var subConsulta = Guid.Parse("20000000-0000-0000-0000-000000000020");
        var subRoupa = Guid.Parse("20000000-0000-0000-0000-000000000024");
        var subEletronicos = Guid.Parse("20000000-0000-0000-0000-000000000025");
        var subCinema = Guid.Parse("20000000-0000-0000-0000-000000000031");
        var subStreaming = Guid.Parse("20000000-0000-0000-0000-000000000033");
        var subViagens = Guid.Parse("20000000-0000-0000-0000-000000000035");

        // Generate transactions for last 3 months
        for (int mesOffset = 0; mesOffset < 3; mesOffset++)
        {
            var mesBase = hoje.AddMonths(-mesOffset);
            var ano = mesBase.Year;
            var mes = mesBase.Month;

            // Receitas (income)
            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 5),
                Tipo = LancamentoTipo.Receita,
                Valor = 8500m,
                ContaId = contaSalario.Id,
                CategoriaId = catRenda,
                SubcategoriaId = subContratoMensal,
                PessoaId = pessoa1.Id,
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 15),
                Tipo = LancamentoTipo.Receita,
                Valor = 2000m,
                ContaId = contaCorrente.Id,
                CategoriaId = catRenda,
                SubcategoriaId = subContratoMensal,
                PessoaId = pessoa2.Id,
                Confirmado = mesOffset > 0
            });

            // Despesas fixas (fixed expenses)
            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 10),
                Tipo = LancamentoTipo.Despesa,
                Valor = -180m,
                ContaId = contaCorrente.Id,
                CategoriaId = catContasCasa,
                SubcategoriaId = subEnergia,
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 10),
                Tipo = LancamentoTipo.Despesa,
                Valor = -95m,
                ContaId = contaCorrente.Id,
                CategoriaId = catContasCasa,
                SubcategoriaId = subAgua,
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 10),
                Tipo = LancamentoTipo.Despesa,
                Valor = -120m,
                ContaId = contaCorrente.Id,
                CategoriaId = catContasCasa,
                SubcategoriaId = subInternet,
                Confirmado = mesOffset > 0
            });

            // Despesas variáveis (variable expenses)
            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 8),
                Tipo = LancamentoTipo.Despesa,
                Valor = -850m,
                ContaId = contaCorrente.Id,
                CategoriaId = catAlimentacao,
                SubcategoriaId = subMercado,
                PessoaId = pessoa1.Id,
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 12),
                Tipo = LancamentoTipo.Despesa,
                Valor = -150m,
                ContaId = contaCorrente.Id,
                CategoriaId = catAlimentacao,
                SubcategoriaId = subRestaurante,
                PessoaId = pessoa2.Id,
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 5),
                Tipo = LancamentoTipo.Despesa,
                Valor = -250m,
                ContaId = contaCorrente.Id,
                CategoriaId = catTransporte,
                SubcategoriaId = subCombustivel,
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 18),
                Tipo = LancamentoTipo.Despesa,
                Valor = -80m,
                ContaId = contaCorrente.Id,
                CategoriaId = catTransporte,
                SubcategoriaId = subUber,
                Confirmado = mesOffset > 0
            });

            // Despesas de cartão (credit card expenses)
            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 3),
                Tipo = LancamentoTipo.Despesa,
                Valor = -450m,
                CartaoCreditoId = cartaoNubank.Id,
                CategoriaId = catCompras,
                SubcategoriaId = subRoupa,
                DataVencimentoCartao = new DateOnly(ano, mes, 10),
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 7),
                Tipo = LancamentoTipo.Despesa,
                Valor = -1200m,
                CartaoCreditoId = cartaoNubank.Id,
                CategoriaId = catCompras,
                SubcategoriaId = subEletronicos,
                DataVencimentoCartao = new DateOnly(ano, mes, 10),
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 14),
                Tipo = LancamentoTipo.Despesa,
                Valor = -60m,
                CartaoCreditoId = cartaoItau.Id,
                CategoriaId = catLazer,
                SubcategoriaId = subStreaming,
                DataVencimentoCartao = new DateOnly(ano, mes, 25),
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 20),
                Tipo = LancamentoTipo.Despesa,
                Valor = -200m,
                CartaoCreditoId = cartaoItau.Id,
                CategoriaId = catLazer,
                SubcategoriaId = subCinema,
                DataVencimentoCartao = new DateOnly(ano, mes, 25),
                Confirmado = mesOffset > 0
            });

            // Despesas de saúde (health expenses)
            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 22),
                Tipo = LancamentoTipo.Despesa,
                Valor = -350m,
                ContaId = contaCorrente.Id,
                CategoriaId = catSaude,
                SubcategoriaId = subPlanoSaude,
                Confirmado = mesOffset > 0
            });

            lancamentos.Add(new Lancamento
            {
                Id = Guid.NewGuid(),
                Data = new DateOnly(ano, mes, 25),
                Tipo = LancamentoTipo.Despesa,
                Valor = -180m,
                ContaId = contaCorrente.Id,
                CategoriaId = catSaude,
                SubcategoriaId = subConsulta,
                PessoaId = pessoa3.Id,
                Confirmado = mesOffset > 0
            });
        }

        // Transferências (transfers)
        lancamentos.Add(new Lancamento
        {
            Id = Guid.NewGuid(),
            Data = hoje.AddDays(-15),
            Tipo = LancamentoTipo.Transferencia,
            Valor = -2000m,
            ContaId = contaCorrente.Id,
            CategoriaId = catFinanceiro,
            SubcategoriaId = Guid.Parse("20000000-0000-0000-0000-000000000043"),
            Confirmado = true
        });

        lancamentos.Add(new Lancamento
        {
            Id = Guid.NewGuid(),
            Data = hoje.AddDays(-15),
            Tipo = LancamentoTipo.Transferencia,
            Valor = 2000m,
            ContaId = contaPoupanca.Id,
            CategoriaId = catFinanceiro,
            SubcategoriaId = Guid.Parse("20000000-0000-0000-0000-000000000043"),
            Confirmado = true,
            ReferenciaId = lancamentos[^1].Id
        });

        _db.Lancamentos.AddRange(lancamentos);

        // 7. Provisões (provisions)
        var provisoes = new List<Provisao>
        {
            new Provisao
            {
                Id = Guid.NewGuid(),
                Onde = ProvisaoOnde.DebitoConta,
                Frequencia = ProvisaoFrequencia.Anual,
                Dia = 15,
                Valor = 2500m,
                ContaId = contaCorrente.Id,
                CategoriaId = catTransporte,
                SubcategoriaId = Guid.Parse("20000000-0000-0000-0000-000000000017")
            },
            new Provisao
            {
                Id = Guid.NewGuid(),
                Onde = ProvisaoOnde.DebitoConta,
                Frequencia = ProvisaoFrequencia.Anual,
                Dia = 1,
                Valor = 1800m,
                ContaId = contaPoupanca.Id,
                CategoriaId = catTransporte,
                SubcategoriaId = Guid.Parse("20000000-0000-0000-0000-000000000016")
            },
            new Provisao
            {
                Id = Guid.NewGuid(),
                Onde = ProvisaoOnde.DebitoConta,
                Frequencia = ProvisaoFrequencia.Anual,
                Dia = 15,
                Valor = 350m,
                ContaId = contaCorrente.Id,
                CategoriaId = catLazer,
                SubcategoriaId = subCinema
            }
        };
        _db.Provisoes.AddRange(provisoes);

        await _db.SaveChangesAsync();
    }
}
