# `backend/scripts/`

Scripts auxiliares para desarrollo, testing y cobertura del backend. Ejecútalos desde la raíz del repo (las rutas relativas asumen ese directorio de trabajo).

| Script | Propósito |
|--------|-----------|
| [`dev-up.sh`](#pipeline-dev-upsh) | **Pipeline de desarrollo**: levanta la pila y la siembra en un solo comando |
| [`seed-dev-data.sh`](#seed-dev-datash) | Siembra datos de desarrollo alineados en `identity_access` y `session_operations` |
| [`seed-users.sh`](#seed-userssh) | Crea usuarios de desarrollo en Keycloak + 4 equipos con participantes, y los aprovisiona en `identity_access` |
| [`cover.sh`](#coversh) | Ejecuta tests de un servicio y genera el reporte de cobertura HTML |
| [`cover-gate.sh`](#cover-gatesh) | Gate de cobertura canónico (ADR-0005): mergea coverlet y aplica el umbral |

---

## Pipeline: `dev-up.sh`

El "pipeline" de desarrollo local: recrea la pila con hot-reload y la siembra en **un solo comando**, de modo que el frontend / móvil (`localhost:8000`) siempre tengan datos para usar. Encadena lo que harías a mano:

1. `docker compose down -v --remove-orphans` — borra los volúmenes para empezar de cero (omitir con `--keep`).
2. `docker compose up -d --wait` — bloquea hasta que los servicios con healthcheck (**postgres**, **keycloak**) estén sanos.
3. `seed-dev-data.sh` — siembra las sesiones (psql directo; solo necesita postgres).
4. Espera a que el gateway escuche en `:8000` y ejecuta `seed-users.sh` (usuarios + equipos) **best-effort**: si el gateway aún está compilando, avisa en vez de tumbar la corrida.

**Uso**

```bash
./backend/scripts/dev-up.sh           # down -v → up --wait → seed (slate limpio)
./backend/scripts/dev-up.sh --keep    # omite `down -v` (conserva los datos de la BD)
```

> **No** corre el gate de cobertura. Ese es un flujo aparte del host — `make -C backend gate-all` — que usa Testcontainers y no toca esta pila. No los encadenes: los contenedores de hot-reload corren como root sobre el código montado (*bind mount*), así que dejan `bin/`/`obj/` con dueño root que bloquean un gate posterior corrido en el host (límpialos con `make -C backend clean-all`, o una vez con `sudo find services -type d \( -name bin -o -name obj \) -exec rm -rf {} +`).

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

## `seed-users.sh`

Asegura que las identidades base de desarrollo del realm importado (`admin`, `operator`, `operator2`, `participant`), el operador adicional (`operator3`) y ocho participantes (`participant01` … `participant08`) existan y queden aprovisionados en la aplicación. Para las cuentas ya importadas en el realm, reutiliza la identidad existente; para las nuevas, las crea en **Keycloak** usando la Admin API del realm `master`. Después autentica cada cuenta contra el realm `umbral` para llamar a `POST /api/users/authenticated` a través del gateway. Así quedan alineados tanto la identidad externa como la fila interna en `identity_access.users`.

Después de los usuarios, siembra **4 equipos** (`Delta`, `Echo`, `Bismarck`, `Los Panas`) vía `POST /api/teams` y asigna los ocho participantes **2 por equipo** vía `POST /api/teams/{id}/participants` (resuelve el `UserId` interno con `GET /api/users/me`). Esto requiere un token de app con rol `Administrator`/`Operator` (usa `admin`).

Es **idempotente**: si la identidad ya existe en Keycloak, la reutiliza; los equipos ya existentes (`409`) se reutilizan y las membresías ya existentes (`409`) se omiten; en todos los casos vuelve a sincronizar el rol de realm y reejecuta el bootstrap del usuario en la aplicación.

**Uso**

```bash
./backend/scripts/seed-users.sh
# o apuntando a otra URL base / instancia de Keycloak:
./backend/scripts/seed-users.sh http://localhost:8000 http://localhost:8080
```

**Variables de entorno**

| Variable | Por defecto |
|----------|-------------|
| `REALM` | `umbral` |
| `WEB_CLIENT_ID` | `umbral-web` |
| `MASTER_REALM` | `master` |
| `KEYCLOAK_ADMIN_USERNAME` | `admin` |
| `KEYCLOAK_ADMIN_PASSWORD` | `admin` |
| `ADMIN_PASSWORD` | `admin123` |
| `OPERATOR_PASSWORD` | `operator123` |
| `PARTICIPANT_PASSWORD` | `participant123` |

> Este script no escribe directo a PostgreSQL. La fuente de verdad de credenciales sigue siendo Keycloak; la fuente de verdad del perfil interno sigue siendo el flujo de bootstrap de `identity-access-service`.

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
