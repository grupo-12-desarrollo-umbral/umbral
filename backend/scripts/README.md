# `backend/scripts/`

Scripts auxiliares para desarrollo, testing y cobertura del backend. Ejecútalos desde la raíz del repo (las rutas relativas asumen ese directorio de trabajo).

| Script | Propósito |
|--------|-----------|
| [`dev-up.sh`](#pipeline-dev-upsh) | **Pipeline de desarrollo**: levanta el stack y lo siembra en un solo comando |
| [`seed-all.sh`](#seed-allsh) | Siembra completa: quizzes, sesiones, equipos, usuarios Keycloak y membresías — todo en un solo script |
| [`cover.sh`](#coversh) | Ejecuta tests de un servicio y genera el reporte de cobertura HTML |
| [`cover-gate.sh`](#cover-gatesh) | Gate de cobertura canónico (ADR-0005): mergea coverlet y aplica el umbral |

---

## Pipeline: `dev-up.sh`

El "pipeline" de desarrollo local: recrea el stack con hot-reload y lo siembra en **un solo comando**, de modo que el frontend / móvil (`localhost:8000`) siempre tengan datos para usar. Encadena lo que harías a mano:

1. `docker compose down -v --remove-orphans` — borra los volúmenes para empezar de cero (omitir con `--keep`).
2. `docker compose up -d --wait` — bloquea hasta que los servicios con healthcheck (**postgres**, **keycloak**) estén sanos.
3. `seed-all.sh` — siembra todo (psql → espera gateway → API). Si el gateway aún está compilando, avisa en vez de tumbar la corrida.

**Uso**

```bash
./backend/scripts/dev-up.sh           # down -v → up --wait → seed (slate limpio)
./backend/scripts/dev-up.sh --keep    # omite `down -v` (conserva los datos de la BD)
```

> **No** corre el gate de cobertura. Ese es un flujo aparte del host — `make -C backend gate-all` — que usa Testcontainers y no toca este stack. No los encadenes: los contenedores de hot-reload corren como root sobre el código montado (*bind mount*), así que dejan `bin/`/`obj/` con dueño root que bloquean un gate posterior corrido en el host (límpialos con `make -C backend clean-all`, o una vez con `sudo find services -type d \( -name bin -o -name obj \) -exec rm -rf {} +`).

---

## `seed-all.sh`

Fusión de los dos scripts anteriores en uno solo. Combina la siembra directa a PostgreSQL con el aprovisionamiento vía API:

1. **Siembra vía `psql`** — 7 quizzes de trivia (5 Published, 1 Draft, 1 Archived) en `mission_design`; 7 sesiones (SMOKE1–SMOKE7) en cada estado del ciclo de vida, cada una con 2 equipos asociados, en `identity_access` y `session_operations`.
2. **Espera** a que el gateway conteste en `:8000`.
3. **Siembra vía API** — 12 usuarios en Keycloak (admin, 3 operators, 8 participants), los bootstrapea en `identity_access` a través del gateway, registra 4 equipos app-level (Delta, Echo, Bismarck, Los Panas) y asigna 2 participantes por equipo.

| Código | Estado | Comportamiento esperado al unirse |
|--------|--------|-----------------------------------|
| `SMOKE1` | `Scheduled` | primera unión tiene éxito (3 min tras seed) |
| `SMOKE2` | `Active` | "The session has moved on — late join isn't allowed." |
| `SMOKE3` | `Scheduled` | la primera unión tiene éxito |
| `SMOKE4` | `Preparing` | la primera unión tiene éxito |
| `SMOKE5` | `Paused` | "The session has moved on — late join isn't allowed." |
| `SMOKE6` | `Finished` | "The session has moved on — late join isn't allowed." |
| `SMOKE7` | `Cancelled` | "The session has moved on — late join isn't allowed." |

Idempotente: `ON CONFLICT DO NOTHING` en las inserciones SQL y detección de `409` en las llamadas API.

La reconstrucción del catálogo de trivia es destructiva: antes de recrear los quizzes con ids
nuevos, elimina las misiones que seleccionan un quiz para impedir referencias
`TriviaQuizId` colgantes. El global setup de Playwright vuelve a crear sus fixtures E2E.

**Uso**

```bash
./backend/scripts/seed-all.sh
```

**Variables de entorno para la parte psql**

| Variable | Por defecto |
|----------|-------------|
| `PGHOST` | `localhost` |
| `PGPORT` | `5432` |
| `PGUSER` | `postgres` |
| `PGPASSWORD` | `postgres` |

**Variables de entorno para la parte API / Keycloak**

| Variable | Por defecto |
|----------|-------------|
| `BASE_URL` | `http://localhost:8000` |
| `KEYCLOAK_URL` | `http://localhost:8080` |
| `REALM` | `umbral` |
| `WEB_CLIENT_ID` | `umbral-web` |
| `MASTER_REALM` | `master` |
| `KEYCLOAK_ADMIN_USERNAME` | `admin` |
| `KEYCLOAK_ADMIN_PASSWORD` | `admin` |
| `ADMIN_PASSWORD` | `admin123` |
| `OPERATOR_PASSWORD` | `operator123` |
| `PARTICIPANT_PASSWORD` | `participant123` |

> `seed-all.sh` es el único seeder. Para sembrar sobre un stack ya levantado, ejecútalo directo;
> para un slate limpio usa `dev-up.sh` (que hace `down -v` → `up --wait` → `seed-all.sh`).

---

## `cover.sh`

Ejecuta los tests de un servicio (unitarios y/o integración) y genera un reporte de cobertura HTML.

```bash
./backend/scripts/cover.sh <service-name> [opciones]
```

`<service-name>` es uno de: `identity-access-service`, `mission-design-service`, `session-operations-service`, `scoring-monitoring-service`.

| Opción | Efecto |
|--------|--------|
| `-u`, `--unit-only` | Solo tests unitarios |
| `-i`, `--integration-only` | Solo tests de integración (requiere Docker) |
| `-o`, `--open` | Abre el reporte HTML en el navegador |
| `-h`, `--help` | Ayuda |

---

## `cover-gate.sh`

Gate de cobertura **canónico** (ADR-0005) y única fuente de verdad del número de cobertura para CI/CD. Encadena coverlet sobre los proyectos de test, mergea los resultados y aplica el umbral de línea. El código de salida **es** el gate: `0` = verde, distinto de `0` = falla (error de build, test roto o cobertura por debajo del umbral).

> No generes el reporte de demostración con `cover.sh`: usa un conjunto de proyectos y filtros distintos, así que su número no coincide con el número gateado.
