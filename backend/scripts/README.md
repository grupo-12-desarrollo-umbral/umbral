# `backend/scripts/`

Scripts auxiliares para desarrollo, testing y cobertura del backend. Ejecútalos desde la raíz del repo (las rutas relativas asumen ese directorio de trabajo).

| Script | Propósito |
|--------|-----------|
| [`seed-dev-data.sh`](#seed-dev-datash) | Siembra datos de desarrollo alineados en `identity_access` y `session_operations` |
| [`seed-teams.sh`](#seed-teamssh) | Crea equipos vía la API de `identity-access-service` a través del gateway |
| [`cover.sh`](#coversh) | Ejecuta tests de un servicio y genera el reporte de cobertura HTML |
| [`cover-gate.sh`](#cover-gatesh) | Gate de cobertura canónico (ADR-0005): mergea coverlet y aplica el umbral |

---

## `seed-dev-data.sh`

Siembra datos de desarrollo **alineados entre dos bases de datos** —`identity_access` y `session_operations`— escribiendo **directamente con `psql`** (no pasa por la API ni por el gateway). Crea una sesión en cada estado del ciclo de vida para que puedas validar el flujo de unión (*join* / *late-join*) en la UI móvil:

| Código | Estado | Comportamiento esperado al unirse |
|--------|--------|-----------------------------------|
| `SMOKE2` | `Active` | "The session has moved on — late join isn't allowed." |
| `SMOKE3` | `Scheduled` | la primera unión tiene éxito |
| `SMOKE4` | `Preparing` | la primera unión tiene éxito |
| `SMOKE5` | `Paused` | "The session has moved on — late join isn't allowed." |
| `SMOKE6` | `Finished` | "The session has moved on — late join isn't allowed." |
| `SMOKE7` | `Cancelled` | "The session has moved on — late join isn't allowed." |

Cada sesión recibe un `id` y un `team` deterministas; los equipos comparten el mismo GUID en ambas bases de datos. Es **idempotente** (`ON CONFLICT DO NOTHING`): seguro de ejecutar varias veces.

**Uso**

```bash
./backend/scripts/seed-dev-data.sh
```

Apunta a otro host de PostgreSQL con variables de entorno `PG*`:

```bash
PGHOST=192.168.1.50 ./backend/scripts/seed-dev-data.sh
```

| Variable | Por defecto |
|----------|-------------|
| `PGHOST` | `localhost` |
| `PGPORT` | `5432` |
| `PGUSER` | `postgres` |
| `PGPASSWORD` | `postgres` |

> Requiere `psql` en el `PATH` y acceso directo a las dos bases de datos (con la pila de docker-compose levantada, los puertos de PostgreSQL están expuestos en `localhost`).

---

## `seed-teams.sh`

Crea equipos de prueba llamando a `POST /api/teams` de `identity-access-service` **a través del gateway**. Se autentica como el usuario `admin` de Keycloak, obtiene un token y envía cada equipo. Es idempotente: los equipos ya existentes (`409`) se omiten.

**Uso**

```bash
./backend/scripts/seed-teams.sh
# o apuntando a otra URL base / instancia de Keycloak:
./backend/scripts/seed-teams.sh http://localhost:8000 http://localhost:8080
```

> Diferencia con `seed-dev-data.sh`: este script ejercita la **API** de identity-access (gateway + Keycloak); `seed-dev-data.sh` escribe directo en las BD y cubre el flujo de sesiones.

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
