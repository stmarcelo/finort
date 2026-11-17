using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class Prazo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "Prazo",
                table: "Projetos",
                type: "date",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Prazo",
                table: "Projetos");
        }
    }
}
