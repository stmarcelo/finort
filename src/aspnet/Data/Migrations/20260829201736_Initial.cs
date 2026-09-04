using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finort.Migrations
{
    /// <inheritdoc />
    public partial class Initial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AuditoriasExclusaoInvestimento",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    NomeInvestimento = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    ValorCotaAtual = table.Column<decimal>(type: "TEXT", nullable: false),
                    DataCotacao = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataExclusao = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditoriasExclusaoInvestimento", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Categorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    IsProtected = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Categorias", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Configuracoes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Email = table.Column<string>(type: "TEXT", nullable: false),
                    SenhaHash = table.Column<string>(type: "TEXT", nullable: false),
                    SmtpHost = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPort = table.Column<int>(type: "INTEGER", nullable: true),
                    SmtpUser = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpPassword = table.Column<string>(type: "TEXT", nullable: true),
                    SmtpFrom = table.Column<string>(type: "TEXT", nullable: true),
                    BackupPasswordCriptografada = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Configuracoes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Contas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Banco = table.Column<string>(type: "TEXT", nullable: true),
                    Agencia = table.Column<string>(type: "TEXT", nullable: true),
                    ContaEDigito = table.Column<string>(type: "TEXT", nullable: true),
                    Nome = table.Column<string>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MesesFechados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Mes = table.Column<int>(type: "INTEGER", nullable: false),
                    Ano = table.Column<int>(type: "INTEGER", nullable: false),
                    DataFechamento = table.Column<DateTime>(type: "TEXT", nullable: false),
                    SaldoAcumulado = table.Column<decimal>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MesesFechados", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pessoas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    CorDeExibicao = table.Column<string>(type: "TEXT", nullable: true),
                    Observacao = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pessoas", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Subcategorias",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    IsProtected = table.Column<bool>(type: "INTEGER", nullable: false),
                    CategoriaId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Subcategorias", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Subcategorias_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PasswordResetTokens",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    TokenHash = table.Column<string>(type: "TEXT", nullable: false),
                    ConfiguracaoId = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UsedAt = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PasswordResetTokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PasswordResetTokens_Configuracoes_ConfiguracaoId",
                        column: x => x.ConfiguracaoId,
                        principalTable: "Configuracoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "CartoesCredito",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Banco = table.Column<string>(type: "TEXT", nullable: false),
                    Ultimos4Digitos = table.Column<string>(type: "TEXT", maxLength: 4, nullable: false),
                    MelhorDiaCompra = table.Column<int>(type: "INTEGER", nullable: false),
                    DiaVencimento = table.Column<int>(type: "INTEGER", nullable: false),
                    Limite = table.Column<decimal>(type: "TEXT", nullable: false),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContaId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CartoesCredito", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CartoesCredito_Contas_ContaId",
                        column: x => x.ContaId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Investimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Nome = table.Column<string>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    ContaVinculadaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Subtipo = table.Column<string>(type: "TEXT", nullable: true),
                    Descricao = table.Column<string>(type: "TEXT", nullable: true),
                    ValorCotaAtual = table.Column<decimal>(type: "TEXT", nullable: false),
                    DataCotacao = table.Column<DateTime>(type: "TEXT", nullable: true),
                    DataVencimento = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Ativo = table.Column<bool>(type: "INTEGER", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Investimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Investimentos_Contas_ContaVinculadaId",
                        column: x => x.ContaVinculadaId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lembretes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    PessoaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Texto = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Dia = table.Column<int>(type: "INTEGER", nullable: true),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lembretes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lembretes_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Projetos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Descricao = table.Column<string>(type: "TEXT", nullable: false),
                    DataContratacao = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    ValorContratado = table.Column<decimal>(type: "TEXT", nullable: false),
                    Concluido = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataConclusao = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    PessoaId = table.Column<Guid>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Projetos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Projetos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Faturas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CartaoCreditoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AnoReferencia = table.Column<int>(type: "INTEGER", nullable: false),
                    MesReferencia = table.Column<int>(type: "INTEGER", nullable: false),
                    ValorTotal = table.Column<decimal>(type: "TEXT", nullable: false),
                    Fechada = table.Column<bool>(type: "INTEGER", nullable: false),
                    DataFechamento = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Faturas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Faturas_CartoesCredito_CartaoCreditoId",
                        column: x => x.CartaoCreditoId,
                        principalTable: "CartoesCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Provisoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Onde = table.Column<int>(type: "INTEGER", nullable: false),
                    Frequencia = table.Column<int>(type: "INTEGER", nullable: false),
                    Dia = table.Column<int>(type: "INTEGER", nullable: false),
                    PessoaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ContaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CartaoCreditoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    ValorVariante = table.Column<bool>(type: "INTEGER", nullable: false),
                    CategoriaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubcategoriaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    UltimoMesLancado = table.Column<int>(type: "INTEGER", nullable: true),
                    UltimoAnoLancado = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Provisoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Provisoes_CartoesCredito_CartaoCreditoId",
                        column: x => x.CartaoCreditoId,
                        principalTable: "CartoesCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Provisoes_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Provisoes_Contas_ContaId",
                        column: x => x.ContaId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Provisoes_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Provisoes_Subcategorias_SubcategoriaId",
                        column: x => x.SubcategoriaId,
                        principalTable: "Subcategorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Lancamentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    ContaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CategoriaId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SubcategoriaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    PessoaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    Confirmado = table.Column<bool>(type: "INTEGER", nullable: false),
                    ReferenciaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    CartaoCreditoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    DataCompra = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    DataVencimentoCartao = table.Column<DateOnly>(type: "TEXT", nullable: true),
                    ParcelamentoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ParcelaAtual = table.Column<int>(type: "INTEGER", nullable: true),
                    TotalParcelas = table.Column<int>(type: "INTEGER", nullable: true),
                    RecorrenciaId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ReembolsoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProvisaoId = table.Column<Guid>(type: "TEXT", nullable: true),
                    ProjetoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Lancamentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Lancamentos_CartoesCredito_CartaoCreditoId",
                        column: x => x.CartaoCreditoId,
                        principalTable: "CartoesCredito",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Categorias_CategoriaId",
                        column: x => x.CategoriaId,
                        principalTable: "Categorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Contas_ContaId",
                        column: x => x.ContaId,
                        principalTable: "Contas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Pessoas_PessoaId",
                        column: x => x.PessoaId,
                        principalTable: "Pessoas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Projetos_ProjetoId",
                        column: x => x.ProjetoId,
                        principalTable: "Projetos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Lancamentos_Subcategorias_SubcategoriaId",
                        column: x => x.SubcategoriaId,
                        principalTable: "Subcategorias",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestimentosMovimentos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvestimentoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    Quantidade = table.Column<decimal>(type: "TEXT", nullable: true),
                    ValorPorCota = table.Column<decimal>(type: "TEXT", nullable: true),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    LancamentoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestimentosMovimentos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestimentosMovimentos_Investimentos_InvestimentoId",
                        column: x => x.InvestimentoId,
                        principalTable: "Investimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestimentosMovimentos_Lancamentos_LancamentoId",
                        column: x => x.LancamentoId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "InvestimentosProventos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    InvestimentoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Data = table.Column<DateOnly>(type: "TEXT", nullable: false),
                    Valor = table.Column<decimal>(type: "TEXT", nullable: false),
                    Tipo = table.Column<int>(type: "INTEGER", nullable: false),
                    LancamentoId = table.Column<Guid>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvestimentosProventos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvestimentosProventos_Investimentos_InvestimentoId",
                        column: x => x.InvestimentoId,
                        principalTable: "Investimentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InvestimentosProventos_Lancamentos_LancamentoId",
                        column: x => x.LancamentoId,
                        principalTable: "Lancamentos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "Categorias",
                columns: new[] { "Id", "IsProtected", "Nome" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000001"), false, "Contas de casa" },
                    { new Guid("10000000-0000-0000-0000-000000000002"), false, "Renda" },
                    { new Guid("10000000-0000-0000-0000-000000000003"), false, "Alimentação" },
                    { new Guid("10000000-0000-0000-0000-000000000004"), false, "Transporte" },
                    { new Guid("10000000-0000-0000-0000-000000000005"), false, "Saúde" },
                    { new Guid("10000000-0000-0000-0000-000000000006"), false, "Compras" },
                    { new Guid("10000000-0000-0000-0000-000000000007"), false, "Educação" },
                    { new Guid("10000000-0000-0000-0000-000000000008"), false, "Lazer" },
                    { new Guid("10000000-0000-0000-0000-000000000009"), true, "Financeiro" },
                    { new Guid("10000000-0000-0000-0000-000000000010"), false, "Familia" },
                    { new Guid("10000000-0000-0000-0000-000000000011"), false, "Impostos" },
                    { new Guid("10000000-0000-0000-0000-000000000012"), false, "Investimento" },
                    { new Guid("10000000-0000-0000-0000-000000000013"), true, "Acerto de saldo" }
                });

            migrationBuilder.InsertData(
                table: "Subcategorias",
                columns: new[] { "Id", "CategoriaId", "IsProtected", "Nome" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000001"), new Guid("10000000-0000-0000-0000-000000000001"), false, "Energia" },
                    { new Guid("20000000-0000-0000-0000-000000000002"), new Guid("10000000-0000-0000-0000-000000000001"), false, "Água" },
                    { new Guid("20000000-0000-0000-0000-000000000003"), new Guid("10000000-0000-0000-0000-000000000001"), false, "Internet" },
                    { new Guid("20000000-0000-0000-0000-000000000004"), new Guid("10000000-0000-0000-0000-000000000002"), false, "Contrato mensal" },
                    { new Guid("20000000-0000-0000-0000-000000000005"), new Guid("10000000-0000-0000-0000-000000000002"), false, "Extra" },
                    { new Guid("20000000-0000-0000-0000-000000000006"), new Guid("10000000-0000-0000-0000-000000000003"), false, "Mercado" },
                    { new Guid("20000000-0000-0000-0000-000000000007"), new Guid("10000000-0000-0000-0000-000000000003"), false, "Açougue" },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000003"), false, "Feira" },
                    { new Guid("20000000-0000-0000-0000-000000000009"), new Guid("10000000-0000-0000-0000-000000000003"), false, "Restaurante" },
                    { new Guid("20000000-0000-0000-0000-000000000010"), new Guid("10000000-0000-0000-0000-000000000004"), false, "Combustível" },
                    { new Guid("20000000-0000-0000-0000-000000000011"), new Guid("10000000-0000-0000-0000-000000000004"), false, "Estacionamento" },
                    { new Guid("20000000-0000-0000-0000-000000000012"), new Guid("10000000-0000-0000-0000-000000000004"), false, "Transporte público" },
                    { new Guid("20000000-0000-0000-0000-000000000013"), new Guid("10000000-0000-0000-0000-000000000004"), false, "Uber/99/Taxi" },
                    { new Guid("20000000-0000-0000-0000-000000000014"), new Guid("10000000-0000-0000-0000-000000000004"), false, "Pedágio" },
                    { new Guid("20000000-0000-0000-0000-000000000015"), new Guid("10000000-0000-0000-0000-000000000004"), false, "Manutenção" },
                    { new Guid("20000000-0000-0000-0000-000000000016"), new Guid("10000000-0000-0000-0000-000000000004"), false, "Seguro" },
                    { new Guid("20000000-0000-0000-0000-000000000017"), new Guid("10000000-0000-0000-0000-000000000004"), false, "IPVA/Licenciamento" },
                    { new Guid("20000000-0000-0000-0000-000000000018"), new Guid("10000000-0000-0000-0000-000000000004"), false, "Financiamento" },
                    { new Guid("20000000-0000-0000-0000-000000000019"), new Guid("10000000-0000-0000-0000-000000000005"), false, "Plano de saúde" },
                    { new Guid("20000000-0000-0000-0000-000000000020"), new Guid("10000000-0000-0000-0000-000000000005"), false, "Consulta" },
                    { new Guid("20000000-0000-0000-0000-000000000021"), new Guid("10000000-0000-0000-0000-000000000005"), false, "Exame" },
                    { new Guid("20000000-0000-0000-0000-000000000022"), new Guid("10000000-0000-0000-0000-000000000005"), false, "Medicamento" },
                    { new Guid("20000000-0000-0000-0000-000000000023"), new Guid("10000000-0000-0000-0000-000000000005"), false, "Dentista" },
                    { new Guid("20000000-0000-0000-0000-000000000024"), new Guid("10000000-0000-0000-0000-000000000006"), false, "Roupa/Calçado" },
                    { new Guid("20000000-0000-0000-0000-000000000025"), new Guid("10000000-0000-0000-0000-000000000006"), false, "Eletrônicos" },
                    { new Guid("20000000-0000-0000-0000-000000000026"), new Guid("10000000-0000-0000-0000-000000000006"), false, "Presente" },
                    { new Guid("20000000-0000-0000-0000-000000000027"), new Guid("10000000-0000-0000-0000-000000000006"), false, "Outro" },
                    { new Guid("20000000-0000-0000-0000-000000000028"), new Guid("10000000-0000-0000-0000-000000000007"), false, "Curso/Faculdade/Escola" },
                    { new Guid("20000000-0000-0000-0000-000000000029"), new Guid("10000000-0000-0000-0000-000000000007"), false, "Livro/Material" },
                    { new Guid("20000000-0000-0000-0000-000000000030"), new Guid("10000000-0000-0000-0000-000000000007"), false, "Certificação" },
                    { new Guid("20000000-0000-0000-0000-000000000031"), new Guid("10000000-0000-0000-0000-000000000008"), false, "Cinema/Teatro" },
                    { new Guid("20000000-0000-0000-0000-000000000032"), new Guid("10000000-0000-0000-0000-000000000008"), false, "Shows/eventos" },
                    { new Guid("20000000-0000-0000-0000-000000000033"), new Guid("10000000-0000-0000-0000-000000000008"), false, "Streaming" },
                    { new Guid("20000000-0000-0000-0000-000000000034"), new Guid("10000000-0000-0000-0000-000000000008"), false, "Jogos" },
                    { new Guid("20000000-0000-0000-0000-000000000035"), new Guid("10000000-0000-0000-0000-000000000008"), false, "Viagens" },
                    { new Guid("20000000-0000-0000-0000-000000000036"), new Guid("10000000-0000-0000-0000-000000000008"), false, "Outros" },
                    { new Guid("20000000-0000-0000-0000-000000000037"), new Guid("10000000-0000-0000-0000-000000000009"), false, "Anuidades/Tarifas" },
                    { new Guid("20000000-0000-0000-0000-000000000038"), new Guid("10000000-0000-0000-0000-000000000009"), false, "Juros" },
                    { new Guid("20000000-0000-0000-0000-000000000039"), new Guid("10000000-0000-0000-0000-000000000009"), false, "IOF" },
                    { new Guid("20000000-0000-0000-0000-000000000040"), new Guid("10000000-0000-0000-0000-000000000009"), false, "Empréstimo" },
                    { new Guid("20000000-0000-0000-0000-000000000041"), new Guid("10000000-0000-0000-0000-000000000009"), false, "Financiamento" },
                    { new Guid("20000000-0000-0000-0000-000000000042"), new Guid("10000000-0000-0000-0000-000000000009"), false, "Pagamento de dívida" },
                    { new Guid("20000000-0000-0000-0000-000000000043"), new Guid("10000000-0000-0000-0000-000000000009"), true, "Transferência" },
                    { new Guid("20000000-0000-0000-0000-000000000044"), new Guid("10000000-0000-0000-0000-000000000010"), false, "Filho" },
                    { new Guid("20000000-0000-0000-0000-000000000045"), new Guid("10000000-0000-0000-0000-000000000010"), false, "Conjugue" },
                    { new Guid("20000000-0000-0000-0000-000000000046"), new Guid("10000000-0000-0000-0000-000000000010"), false, "Pensão" },
                    { new Guid("20000000-0000-0000-0000-000000000047"), new Guid("10000000-0000-0000-0000-000000000010"), false, "Outro" },
                    { new Guid("20000000-0000-0000-0000-000000000048"), new Guid("10000000-0000-0000-0000-000000000011"), false, "Imposto de renda" },
                    { new Guid("20000000-0000-0000-0000-000000000049"), new Guid("10000000-0000-0000-0000-000000000011"), false, "IPTU" },
                    { new Guid("20000000-0000-0000-0000-000000000050"), new Guid("10000000-0000-0000-0000-000000000011"), false, "Multas" },
                    { new Guid("20000000-0000-0000-0000-000000000051"), new Guid("10000000-0000-0000-0000-000000000011"), false, "Taxas" },
                    { new Guid("20000000-0000-0000-0000-000000000052"), new Guid("10000000-0000-0000-0000-000000000011"), false, "Outros impostos" },
                    { new Guid("20000000-0000-0000-0000-000000000053"), new Guid("10000000-0000-0000-0000-000000000012"), false, "Dividendos / Rendimentos" },
                    { new Guid("20000000-0000-0000-0000-000000000054"), new Guid("10000000-0000-0000-0000-000000000012"), false, "Compra/Aporte" },
                    { new Guid("20000000-0000-0000-0000-000000000055"), new Guid("10000000-0000-0000-0000-000000000012"), false, "Venda / Resgate" },
                    { new Guid("20000000-0000-0000-0000-000000000056"), new Guid("10000000-0000-0000-0000-000000000013"), true, "Acerto" },
                    { new Guid("20000000-0000-0000-0000-000000000057"), new Guid("10000000-0000-0000-0000-000000000009"), true, "Cartão de crédito" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_CartoesCredito_ContaId",
                table: "CartoesCredito",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_Faturas_CartaoCreditoId_AnoReferencia_MesReferencia",
                table: "Faturas",
                columns: new[] { "CartaoCreditoId", "AnoReferencia", "MesReferencia" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Investimentos_ContaVinculadaId",
                table: "Investimentos",
                column: "ContaVinculadaId");

            migrationBuilder.CreateIndex(
                name: "IX_Investimentos_Nome",
                table: "Investimentos",
                column: "Nome",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InvestimentosMovimentos_InvestimentoId",
                table: "InvestimentosMovimentos",
                column: "InvestimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestimentosMovimentos_LancamentoId",
                table: "InvestimentosMovimentos",
                column: "LancamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestimentosProventos_InvestimentoId",
                table: "InvestimentosProventos",
                column: "InvestimentoId");

            migrationBuilder.CreateIndex(
                name: "IX_InvestimentosProventos_LancamentoId",
                table: "InvestimentosProventos",
                column: "LancamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_CartaoCreditoId",
                table: "Lancamentos",
                column: "CartaoCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_CategoriaId",
                table: "Lancamentos",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ContaId",
                table: "Lancamentos",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_Data",
                table: "Lancamentos",
                column: "Data");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ParcelamentoId",
                table: "Lancamentos",
                column: "ParcelamentoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_PessoaId",
                table: "Lancamentos",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ProjetoId",
                table: "Lancamentos",
                column: "ProjetoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ProvisaoId",
                table: "Lancamentos",
                column: "ProvisaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_RecorrenciaId",
                table: "Lancamentos",
                column: "RecorrenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ReferenciaId",
                table: "Lancamentos",
                column: "ReferenciaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_SubcategoriaId",
                table: "Lancamentos",
                column: "SubcategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lembretes_PessoaId",
                table: "Lembretes",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_MesesFechados_Mes_Ano",
                table: "MesesFechados",
                columns: new[] { "Mes", "Ano" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PasswordResetTokens_ConfiguracaoId",
                table: "PasswordResetTokens",
                column: "ConfiguracaoId");

            migrationBuilder.CreateIndex(
                name: "IX_Projetos_PessoaId",
                table: "Projetos",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_Provisoes_CartaoCreditoId",
                table: "Provisoes",
                column: "CartaoCreditoId");

            migrationBuilder.CreateIndex(
                name: "IX_Provisoes_CategoriaId",
                table: "Provisoes",
                column: "CategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Provisoes_ContaId",
                table: "Provisoes",
                column: "ContaId");

            migrationBuilder.CreateIndex(
                name: "IX_Provisoes_PessoaId",
                table: "Provisoes",
                column: "PessoaId");

            migrationBuilder.CreateIndex(
                name: "IX_Provisoes_SubcategoriaId",
                table: "Provisoes",
                column: "SubcategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Subcategorias_CategoriaId",
                table: "Subcategorias",
                column: "CategoriaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AuditoriasExclusaoInvestimento");

            migrationBuilder.DropTable(
                name: "Faturas");

            migrationBuilder.DropTable(
                name: "InvestimentosMovimentos");

            migrationBuilder.DropTable(
                name: "InvestimentosProventos");

            migrationBuilder.DropTable(
                name: "Lembretes");

            migrationBuilder.DropTable(
                name: "MesesFechados");

            migrationBuilder.DropTable(
                name: "PasswordResetTokens");

            migrationBuilder.DropTable(
                name: "Provisoes");

            migrationBuilder.DropTable(
                name: "Investimentos");

            migrationBuilder.DropTable(
                name: "Lancamentos");

            migrationBuilder.DropTable(
                name: "Configuracoes");

            migrationBuilder.DropTable(
                name: "CartoesCredito");

            migrationBuilder.DropTable(
                name: "Projetos");

            migrationBuilder.DropTable(
                name: "Subcategorias");

            migrationBuilder.DropTable(
                name: "Contas");

            migrationBuilder.DropTable(
                name: "Pessoas");

            migrationBuilder.DropTable(
                name: "Categorias");
        }
    }
}
