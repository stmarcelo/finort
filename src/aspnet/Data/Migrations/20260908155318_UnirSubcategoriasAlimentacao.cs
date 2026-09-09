using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Finort.Migrations
{
    /// <inheritdoc />
    public partial class UnirSubcategoriasAlimentacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Rename Mercado to merged name
            migrationBuilder.Sql(@"
                UPDATE Subcategorias 
                SET Nome = 'Mercado / Feira / Açougue' 
                WHERE Id = '20000000-0000-0000-0000-000000000006'");

            // Delete Açougue only if no launches reference it
            migrationBuilder.Sql(@"
                DELETE FROM Subcategorias 
                WHERE Id = '20000000-0000-0000-0000-000000000007' 
                AND NOT EXISTS (SELECT 1 FROM Lancamentos WHERE SubcategoriaId = '20000000-0000-0000-0000-000000000007')");

            // Delete Feira only if no launches reference it
            migrationBuilder.Sql(@"
                DELETE FROM Subcategorias 
                WHERE Id = '20000000-0000-0000-0000-000000000008' 
                AND NOT EXISTS (SELECT 1 FROM Lancamentos WHERE SubcategoriaId = '20000000-0000-0000-0000-000000000008')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Subcategorias",
                keyColumn: "Id",
                keyValue: new Guid("20000000-0000-0000-0000-000000000006"),
                column: "Nome",
                value: "Mercado");

            migrationBuilder.InsertData(
                table: "Subcategorias",
                columns: new[] { "Id", "CategoriaId", "IsProtected", "Nome" },
                values: new object[,]
                {
                    { new Guid("20000000-0000-0000-0000-000000000007"), new Guid("10000000-0000-0000-0000-000000000003"), false, "Açougue" },
                    { new Guid("20000000-0000-0000-0000-000000000008"), new Guid("10000000-0000-0000-0000-000000000003"), false, "Feira" }
                });
        }
    }
}
