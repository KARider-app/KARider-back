-- =============================================================================
-- Reversión manual (solo desarrollo/pruebas) de 20261023200200_Sprint3_RestriccionNumeroEdiciones.
-- La forma recomendada es la nativa de EF Core:
--   dotnet ef database update Sprint3_NumeroEdicionesViaje -p src/KARider.Infrastructure -s src/KARider.API
-- Este script existe para bases donde no se tenga el SDK de .NET (p. ej. el SQL Editor de Neon).
-- =============================================================================
START TRANSACTION;
ALTER TABLE viajes DROP CONSTRAINT ck_viajes_numero_ediciones;
DELETE FROM __ef_migrations_history WHERE migration_id = '20261023200200_Sprint3_RestriccionNumeroEdiciones';
COMMIT;
