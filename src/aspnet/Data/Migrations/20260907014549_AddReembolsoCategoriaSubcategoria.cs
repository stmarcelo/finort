using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Migrations
{
    /// <inheritdoc />
    public partial class AddReembolsoCategoriaSubcategoria : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ReembolsoCategoriaId",
                table: "Lancamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReembolsoSubcategoriaId",
                table: "Lancamentos",
                type: "TEXT",
                nullable: true);

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
        }
    }
}
