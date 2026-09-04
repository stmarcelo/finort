using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Migrations
{
    /// <inheritdoc />
    public partial class RenomearRendaParaReceita : Migration
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
        }
    }
}
