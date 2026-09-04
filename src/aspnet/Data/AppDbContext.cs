using Finort.Models.Auth;
using Finort.Models.Financeiro;
using Microsoft.EntityFrameworkCore;

namespace Finort.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions options) : base(options) { }

    protected AppDbContext() { }

    public DbSet<Configuracao> Configuracoes => Set<Configuracao>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();

    public DbSet<Pessoa> Pessoas => Set<Pessoa>();
    public DbSet<Categoria> Categorias => Set<Categoria>();
    public DbSet<Subcategoria> Subcategorias => Set<Subcategoria>();
    public DbSet<Conta> Contas => Set<Conta>();
    public DbSet<Lancamento> Lancamentos => Set<Lancamento>();
    public DbSet<CartaoCredito> CartoesCredito => Set<CartaoCredito>();
    public DbSet<Fatura> Faturas => Set<Fatura>();
    public DbSet<Provisao> Provisoes => Set<Provisao>();
    public DbSet<MesFechado> MesesFechados => Set<MesFechado>();
    public DbSet<Projeto> Projetos => Set<Projeto>();
    public DbSet<Lembrete> Lembretes => Set<Lembrete>();

    public DbSet<Investimento> Investimentos => Set<Investimento>();
    public DbSet<InvestimentoProvento> InvestimentosProventos => Set<InvestimentoProvento>();
    public DbSet<AuditoriaExclusaoInvestimento> AuditoriasExclusaoInvestimento => Set<AuditoriaExclusaoInvestimento>();
    public DbSet<InvestimentoMovimento> InvestimentosMovimentos => Set<InvestimentoMovimento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<PasswordResetToken>()
            .HasOne(t => t.Configuracao)
            .WithMany()
            .HasForeignKey(t => t.ConfiguracaoId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<Subcategoria>()
            .HasOne(s => s.Categoria)
            .WithMany(c => c.Subcategorias)
            .HasForeignKey(s => s.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lancamento>()
            .HasOne(l => l.Conta)
            .WithMany()
            .HasForeignKey(l => l.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lancamento>()
            .HasOne(l => l.Categoria)
            .WithMany()
            .HasForeignKey(l => l.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lancamento>()
            .HasOne(l => l.Subcategoria)
            .WithMany()
            .HasForeignKey(l => l.SubcategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lancamento>()
            .HasOne(l => l.Pessoa)
            .WithMany()
            .HasForeignKey(l => l.PessoaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lancamento>()
            .HasIndex(l => l.Data);

        modelBuilder.Entity<Lancamento>()
            .HasIndex(l => l.ReferenciaId);

        modelBuilder.Entity<CartaoCredito>()
            .HasOne(c => c.Conta)
            .WithMany()
            .HasForeignKey(c => c.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<CartaoCredito>()
            .Property(c => c.Ultimos4Digitos)
            .HasMaxLength(4);

        modelBuilder.Entity<Fatura>()
            .HasOne(f => f.CartaoCredito)
            .WithMany()
            .HasForeignKey(f => f.CartaoCreditoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Fatura>()
            .HasIndex(f => new { f.CartaoCreditoId, f.AnoReferencia, f.MesReferencia })
            .IsUnique();

        modelBuilder.Entity<Provisao>()
            .HasOne(p => p.Pessoa)
            .WithMany()
            .HasForeignKey(p => p.PessoaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Provisao>()
            .HasOne(p => p.Conta)
            .WithMany()
            .HasForeignKey(p => p.ContaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Provisao>()
            .HasOne(p => p.CartaoCredito)
            .WithMany()
            .HasForeignKey(p => p.CartaoCreditoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Provisao>()
            .HasOne(p => p.Categoria)
            .WithMany()
            .HasForeignKey(p => p.CategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Provisao>()
            .HasOne(p => p.Subcategoria)
            .WithMany()
            .HasForeignKey(p => p.SubcategoriaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<MesFechado>()
            .HasIndex(m => new { m.Mes, m.Ano })
            .IsUnique();

        modelBuilder.Entity<Lancamento>()
            .HasOne(l => l.CartaoCredito)
            .WithMany()
            .HasForeignKey(l => l.CartaoCreditoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lancamento>()
            .HasIndex(l => l.ParcelamentoId);

        modelBuilder.Entity<Lancamento>()
            .HasIndex(l => l.RecorrenciaId);

        modelBuilder.Entity<Lancamento>()
            .HasIndex(l => l.ProvisaoId);

        modelBuilder.Entity<Investimento>()
            .HasOne(i => i.Conta)
            .WithMany()
            .HasForeignKey(i => i.ContaVinculadaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvestimentoProvento>()
            .HasOne(p => p.Investimento)
            .WithMany()
            .HasForeignKey(p => p.InvestimentoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvestimentoProvento>()
            .HasIndex(p => p.InvestimentoId);

        modelBuilder.Entity<InvestimentoProvento>()
            .HasOne(p => p.Lancamento)
            .WithMany()
            .HasForeignKey(p => p.LancamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvestimentoProvento>()
            .HasIndex(p => p.LancamentoId);

        modelBuilder.Entity<Investimento>()
            .HasIndex(i => new { i.Nome })
            .IsUnique();

        modelBuilder.Entity<InvestimentoMovimento>()
            .HasOne(m => m.Investimento)
            .WithMany()
            .HasForeignKey(m => m.InvestimentoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvestimentoMovimento>()
            .HasOne(m => m.Lancamento)
            .WithMany()
            .HasForeignKey(m => m.LancamentoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<InvestimentoMovimento>()
            .HasIndex(m => m.InvestimentoId);

        modelBuilder.Entity<InvestimentoMovimento>()
            .HasIndex(m => m.LancamentoId);

        modelBuilder.Entity<Projeto>()
            .HasOne(p => p.Pessoa)
            .WithMany()
            .HasForeignKey(p => p.PessoaId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lancamento>()
            .HasOne(l => l.Projeto)
            .WithMany()
            .HasForeignKey(l => l.ProjetoId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Lancamento>()
            .HasIndex(l => l.ProjetoId);

        modelBuilder.Entity<Lembrete>(entity =>
        {
            entity.HasIndex(e => e.PessoaId);
            entity.HasOne(e => e.Pessoa)
                .WithMany()
                .HasForeignKey(e => e.PessoaId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        SeedCategorias(modelBuilder);
    }

    private static void SeedCategorias(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Categoria>().HasData(
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000001"), Nome = "Contas de casa" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nome = "Receita" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nome = "Alimentação" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Transporte" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000005"), Nome = "Saúde" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000006"), Nome = "Compras" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000007"), Nome = "Educação" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000008"), Nome = "Lazer" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "Financeiro", IsProtected = true },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000010"), Nome = "Familia" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000011"), Nome = "Impostos" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000012"), Nome = "Investimento" },
            new Categoria { Id = Guid.Parse("10000000-0000-0000-0000-000000000013"), Nome = "Acerto de saldo", IsProtected = true });

        modelBuilder.Entity<Subcategoria>().HasData(
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000001"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000001"), Nome = "Energia" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000002"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000001"), Nome = "Água" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000003"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000001"), Nome = "Internet" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000004"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nome = "Contrato mensal" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000005"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000002"), Nome = "Extra" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000006"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nome = "Mercado" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000007"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nome = "Açougue" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000008"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nome = "Feira" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000009"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000003"), Nome = "Restaurante" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000010"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Combustível" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000011"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Estacionamento" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000012"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Transporte público" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000013"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Uber/99/Taxi" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000014"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Pedágio" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000015"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Manutenção" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000016"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Seguro" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000017"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "IPVA/Licenciamento" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000018"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000004"), Nome = "Financiamento" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000019"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000005"), Nome = "Plano de saúde" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000020"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000005"), Nome = "Consulta" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000021"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000005"), Nome = "Exame" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000022"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000005"), Nome = "Medicamento" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000023"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000005"), Nome = "Dentista" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000024"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000006"), Nome = "Roupa/Calçado" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000025"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000006"), Nome = "Eletrônicos" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000026"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000006"), Nome = "Presente" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000027"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000006"), Nome = "Outro" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000028"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000007"), Nome = "Curso/Faculdade/Escola" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000029"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000007"), Nome = "Livro/Material" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000030"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000007"), Nome = "Certificação" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000031"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000008"), Nome = "Cinema/Teatro" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000032"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000008"), Nome = "Shows/eventos" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000033"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000008"), Nome = "Streaming" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000034"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000008"), Nome = "Jogos" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000035"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000008"), Nome = "Viagens" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000036"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000008"), Nome = "Outros" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000037"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "Anuidades/Tarifas" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000038"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "Juros" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000039"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "IOF" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000040"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "Empréstimo" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000041"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "Financiamento" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000042"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "Pagamento de dívida" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000043"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "Transferência", IsProtected = true },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000044"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000010"), Nome = "Filho" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000045"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000010"), Nome = "Conjugue" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000046"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000010"), Nome = "Pensão" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000047"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000010"), Nome = "Outro" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000048"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000011"), Nome = "Imposto de renda" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000049"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000011"), Nome = "IPTU" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000050"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000011"), Nome = "Multas" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000051"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000011"), Nome = "Taxas" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000052"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000011"), Nome = "Outros impostos" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000053"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000012"), Nome = "Dividendos / Rendimentos" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000054"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000012"), Nome = "Compra/Aporte" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000055"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000012"), Nome = "Venda / Resgate" },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000056"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000013"), Nome = "Acerto", IsProtected = true },
            new Subcategoria { Id = Guid.Parse("20000000-0000-0000-0000-000000000057"), CategoriaId = Guid.Parse("10000000-0000-0000-0000-000000000009"), Nome = "Cartão de crédito", IsProtected = true });
    }
}
