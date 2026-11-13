-- =============================================================================
-- Reversión manual (solo desarrollo/pruebas) de 20261113055522_Sprint4_CoordenadasRealesPuntosEncuentro.
-- La forma recomendada es la nativa de EF Core:
--   dotnet ef database update Sprint3_RestriccionNumeroEdiciones -p src/KARider.Infrastructure -s src/KARider.API
-- Este script existe para bases donde no se tenga el SDK de .NET (p. ej. el SQL Editor de Neon).
-- =============================================================================
START TRANSACTION;
UPDATE puntos_encuentro SET latitud = 20.0875, longitud = -99.348 WHERE id = 1;
UPDATE puntos_encuentro SET latitud = 20.0881, longitud = -99.3472 WHERE id = 2;
UPDATE puntos_encuentro SET latitud = 20.087, longitud = -99.3489 WHERE id = 3;
DELETE FROM __ef_migrations_history WHERE migration_id = '20261113055522_Sprint4_CoordenadasRealesPuntosEncuentro';
COMMIT;
