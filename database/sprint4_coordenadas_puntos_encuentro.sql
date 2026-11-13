-- =============================================================================
-- KARider · SPRINT 4 (HU-08): coordenadas reales de los puntos de encuentro del campus UTTT.
-- Las coordenadas sembradas en el Sprint 1 eran aproximadas y quedaban ~9 km al norte del campus, por lo que
-- el mapa, Google Maps y Waze llevaban a un lugar equivocado. Valores tomados de OpenStreetMap (contorno del
-- campus way 386380501, estacionamiento way 907217233 y accesos desde la Carretera Tula-Tepeji).
-- NO es necesario ejecutarlo a mano si la API arranca con Database__AplicarMigracionesAlIniciar=true
-- o si se usa `dotnet ef database update` (el pipeline de Azure aplica el bundle de migraciones).
-- Equivale a la migración de EF Core: 20261113055522_Sprint4_CoordenadasRealesPuntosEncuentro (actualización de datos)
-- =============================================================================

START TRANSACTION;
UPDATE puntos_encuentro SET latitud = 20.00997, longitud = -99.3425 WHERE id = 1;
UPDATE puntos_encuentro SET latitud = 20.00995, longitud = -99.34395 WHERE id = 2;
UPDATE puntos_encuentro SET latitud = 20.00982, longitud = -99.34285 WHERE id = 3;

INSERT INTO __ef_migrations_history (migration_id, product_version)
VALUES ('20261113055522_Sprint4_CoordenadasRealesPuntosEncuentro', '9.0.19');
COMMIT;
