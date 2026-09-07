using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Migrations
{
    /// <inheritdoc />
    public partial class AddContaIdToMesFechado : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MesesFechados_Mes_Ano",
                table: "MesesFechados");

            migrationBuilder.AddColumn<Guid>(
                name: "ContaId",
                table: "MesesFechados",
                type: "TEXT",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateIndex(
                name: "IX_MesesFechados_ContaId_Mes_Ano",
                table: "MesesFechados",
                columns: new[] { "ContaId", "Mes", "Ano" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_MesesFechados_ContaId_Mes_Ano",
                table: "MesesFechados");

            migrationBuilder.DropColumn(
                name: "ContaId",
                table: "MesesFechados");

            migrationBuilder.CreateIndex(
                name: "IX_MesesFechados_Mes_Ano",
                table: "MesesFechados",
                columns: new[] { "Mes", "Ano" },
                unique: true);
        }
    }
}
