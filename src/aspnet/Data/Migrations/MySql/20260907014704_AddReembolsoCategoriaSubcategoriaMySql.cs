using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AddReembolsoCategoriaSubcategoriaMySql : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReembolsoCategoriaId",
                table: "Lancamentos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ReembolsoSubcategoriaId",
                table: "Lancamentos",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AlterColumn<string>(
                name: "Ultimos4Digitos",
                table: "CartoesCredito",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(4)",
                oldMaxLength: 4)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ReembolsoCategoriaId",
                table: "Lancamentos",
                column: "ReembolsoCategoriaId");

            migrationBuilder.CreateIndex(
                name: "IX_Lancamentos_ReembolsoSubcategoriaId",
                table: "Lancamentos",
                column: "ReembolsoSubcategoriaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Lancamentos_Categorias_ReembolsoCategoriaId",
                table: "Lancamentos",
                column: "ReembolsoCategoriaId",
                principalTable: "Categorias",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Lancamentos_Subcategorias_ReembolsoSubcategoriaId",
                table: "Lancamentos",
                column: "ReembolsoSubcategoriaId",
                principalTable: "Subcategorias",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Lancamentos_Categorias_ReembolsoCategoriaId",
                table: "Lancamentos");

            migrationBuilder.DropForeignKey(
                name: "FK_Lancamentos_Subcategorias_ReembolsoSubcategoriaId",
                table: "Lancamentos");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_ReembolsoCategoriaId",
                table: "Lancamentos");

            migrationBuilder.DropIndex(
                name: "IX_Lancamentos_ReembolsoSubcategoriaId",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "ReembolsoCategoriaId",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "ReembolsoSubcategoriaId",
                table: "Lancamentos");

            migrationBuilder.AlterColumn<string>(
                name: "Ultimos4Digitos",
                table: "CartoesCredito",
                type: "varchar(4)",
                maxLength: 4,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20)
                .Annotation("MySql:CharSet", "utf8mb4")
                .OldAnnotation("MySql:CharSet", "utf8mb4");
        }
    }
}
