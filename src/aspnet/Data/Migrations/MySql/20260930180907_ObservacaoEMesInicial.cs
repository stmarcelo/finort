using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class ObservacaoEMesInicial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "MesInicial",
                table: "Provisoes",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Observacao",
                table: "Lancamentos",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MesInicial",
                table: "Provisoes");

            migrationBuilder.DropColumn(
                name: "Observacao",
                table: "Lancamentos");
        }
    }
}
