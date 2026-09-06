using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finort.Migrations
{
    /// <inheritdoc />
    public partial class SeedDadosCompletos : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "Nome",
                value: "Receita");

            migrationBuilder.AddColumn<DateOnly>(
                name: "DataCompra",
                table: "Lancamentos",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "Limite",
                table: "Contas",
                type: "TEXT",
                nullable: true);

            migrationBuilder.InsertData(
                table: "Categorias",
                columns: new[] { "Id", "IsProtected", "Nome" },
                values: new object[,]
                {
                    { new Guid("10000000-0000-0000-0000-000000000014"), false, "Indefinida" },
                    { new Guid("10000000-0000-0000-0000-000000000015"), false, "Serviços" }
                });

            migrationBuilder.InsertData(
                table: "Subcategorias",
                columns: new[] { "Id", "CategoriaId", "IsProtected", "Nome" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000058"), new Guid("10000000-0000-0000-0000-000000000001"), false, "Manutenção" },
                    { new Guid("20000000-0000-0000-0000-000000000059"), new Guid("10000000-0000-0000-0000-000000000015"), false, "Assinaturas" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000002"),
                column: "Nome",
                value: "Renda");

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000014"));

            migrationBuilder.DeleteData(
                table: "Subcategorias",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000058"));

            migrationBuilder.DeleteData(
                table: "Subcategorias",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000059"));

            migrationBuilder.DeleteData(
                table: "Categorias",
                keyColumn: "Id",
                keyValue: new Guid("10000000-0000-0000-0000-000000000015"));

            migrationBuilder.DropColumn(
                name: "DataCompra",
                table: "Lancamentos");

            migrationBuilder.DropColumn(
                name: "Limite",
                table: "Contas");
        }
    }
}
