# Backend de Umbral

El backend de Umbral es un sistema de **microservicios** detrás de una **puerta
de enlace de API**: un punto de entrada único basado en el proxy inverso YARP que valida la
autenticación e inyecta cabeceras de confianza hacia los servicios de dominio.
Cada servicio es un contexto delimitado independiente, construido con
**Arquitectura Limpia** (`Domain`, `Application`, `Infrastructure`, `Api`) y **CQRS sobre
MediatR**, con su propia base de datos PostgreSQL.

> **Frontera clave:** los servicios de dominio **no validan JWT**. La validación
> del token (firma, emisor, audiencia) ocurre **una sola vez en el gateway**
> (ADR-0001); aguas abajo solo viajan cabeceras de confianza (`X-User-*`). El
> frontend y el móvil **solo** hablan con el gateway (`localhost:8000`), nunca
> directamente con un servicio.

---

## Tabla de contenidos

1. [Integradores principales](#quiénes-son-los-principales-integradores-de-este-backend)
2. [Arquitectura general](#arquitectura-general)
3. [Servicios](#servicios)
4. [Configuración](#configuración)
5. [Cómo ejecutar el backend](#cómo-ejecutar-el-backend)
6. [Autenticación: validación central y cabeceras de confianza](#autenticación-validación-central-y-cabeceras-de-confianza)
7. [Persistencia](#persistencia)
8. [Mensajería](#mensajería)
9. [Pruebas y cobertura](#pruebas-y-cobertura)
10. [Principios SOLID y patrones de diseño](#principios-solid-y-patrones-de-diseño)
11. [Automatización](#automatización)
12. [Decisiones de arquitectura (ADR)](#decisiones-de-arquitectura-adr)

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

Separar el sistema en contextos delimitados evita que un cambio en, por ejemplo, la
operación de sesiones en vivo arrastre a la gestión de identidad: cada servicio
se despliega, prueba y razona de forma aislada.

---

## Arquitectura general

```
  ┌───────────┐     ┌───────────┐
  │ Frontend  │     │   Móvil   │
  │ (Next.js) │     │  (Expo)   │
  └─────┬─────┘     └─────┬─────┘
        │  JWT (Bearer / consulta en WebSocket)
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
      │              │                 │  (concentrador SignalR en vivo)
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

| Servicio                     | Contexto delimitado                                                                         | Puerto local | Estado                                            |
| ---------------------------- | ------------------------------------------------------------------------------------------- | :---------: | -------------------------------------------------- |
| `api-gateway`                | Borde / enrutamiento + seguridad                                                            |   `8000`    | Activo                                             |
| `users-service`              | **Usuarios** — perfil y acceso de Umbral; autenticación y credenciales delegadas a Keycloak |      —      | Activo                                            |
| `mission-design-service`     | **Diseño de misiones** — misiones y trivias publicables                                     |      —      | Activo                                            |
| `session-operations-service` | **Operación de sesiones** — sesiones en vivo, equipos, operador y difusión por SignalR      |      —      | Activo                                            |
| `scoring-monitoring-service` | **Puntuación y monitoreo**                                                                  |      —      | Activo                                            |

> Cada servicio tiene su propio `README.md` con responsabilidades, límites,
> superficies API, dependencias y comandos de desarrollo. Este documento es la
> vista transversal.

---

## Configuración

Los servicios **no tienen** `appsettings.json` con secretos: toda la
configuración se inyecta por **variables de entorno** (las claves anidadas usan
`__`). Los valores de desarrollo viven en `docker-compose.yml`.

### Variables de entorno transversales

| Variable                                              | Ámbito               | Valor de desarrollo                   | Descripción                                                        |
| ----------------------------------------------------- | -------------------- | -------------------------------------- | ------------------------------------------------------------------ |
| `ASPNETCORE_ENVIRONMENT`                              | todos                | `Development`                          | Entorno de ejecución                                               |
| `ASPNETCORE_URLS`                                     | todos                | `http://+:8080`                        | Dirección de escucha dentro del contenedor                         |
| `ConnectionStrings__umbral_backendDb`                 | servicios de dominio | — (✅ requerida)                       | Cadena de conexión Npgsql; una BD por servicio                     |
| `Keycloak__Authority`                                 | gateway              | `http://keycloak:8080/realms/umbral`   | Reino de Keycloak para validar el JWT                              |
| `Keycloak__Audience`                                  | gateway              | `umbral-web`                           | Audiencia válida del token                                         |
| `Frontend__AllowedOrigins__*`                         | gateway              | —                                      | Orígenes CORS permitidos (necesario para SignalR con credenciales) |
| `ReverseProxy__Clusters__*__Destinations__*__Address` | gateway              | URLs de servicio                       | Destinos del proxy inverso YARP                                    |
| `UsersService__BaseAddress`                         | session-operations   | `http://users-service:8080/` | Resolución de perfil del actor (ADR-0009)                          |
| `PublishedTriviaQuizSource__BaseAddress`              | session-operations   | `http://mission-design-service:8080/`  | Fuente de trivias publicadas                                       |

### Puertos

| Componente                 | Equipo local     | Contenedor / notas                                       |
| -------------------------- | ---------------- | -------------------------------------------------------- |
| api-gateway                | `8000`           | `8000:8080` — único punto de entrada para frontend/móvil |
| mission-design-service     | —                | sólo red interna, vía api-gateway (sin puerto de host)   |
| users-service    | —                | sólo red interna, vía api-gateway (sin puerto de host)   |
| session-operations-service | —                | sólo red interna, vía api-gateway (sin puerto de host)   |
| scoring-monitoring-service | —                | sólo red interna, vía api-gateway (sin puerto de host)   |
| PostgreSQL                 | `5432`           | usuario/clave `postgres` / `postgres`                    |
| Keycloak                   | `8080` / `9000`  | HTTP / puerto de administración y salud                  |
| Mailpit                    | `8025` / `1025`  | Interfaz web de correo / receptor SMTP local             |
| RabbitMQ                   | `5672` / `15672` | AMQP / consola de administración                         |

### Correo saliente de Keycloak (SMTP)

Keycloak envía correo de **verificación de email** (`verifyEmail: true` en el
reino), **restablecimiento de contraseña** e invitaciones. En desarrollo el
reino apunta por defecto a **Mailpit** (`mailpit:1025`, sin autenticación), que captura
todo el correo sin enviarlo a Internet — inspecciónalo en
[http://localhost:8025](http://localhost:8025).

El bloque `smtpServer` del reino (`deploy/keycloak/import/umbral-realm.json`)
está parametrizado por variables de entorno del **contenedor de Keycloak**. Con
los valores predeterminados, el entorno de desarrollo funciona sin configuración.
**En cualquier otro entorno hay que usar un servidor de retransmisión SMTP real**
definiendo:

| Variable                   | Valor de desarrollo   | Descripción                                            |
| -------------------------- | --------------------- | ------------------------------------------------------ |
| `KC_SMTP_HOST`             | `mailpit`             | Host del servidor SMTP                                  |
| `KC_SMTP_PORT`             | `1025`                | Puerto SMTP                                             |
| `KC_SMTP_FROM`             | `no-reply@umbral.local` | Dirección remitente                                  |
| `KC_SMTP_FROM_DISPLAY_NAME`| `Umbral`              | Nombre visible del remitente                            |
| `KC_SMTP_SSL`              | `false`               | SSL/TLS implícito (normalmente puerto 465)             |
| `KC_SMTP_STARTTLS`         | `false`               | STARTTLS (normalmente puerto 587)                      |
| `KC_SMTP_AUTH`             | `false`               | Requiere autenticación en el servidor                  |
| `KC_SMTP_USER`             | _(vacío)_             | Usuario SMTP (si `KC_SMTP_AUTH=true`)                  |
| `KC_SMTP_PASSWORD`         | _(vacío)_             | Contraseña SMTP (si `KC_SMTP_AUTH=true`)               |

Los usuarios sembrados de desarrollo llevan `emailVerified: true`, así que el
inicio de sesión local sigue funcionando sin pasar por Mailpit.

### Restablecimiento de contraseña hospedado por Keycloak

El reino deja **explícitamente** enlazado el flujo hospedado de restablecimiento
de contraseña con `resetCredentialsFlow: "reset credentials"` y lo expone en:

- `http://localhost:8080/realms/umbral/login-actions/reset-credentials`

Ese es el punto de entrada que consumen el inicio de sesión web y la aplicación
móvil para «¿Olvidaste tu contraseña?».

Para que el enlace del correo siga siendo usable fuera del caso ideal de entrega
instantánea, el reino fija `actionTokenGeneratedByUserLifespan: 900` (15
minutos). Es la vigencia del token enviado por correo; los 5 minutos
predeterminados de Keycloak suelen ser insuficientes para completar el proceso.

---

## Cómo ejecutar el backend

El pipeline local tiene dos carriles independientes:

| Objetivo | Comando canónico desde la raíz del monorepo |
| --- | --- |
| Ejecutar el entorno | `docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml up -d --wait` |
| Gate de pruebas y cobertura | `make -C backend gate SVC=<servicio>` |
| Reproducir CI para un servicio | `make -C backend ci SVC=<servicio>` |
| Reproducir CI para todo el backend | `make -C backend ci-all` |

No encadenes ambos carriles como si fueran un único pipeline: la verificación
usa dependencias desechables mediante Testcontainers y no necesita el stack
persistente. `gate` es el control canónico de pruebas y cobertura de los
servicios de dominio; `ci` añade build y controles de arquitectura y constituye
el veredicto completo antes de integrar.

### Desarrollo con recarga en caliente (recomendado)

`docker compose up` carga **automáticamente** `docker-compose.override.yml`, que
ejecuta cada servicio .NET desde la imagen del SDK con `src/` montado en el
contenedor bajo `dotnet watch run`. Al guardar un `.cs`, el servicio recompila y
reinicia en caliente — **sin `--build` ni reconstrucción manual**.

```bash
cd backend
docker compose up -d        # recarga en caliente; aplica la configuración adicional
```

Después inicia el frontend o la aplicación móvil: tienen su propia recarga y
solo consumen el gateway en `localhost:8000`.

Notas:

- El primer arranque es más lento mientras el SDK restaura paquetes NuGet; un
  volumen compartido `nuget-packages` mantiene los reinicios posteriores
  rápidos.
- La configuración adicional fuerza `DOTNET_USE_POLLING_FILE_WATCHER=true` porque los eventos
  `inotify` no siempre cruzan los _bind mounts_.
- La infraestructura (PostgreSQL / Keycloak / RabbitMQ) no cambia: son imágenes,
  no compilaciones locales.

### Construcción similar a producción

Para ignorar la configuración adicional y generar un `publish` en modo Release
dentro de cada imagen, como hacen CI y despliegue:

```bash
docker compose -f docker-compose.yml up --build
```

### Cadena de herramientas .NET (sin Docker)

La cadena .NET pasa por el `Makefile` preparado para el entorno aislado. Consulta
la [guía de integración continua local](docs/local-ci.md):

```bash
make build SVC=<servicio>   # compila Api + proyectos de prueba
make test  SVC=<servicio>   # pruebas unitarias + integración
make gate  SVC=<servicio>   # control de cobertura (ADR-0005)
make ci    SVC=<servicio>   # reproduce la integración continua y genera reportes
make ci-all                 # verifica todas las cargas del backend
make ef    SVC=<servicio> ARGS="migrations add <Nombre>"
```

### Datos de desarrollo

Con el entorno ya levantado, la siembra de datos de demostración es opcional
(ver [Automatización](#automatización)):

```bash
./backend/scripts/seed-all.sh
```

`dev-up.sh` se conserva como una comodidad para recrear y sembrar todo, pero no
es el pipeline recomendado: sin `--keep` elimina los volúmenes y, si el gateway
aún no está listo, muestra una advertencia en lugar de propagar el fallo del
seeder.

---

## Autenticación: validación central y cabeceras de confianza

El JWT se valida **una sola vez, en el gateway** (`AddJwtBearer` contra el reino
de Keycloak; emisores válidos `localhost:8080` y `keycloak:8080`, audiencia
`umbral-web`). En conexiones WebSocket/SignalR, el token se extrae de la cadena
de consulta (ADR-0002). Tras validarlo, el gateway reenvía la petición con cabeceras de
confianza y **sin** el JWT:

| Cabecera       | Origen (atributo) | Ejemplo            |
| -------------- | -------------- | ------------------ |
| `X-User-Id`    | `sub`          | `abc-123`          |
| `X-User-Role`  | rol del reino  | `Administrator`    |
| `X-User-Email` | `email`        | `user@example.com` |

Comportamiento: token ausente/ inválido → `401` en el gateway; el proveedor de
identidad inaccesible → `503` (`ProblemDetails`). Los servicios confían en las
cabeceras y aplican autorización por rol o política en sus propios puntos de entrada.

---

## Persistencia

- **Motor:** PostgreSQL vía Npgsql.
- **Una base de datos por servicio:** `users`, `mission_design`,
  `session_operations` y `scoring_monitoring` (aislamiento por contexto
  delimitado).
- **Cadena de conexión lógica:** `umbral_backendDb` (cada servicio recibe la suya
  por variable de entorno).
- **Migraciones:** se aplican automáticamente al arrancar el servicio.
- **Pruebas de integración:** comparten un Testcontainer de PostgreSQL (ADR-0008).

---

## Mensajería

RabbitMQ (`5672` AMQP, `15672` administración) está disponible en el entorno para la
integración **dirigida por eventos** entre servicios, evitando llamadas
síncronas acopladas. La consola de administración permite inspeccionar colas e
intercambios en desarrollo.

---

## Pruebas y cobertura

Cada servicio tiene proyectos `UnitTests` y `IntegrationTests`
(`WebApplicationFactory` + Testcontainers).

```bash
make test SVC=<servicio>
make gate SVC=<servicio>   # control de cobertura ADR-0005
```

El control canónico es `scripts/cover-gate.sh` (ADR-0005): encadena Coverlet sobre
los proyectos de prueba, combina los resultados y exige una cobertura agregada
de ramas de al menos **95%**. Su **código de salida es el veredicto** (`0` verde,
distinto de `0` falla) y es la **única fuente
de verdad** del número de cobertura para CI/CD.

El reporte se genera desde el mismo archivo Cobertura combinado que se controla,
por servicio, en:

```
backend/coverage/<servicio>/merged.cobertura.xml   # resultado controlado
backend/coverage/<servicio>/Summary.txt            # resumen de texto
backend/coverage/<servicio>/Summary.csv            # resumen CSV
backend/coverage/<servicio>/index.html              # reporte HTML
```

> No uses `cover.sh` para el número oficial: usa un conjunto de proyectos y
> filtros distintos, así que su porcentaje **no** coincide con el oficial.

---

## Principios SOLID y patrones de diseño

La arquitectura del backend evidencia SRP, OCP, LSP, ISP y DIP mediante
componentes reales de validación, ciclo de vida, comunicación en tiempo real y
persistencia. La memoria técnica relaciona cada principio con clases y pruebas
concretas, y registra también los límites conocidos de la implementación.

- [Evidencia de principios SOLID](docs/solid-principles-evidence.md)
- [Matriz de patrones requeridos](docs/required_patterns_matrix.md)
- [Realizaciones de patrones por capa](docs/adr-0012-pattern-realizations-by-layer.md)
- [Convención de ubicación de patrones](docs/adr/0012-design-pattern-placement-convention.md)

Entre las realizaciones principales se encuentran:

- Cadena de responsabilidad para la validación contextual de evidencias.
- Estado para el ciclo de vida de las sesiones.
- Composición para la jerarquía de misiones.
- Proxy para el acceso a recursos protegidos.
- Fachada para coordinar operaciones de sesión y sus colaboradores.
- Inversión de dependencias mediante puertos definidos en Application y
  adaptadores implementados en Api e Infrastructure.

---

## Automatización

Ver [`scripts/README.md`](scripts/README.md) para el detalle. Ejecútalos desde
la raíz del repo.

| Script             | Propósito                                                                                                                               |
| ------------------ | --------------------------------------------------------------------------------------------------------------------------------------- |
| `dev-up.sh`        | Comodidad opcional y destructiva por defecto: `down -v` → `up --wait` → `seed-all.sh`                                                   |
| `seed-all.sh`      | Siembra completa e idempotente: cuestionarios, sesiones, equipos, usuarios y membresías mediante psql y la API del gateway             |
| `cover.sh`         | Pruebas + reporte HTML exploratorio de cobertura de un servicio                                                                          |
| `cover-gate.sh`    | Control canónico de cobertura (ADR-0005) — fuente de verdad para CI/CD                                                                   |

---

## Decisiones de arquitectura (ADR)

En `docs/adr/`:

- **ADR-0001** — Validación central de JWT en el gateway.
- **ADR-0002** — Extracción del token en conexiones WebSocket en el gateway.
- **ADR-0003** — Resumen de diseño gateway ↔ Keycloak.
- **ADR-0004** — Patrones de dominio requeridos.
- **ADR-0005** — Coverlet + MSBuild para cobertura agregada (el control).
- **ADR-0006** — Arquitectura de gestión de usuarios (HU-02).
- **ADR-0007** — Token de unión: propiedad de identidad y validación no
  consumidora.
- **ADR-0008** — Testcontainer de PostgreSQL compartido para pruebas de
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
