using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class ImpostoResgate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Imposto",
                table: "InvestimentosMovimentos",
                type: "decimal(65,30)",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LancamentoImpostoId",
                table: "InvestimentosMovimentos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.InsertData(
                table: "Subcategorias",
                columns: new[] { "Id", "CategoriaId", "IsProtected", "Nome" },
                values: new object[] { new Guid("20000000-0000-0000-0000-000000000060"), new Guid("10000000-0000-0000-0000-000000000011"), true, "Imposto de resgate" });

            migrationBuilder.CreateIndex(
                name: "IX_InvestimentosMovimentos_LancamentoImpostoId",
                table: "InvestimentosMovimentos",
                column: "LancamentoImpostoId");

            migrationBuilder.AddForeignKey(
                name: "FK_InvestimentosMovimentos_Lancamentos_LancamentoImpostoId",
                table: "InvestimentosMovimentos",
                column: "LancamentoImpostoId",
                principalTable: "Lancamentos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InvestimentosMovimentos_Lancamentos_LancamentoImpostoId",
                table: "InvestimentosMovimentos");

            migrationBuilder.DropIndex(
                name: "IX_InvestimentosMovimentos_LancamentoImpostoId",
                table: "InvestimentosMovimentos");

            migrationBuilder.DeleteData(
                table: "Subcategorias",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000060"));

            migrationBuilder.DropColumn(
                name: "Imposto",
                table: "InvestimentosMovimentos");

            migrationBuilder.DropColumn(
                name: "LancamentoImpostoId",
                table: "InvestimentosMovimentos");
        }
    }
}
