using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KARider.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_NumeroEdicionesViaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "numero_ediciones",
                table: "viajes",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "numero_ediciones",
                table: "viajes");
        }
    }
}
