using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KARider.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint4_CoordenadasRealesPuntosEncuentro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "puntos_encuentro",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "latitud", "longitud" },
                values: new object[] { 20.009969999999999, -99.342500000000001 });

            migrationBuilder.UpdateData(
                table: "puntos_encuentro",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "latitud", "longitud" },
                values: new object[] { 20.00995, -99.343950000000007 });

            migrationBuilder.UpdateData(
                table: "puntos_encuentro",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "latitud", "longitud" },
                values: new object[] { 20.009820000000001, -99.342849999999999 });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "puntos_encuentro",
                keyColumn: "id",
                keyValue: 1,
                columns: new[] { "latitud", "longitud" },
                values: new object[] { 20.087499999999999, -99.347999999999999 });

            migrationBuilder.UpdateData(
                table: "puntos_encuentro",
                keyColumn: "id",
                keyValue: 2,
                columns: new[] { "latitud", "longitud" },
                values: new object[] { 20.088100000000001, -99.347200000000001 });

            migrationBuilder.UpdateData(
                table: "puntos_encuentro",
                keyColumn: "id",
                keyValue: 3,
                columns: new[] { "latitud", "longitud" },
                values: new object[] { 20.087, -99.3489 });
        }
    }
}
