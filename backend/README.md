# Umbral Backend

El backend de Umbral es un sistema de **microservicios** detrás de un **API
gateway**: un punto de entrada único (YARP reverse proxy) que valida la
autenticación e inyecta cabeceras de confianza hacia los servicios de dominio.
Cada servicio es un _bounded context_ independiente, construido con **Clean
Architecture** (`Domain`, `Application`, `Infrastructure`, `Api`) y **CQRS sobre
MediatR**, con su propia base de datos PostgreSQL.

> **Frontera clave:** los servicios de dominio **no validan JWT**. La validación
> del token (firma, emisor, audiencia) ocurre **una sola vez en el gateway**
> (ADR-0001); aguas abajo solo viajan cabeceras de confianza (`X-User-*`). El
> frontend y el móvil **solo** hablan con el gateway (`localhost:8000`), nunca
> directamente con un servicio.

---

## Tabla de contenidos

1. [Por qué existe este backend](#por-qué-existe-este-backend)
2. [Arquitectura general](#arquitectura-general)
3. [Servicios](#servicios)
4. [Configuración](#configuración)
5. [Cómo ejecutar el backend](#cómo-ejecutar-el-backend)
6. [Autenticación: validación central y cabeceras de confianza](#autenticación-validación-central-y-cabeceras-de-confianza)
7. [Persistencia](#persistencia)
8. [Mensajería](#mensajería)
9. [Testing y cobertura](#testing-y-cobertura)
10. [Scripts](#scripts)
11. [Decisiones de arquitectura (ADR)](#decisiones-de-arquitectura-adr)

---

## Quiénes son los principales integradores de este backend

Keycloak autentica a los usuarios y emite tokens, pero no conoce el dominio de
Umbral. El gateway centraliza la seguridad de borde y el enrutamiento; cada
servicio posee una parcela del dominio y nada más.

| Pieza externa / transversal      | Lo que aporta el backend                                                              |
| -------------------------------- | ------------------------------------------------------------------------------------- |
| **Keycloak** emite y firma JWT   | El **gateway** los valida una vez y traduce identidad a cabeceras de confianza        |
| **PostgreSQL** persiste filas    | Cada **servicio** posee su esquema y sus invariantes de dominio (una BD por servicio) |
| **RabbitMQ** transporta mensajes | Los servicios publican/consumen **eventos de integración** sin acoplarse entre sí     |

Separar el sistema en _bounded contexts_ evita que un cambio en, por ejemplo, la
operación de sesiones en vivo arrastre a la gestión de identidad: cada servicio
se despliega, prueba y razona de forma aislada.

---

## Arquitectura general

```
  ┌───────────┐     ┌───────────┐
  │ Frontend  │     │  Mobile   │
  │ (Next.js) │     │  (Expo)   │
  └─────┬─────┘     └─────┬─────┘
        │  JWT (Bearer / query en WebSocket)
        └──────────┬──────┘
                   ▼
          ┌──────────────────┐      valida JWT contra Keycloak
          │   api-gateway     │◄──────────────────────────────► Keycloak
          │  (YARP, :8000)    │      inyecta X-User-Id/Role/Email
          └───┬─────┬─────┬───┘
              │     │     │   (cabeceras de confianza, sin JWT)
      ┌───────┘     │     └────────────────┐
      ▼             ▼                       ▼
┌───────────┐ ┌──────────────┐ ┌────────────────────────┐
│ identity- │ │   mission-   │ │   session-operations-  │
│  access   │ │   design     │ │       service          │
└─────┬─────┘ └──────┬───────┘ └───────┬────────────────┘
      │              │                 │  (SignalR hub en vivo)
      ▼              ▼                 ▼
   PostgreSQL (una base de datos por servicio)        RabbitMQ
```

- **api-gateway** — borde único: CORS, validación de JWT (ADR-0001), extracción
  del token en conexiones WebSocket/SignalR (ADR-0002) y reenvío con cabeceras
  de confianza (`TrustedHeadersTransform`).
- **Servicios de dominio** — cada uno con sus capas `Domain` / `Application` /
  `Infrastructure` / `Api` y CQRS sobre MediatR.
- **Infraestructura compartida** — PostgreSQL, Keycloak y RabbitMQ levantados
  por `docker-compose.yml`.

---

## Servicios

| Servicio                     | Bounded context                                                                             | Puerto host | Estado                                             |
| ---------------------------- | ------------------------------------------------------------------------------------------- | :---------: | -------------------------------------------------- |
| `api-gateway`                | Borde / enrutamiento + seguridad                                                            |   `8000`    | Activo                                             |
| `identity-access-service`    | **Identity** — aprovisionamiento post-login, usuarios, roles, política de acceso, equipos   |   `5002`    | Activo                                             |
| `mission-design-service`     | **Mission Design** — diseño de misiones / trivias publicables                               |   `5001`    | Activo                                             |
| `session-operations-service` | **Session Operations** — sesiones en vivo, unión de equipos, operador, difusión por SignalR |   `5003`    | Activo                                             |
| `scoring-monitoring-service` | **Scoring & Monitoring** — puntuación y monitoreo                                           |      —      | Scaffold (aún no incluido en `docker-compose.yml`) |

> Cada servicio tiene su propio `README.md` con la referencia de su API, su
> modelo de dominio y su tabla de errores. Este documento es la vista
> transversal.

---

## Configuración

Los servicios **no tienen** `appsettings.json` con secretos: toda la
configuración se inyecta por **variables de entorno** (las claves anidadas usan
`__`). Los valores de desarrollo viven en `docker-compose.yml`.

### Variables de entorno transversales

| Variable                                              | Ámbito               | Por defecto (dev)                      | Descripción                                                        |
| ----------------------------------------------------- | -------------------- | -------------------------------------- | ------------------------------------------------------------------ |
| `ASPNETCORE_ENVIRONMENT`                              | todos                | `Development`                          | Entorno de ejecución                                               |
| `ASPNETCORE_URLS`                                     | todos                | `http://+:8080`                        | Bind dentro del contenedor                                         |
| `ConnectionStrings__umbral_backendDb`                 | servicios de dominio | — (✅ requerida)                       | Connection string Npgsql; una BD por servicio                      |
| `Keycloak__Authority`                                 | gateway              | `http://keycloak:8080/realms/umbral`   | Realm de Keycloak para validar el JWT                              |
| `Keycloak__Audience`                                  | gateway              | `umbral-web`                           | Audiencia válida del token                                         |
| `Frontend__AllowedOrigins__*`                         | gateway              | —                                      | Orígenes CORS permitidos (necesario para SignalR con credenciales) |
| `ReverseProxy__Clusters__*__Destinations__*__Address` | gateway              | URLs de servicio                       | Destinos del reverse proxy YARP                                    |
| `IdentityAccess__BaseAddress`                         | session-operations   | `http://identity-access-service:8080/` | Resolución de perfil del actor (ADR-0009)                          |
| `PublishedTriviaQuizSource__BaseAddress`              | session-operations   | `http://mission-design-service:8080/`  | Fuente de trivias publicadas                                       |

### Puertos

| Componente                 | Host             | Contenedor / notas                                       |
| -------------------------- | ---------------- | -------------------------------------------------------- |
| api-gateway                | `8000`           | `8000:8080` — único punto de entrada para frontend/móvil |
| mission-design-service     | `5001`           | `5001:8080`                                              |
| identity-access-service    | `5002`           | `5002:8080`                                              |
| session-operations-service | `5003`           | `5003:8080`                                              |
| PostgreSQL                 | `5432`           | usuario/clave `postgres` / `postgres`                    |
| Keycloak                   | `8080` / `9000`  | HTTP / puerto de management (health)                     |
| RabbitMQ                   | `5672` / `15672` | AMQP / consola de management                             |

---

## Cómo ejecutar el backend

### Desarrollo con hot reload (recomendado)

`docker compose up` carga **automáticamente** `docker-compose.override.yml`, que
ejecuta cada servicio .NET desde la imagen del SDK con su `src/` montado (_bind
mount_) bajo `dotnet watch run`. Al guardar un `.cs` el servicio recompila y
reinicia en caliente — **sin `--build` ni reconstrucción manual**.

```bash
cd backend
docker compose up -d        # hot reload (el override aplica solo)
```

Después arranca el frontend / móvil como siempre: tienen su propio hot reload y
solo consumen el gateway en `localhost:8000`.

Notas:

- El primer arranque es más lento mientras el SDK restaura paquetes NuGet; un
  volumen compartido `nuget-packages` mantiene los reinicios posteriores
  rápidos.
- El override fuerza `DOTNET_USE_POLLING_FILE_WATCHER=true` porque los eventos
  `inotify` no siempre cruzan los _bind mounts_.
- La infraestructura (PostgreSQL / Keycloak / RabbitMQ) no cambia: son imágenes,
  no _builds_.

### Build tipo producción

Para ignorar el override y hornear un `publish` en Release dentro de cada imagen
(lo que usan CI y despliegue):

```bash
docker compose -f docker-compose.yml up --build
```

### Toolchain .NET (sin Docker)

La cadena .NET pasa por el `Makefile` endurecido para el sandbox — ver
`AGENTS.md`:

```bash
make build SVC=<servicio>   # compila Api + proyectos de test
make test  SVC=<servicio>   # tests unitarios + integración
make gate  SVC=<servicio>   # gate de cobertura (ADR-0005)
make ef    SVC=<servicio> ARGS="migrations add <Nombre>"
```

### Datos de desarrollo (seed)

Con la pila levantada, siembra datos de prueba (ver [Scripts](#scripts)):

```bash
./scripts/dev-up.sh          # pipeline: down -v → up --wait → seed (sesiones + usuarios/equipos)
./scripts/seed-dev-data.sh   # solo sesiones en cada estado del ciclo de vida (psql directo)
./scripts/seed-users.sh      # solo usuarios + 4 equipos con participantes vía la API del gateway
```

---

## Autenticación: validación central y cabeceras de confianza

El JWT se valida **una sola vez, en el gateway** (`AddJwtBearer` contra el realm
de Keycloak; emisores válidos `localhost:8080` y `keycloak:8080`, audiencia
`umbral-web`). En conexiones WebSocket/SignalR el token se extrae del query
string (ADR-0002). Tras validar, el gateway reenvía la petición con cabeceras de
confianza y **sin** el JWT:

| Cabecera       | Origen (claim) | Ejemplo            |
| -------------- | -------------- | ------------------ |
| `X-User-Id`    | `sub`          | `abc-123`          |
| `X-User-Role`  | rol de realm   | `Administrator`    |
| `X-User-Email` | `email`        | `user@example.com` |

Comportamiento: token ausente/ inválido → `401` en el gateway; el proveedor de
identidad inaccesible → `503` (`ProblemDetails`). Los servicios confían en las
cabeceras y aplican autorización por rol/política en sus propios endpoints.

---

## Persistencia

- **Motor:** PostgreSQL vía Npgsql.
- **Una base de datos por servicio:** `identity_access`, `mission_design`,
  `session_operations` (aislamiento de esquema por _bounded context_).
- **Connection string lógica:** `umbral_backendDb` (cada servicio recibe la suya
  por variable de entorno).
- **Migraciones:** se aplican automáticamente al arrancar el servicio.
- **Tests de integración:** comparten un Testcontainer de PostgreSQL (ADR-0008).

---

## Mensajería

RabbitMQ (`5672` AMQP, `15672` management) está disponible en la pila para la
integración **dirigida por eventos** entre servicios, evitando llamadas
síncronas acopladas. La consola de management ayuda a inspeccionar colas y
exchanges en desarrollo.

---

## Testing y cobertura

Cada servicio tiene proyectos `UnitTests` y `IntegrationTests`
(`WebApplicationFactory` + Testcontainers).

```bash
make test SVC=<servicio>
make gate SVC=<servicio>   # gate de cobertura ADR-0005
```

El gate canónico es `scripts/cover-gate.sh` (ADR-0005): encadena coverlet sobre
los proyectos de test, **mergea** los resultados y aplica el umbral de línea
sobre el total (por defecto **90%**, override con `THRESHOLD=NN`). Su **código
de salida es el gate** (`0` verde, distinto de `0` falla) y es la **única fuente
de verdad** del número de cobertura para CI/CD.

El reporte se renderiza desde el mismo fichero Cobertura mergeado que se gatea,
por servicio, en:

```
<servicio>/coverage/gate/merged.cobertura.xml   # fichero gateado
<servicio>/coverage/gate/Summary.txt            # resumen de texto
<servicio>/coverage/gate/index.html             # reporte HTML
```

> No uses `cover.sh` para el número oficial: usa un conjunto de proyectos y
> filtros distintos, así que su porcentaje **no** coincide con el gateado.

---

## Scripts

Ver [`scripts/README.md`](scripts/README.md) para el detalle. Ejecútalos desde
la raíz del repo.

| Script             | Propósito                                                                                                                               |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------- |
| `dev-up.sh`        | Pipeline de desarrollo: `down -v` → `up --wait` → siembra (sesiones + usuarios/equipos). Un solo comando para levantar y poblar la pila |
| `seed-dev-data.sh` | Siembra sesiones en cada estado del ciclo de vida en `identity_access` y `session_operations` (escribe directo con `psql`; idempotente) |
| `seed-users.sh`    | Crea usuarios en Keycloak + 4 equipos con participantes (2 por equipo) vía la API del gateway (idempotente)                             |
| `cover.sh`         | Tests + reporte HTML de cobertura de demostración de un servicio                                                                        |
| `cover-gate.sh`    | Gate de cobertura canónico (ADR-0005) — la fuente de verdad para CI/CD                                                                  |

---

## Decisiones de arquitectura (ADR)

En `docs/adr/`:

- **ADR-0001** — Validación central de JWT en el gateway.
- **ADR-0002** — Extracción del token en conexiones WebSocket en el gateway.
- **ADR-0003** — Resumen de diseño gateway ↔ Keycloak.
- **ADR-0004** — Patrones de dominio requeridos.
- **ADR-0005** — Coverlet + MSBuild para cobertura agregada (el gate).
- **ADR-0006** — Arquitectura de gestión de usuarios (HU-02).
- **ADR-0007** — Token de unión: propiedad de identidad y validación no
  consumidora.
- **ADR-0008** — Testcontainer de PostgreSQL compartido para tests de
  integración.
- **ADR-0009** — Resolver propiedad del operador vía el perfil de actor de
  identity.

Reglas de frontera del backend:

- Los servicios de dominio **no** validan JWT — confían en las cabeceras de
  confianza del gateway.
- El frontend y el móvil **no** llaman a un servicio directamente — solo al
  gateway (`localhost:8000`).
- Cada servicio **no** accede a la base de datos de otro — la integración entre
  contextos es por API o por eventos.
