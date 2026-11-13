<!--
Título del PR (Conventional Commits): tipo(alcance): descripción
  ej.  feat(viajes): HU-08 mapa con ruta real   ·   fix(auth): ...   ·   ci: ...   ·   docs: ...
Rama: feature/<nombre>-Sprint<N>-HU<NN>-<tema> · bugfix/... · hotfix/... (ver docs/flujo-ramas.md)
-->

## ¿Qué cambia y por qué?

<!-- Describe el problema que resuelve y el enfoque elegido. -->

## Historia de usuario / issue

<!-- "Closes #123" cierra el issue al fusionar. Indica la HU (p. ej. HU-08). -->

## Tipo de cambio

- [ ] Nueva funcionalidad (`feature/*`)
- [ ] Corrección (`bugfix/*`)
- [ ] Corrección urgente de producción (`hotfix/*` → `main` y `develop`)
- [ ] CI/CD, documentación o mantenimiento

## Cómo se probó

<!-- Pruebas automáticas agregadas y pasos manuales. -->

## Lista de verificación

- [ ] La rama sale de `develop` (o de `main` si es `hotfix/*`) y apunta a la rama correcta.
- [ ] CI en verde: lint, pruebas, build y SonarCloud (Quality Gate aprobado).
- [ ] Agregué o actualicé pruebas para el código nuevo.
- [ ] Sin secretos, contraseñas ni `.env` en el diff.
- [ ] Si cambia el contrato de la API: actualicé el otro repositorio (front/back) y la documentación.
- [ ] Si cambia la base de datos: incluye migración de EF Core y script en `database/`.
