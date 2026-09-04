using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Finort.Migrations
{
    /// <inheritdoc />
    public partial class AdicionarDiasAntecipacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DiasAntecipacao",
                table: "Configuracoes",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DiasAntecipacao",
                table: "Configuracoes");
        }
    }
}
