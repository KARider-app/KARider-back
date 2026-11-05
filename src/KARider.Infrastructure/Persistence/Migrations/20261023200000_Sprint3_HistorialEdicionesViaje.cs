using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace KARider.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Sprint3_HistorialEdicionesViaje : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "historial_ediciones_viaje",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    viaje_id = table.Column<Guid>(type: "uuid", nullable: false),
                    fecha_salida_anterior = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    fecha_salida_nueva = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    punto_encuentro_anterior_id = table.Column<int>(type: "integer", nullable: false),
                    punto_encuentro_nuevo_id = table.Column<int>(type: "integer", nullable: false),
                    cupos_libres_anteriores = table.Column<int>(type: "integer", nullable: false),
                    cupos_libres_nuevos = table.Column<int>(type: "integer", nullable: false),
                    mensaje_aviso = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    creado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    actualizado_en = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_historial_ediciones_viaje", x => x.id);
                    table.ForeignKey(
                        name: "fk_historial_ediciones_viaje_viajes_viaje_id",
                        column: x => x.viaje_id,
                        principalTable: "viajes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_historial_ediciones_viaje_viaje_id_creado_en",
                table: "historial_ediciones_viaje",
                columns: new[] { "viaje_id", "creado_en" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "historial_ediciones_viaje");
        }
    }
}
