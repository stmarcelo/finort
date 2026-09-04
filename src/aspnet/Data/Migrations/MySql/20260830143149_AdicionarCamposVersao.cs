using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class AdicionarCamposVersao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UltimaVerificacaoVersao",
                table: "Configuracoes",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VersaoConhecida",
                table: "Configuracoes",
                type: "longtext",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UltimaVerificacaoVersao",
                table: "Configuracoes");

            migrationBuilder.DropColumn(
                name: "VersaoConhecida",
                table: "Configuracoes");
        }
    }
}
