# Flujo de ramas (GitFlow) y Pull Requests

Aplica igual a **KARider-back** y **KARider-front**.

## Ramas

| Rama | Para qué | Sale de | Se integra a | Despliega |
|---|---|---|---|---|
| `main` | Código en producción | — | — | **Sí** (único origen de producción) |
| `develop` | Integración del siguiente lanzamiento | — | `main` (vía PR de lanzamiento) | No |
| `feature/<nombre>-Sprint<N>-HU<NN>-<tema>` | Funcionalidad nueva | `develop` | `develop` | No |
| `bugfix/<nombre>-Sprint<N>-<tema>` | Corrección no urgente | `develop` | `develop` | No |
| `hotfix/<tema>` | Corrección urgente de producción | `main` | `main` **y** `develop` | Sí, al llegar a `main` |
| `release/<versión>` | Estabilizar un lanzamiento grande (opcional) | `develop` | `main` y `develop` | Sí, al llegar a `main` |

Ejemplos: `feature/kazumi-Sprint2-HU08-mapa-ruta-real`, `bugfix/kazumi-Sprint4-seguridad-servidor`,
`hotfix/eliminar-workflow-duplicado`.

## Ciclo

1. Crear la rama desde `develop` actualizado (`git switch develop && git pull && git switch -c feature/...`).
2. Commits con [Conventional Commits](https://www.conventionalcommits.org/es/): `feat(viajes): ...`, `fix(auth): ...`.
3. Abrir PR hacia `develop` con la plantilla. CI (lint, pruebas, build) y SonarCloud deben quedar en verde,
   con al menos **1 aprobación** de un compañero.
4. Fusionar con **merge commit** (conserva cada commit con su autor). **No borrar la rama** al fusionar:
   el equipo las conserva como evidencia del trabajo por sprint.
5. Lanzamiento: PR `develop → main` (opcionalmente pasando por `release/<versión>`). Al fusionar en `main`
   se despliega a producción automáticamente.
6. Si se arregla algo directo en producción (`hotfix/*` → `main`), abrir de inmediato el PR `main → develop`
   para que no se pierda en el siguiente lanzamiento.

## Reglas

- Nada de commits directos a `main` ni a `develop`: todo entra por PR (protección de ramas).
- No desactivar validaciones para que un PR pase: se corrige el código o la prueba.
- Los cambios de contrato de API se hacen en ambos repos y se fusionan juntos (primero el backend).
- Las migraciones de base de datos van en el mismo PR que el cambio del modelo, con su script en `database/`.
