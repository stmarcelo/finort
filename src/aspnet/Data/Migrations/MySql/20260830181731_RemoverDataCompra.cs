using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Data.Migrations.MySql
{
    /// <inheritdoc />
    public partial class RemoverDataCompra : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DataCompra",
                table: "Lancamentos");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateOnly>(
                name: "DataCompra",
                table: "Lancamentos",
                type: "date",
                nullable: true);
        }
    }
}
