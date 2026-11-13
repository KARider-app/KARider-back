-- =============================================================================
-- KARider · SPRINT 3 (HU-09): historial de ediciones de viajes y contador de ediciones.
-- NO es necesario ejecutarlo a mano si la API arranca con Database__AplicarMigracionesAlIniciar=true
-- o si se usa `dotnet ef database update`. Ejecutar después de sprint2_reservas_calificaciones.sql y de la
-- migración 20261023185057_Sprint2_IndiceDestinoViajes (índice ix_viajes_destino).
-- Equivale a las migraciones de EF Core:
--   20261023200000_Sprint3_HistorialEdicionesViaje   (creación de tabla)
--   20261023200100_Sprint3_NumeroEdicionesViaje      (adición de columna)
--   20261023200200_Sprint3_RestriccionNumeroEdiciones (restricción CHECK)
-- Regenerar con: dotnet ef migrations script Sprint2_ReservasCalificacionesYNotificaciones -p src/KARider.Infrastructure -s src/KARider.API
-- =============================================================================

START TRANSACTION;
CREATE TABLE historial_ediciones_viaje (
    id uuid NOT NULL,
    viaje_id uuid NOT NULL,
    fecha_salida_anterior timestamp with time zone NOT NULL,
    fecha_salida_nueva timestamp with time zone NOT NULL,
    punto_encuentro_anterior_id integer NOT NULL,
    punto_encuentro_nuevo_id integer NOT NULL,
    cupos_libres_anteriores integer NOT NULL,
    cupos_libres_nuevos integer NOT NULL,
    mensaje_aviso character varying(500),
    creado_en timestamp with time zone NOT NULL,
    actualizado_en timestamp with time zone,
    CONSTRAINT pk_historial_ediciones_viaje PRIMARY KEY (id),
    CONSTRAINT fk_historial_ediciones_viaje_viajes_viaje_id FOREIGN KEY (viaje_id) REFERENCES viajes (id) ON DELETE CASCADE
);

CREATE INDEX ix_historial_ediciones_viaje_viaje_id_creado_en ON historial_ediciones_viaje (viaje_id, creado_en);

INSERT INTO __ef_migrations_history (migration_id, product_version)
VALUES ('20261023200000_Sprint3_HistorialEdicionesViaje', '9.0.19');

ALTER TABLE viajes ADD numero_ediciones integer NOT NULL DEFAULT 0;

INSERT INTO __ef_migrations_history (migration_id, product_version)
VALUES ('20261023200100_Sprint3_NumeroEdicionesViaje', '9.0.19');

ALTER TABLE viajes ADD CONSTRAINT ck_viajes_numero_ediciones CHECK (numero_ediciones >= 0);

INSERT INTO __ef_migrations_history (migration_id, product_version)
VALUES ('20261023200200_Sprint3_RestriccionNumeroEdiciones', '9.0.19');

COMMIT;
