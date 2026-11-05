using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KARider.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_RestriccionNumeroEdiciones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddCheckConstraint(
                name: "ck_viajes_numero_ediciones",
                table: "viajes",
                sql: "numero_ediciones >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_viajes_numero_ediciones",
                table: "viajes");
        }
    }
}
