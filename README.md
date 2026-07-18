# Umbral

Umbral es una plataforma para ejecutar sesiones en vivo de tipo misión y
trivia en contextos educativos y competitivos. Permite diseñar contenido,
organizar equipos, operar sesiones, registrar evidencias y obtener rankings
auditables en tiempo real.

## Arquitectura

El monorepositorio contiene tres cargas de trabajo:

- `backend/`: API Gateway YARP y microservicios .NET.
- `frontend/`: aplicación web Next.js para administradores, operadores y
  participantes.
- `mobile/`: aplicación Expo para la experiencia móvil.

Frontend y mobile se comunican exclusivamente con el API Gateway. El gateway
valida el JWT emitido por Keycloak y genera headers de identidad confiable para
los servicios internos. Los contextos se integran por contratos HTTP y eventos;
ninguno accede a la base de datos de otro.

## Microservicios

| Servicio | Autoridad | Documentación |
| --- | --- | --- |
| Users | perfil, rol y acceso de Umbral; delega autenticación y credenciales a Keycloak | [README](backend/services/users-service/README.md) |
| Mission Design | autoría y activación de misiones y trivias | [README](backend/services/mission-design-service/README.md) |
| Session Operations | estado y operación de sesiones en vivo | [README](backend/services/session-operations-service/README.md) |
| Scoring Monitoring | puntuación, ranking, penalizaciones e historial | [README](backend/services/scoring-monitoring-service/README.md) |

Session Operations es la única autoridad sobre el runtime. Mission Design
entrega contenido preparado, Users entrega hechos de identidad y elegibilidad,
y Scoring Monitoring deriva vistas a partir de los hechos de la sesión.

Users no autentica contraseñas ni emite o valida tokens. Delega en Keycloak el
alta de la identidad, las credenciales, el login, la verificación de correo y la
recuperación de contraseña; conserva únicamente el perfil y las reglas de acceso
propias del dominio de Umbral.

Para las fronteras y contratos entre cargas, consulta
[CONTEXT-MAP.md](CONTEXT-MAP.md) y [backend/CONTEXT-MAP.md](backend/CONTEXT-MAP.md).

## Pipeline local esencial

Hay dos flujos independientes: ejecutar la solución para desarrollarla y
verificar cambios antes de integrarlos. Levantar Compose no es un prerrequisito
de la verificación.

### Prerrequisitos

- Docker con Compose v2.24 o posterior.
- Para verificar el backend en el host: GNU Make y el SDK exacto indicado en
  `backend/global.json`.
- Para desarrollar fuera de Compose: Node.js y pnpm en `frontend/`; Node.js y
  npm en `mobile/`.

### 1. Ejecutar la solución web completa

Desde la raíz, Compose levanta PostgreSQL, Keycloak, Mailpit, RabbitMQ, Seq, los
microservicios, el gateway y el frontend:

```bash
cp backend/.env.example backend/.env  # opcional: personaliza SMTP, hostname y secretos
docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml up -d --wait
```

Comprueba el estado del stack:

```bash
docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml ps
```

La siembra no forma parte del arranque esencial. Cuando necesites el catálogo y
las sesiones de demostración, ejecútala explícitamente:

```bash
./backend/scripts/seed-all.sh
```

Puntos de acceso:

| Componente | URL |
| --- | --- |
| Frontend | `http://localhost:3000` |
| API Gateway | `http://localhost:8000` |
| Keycloak | `http://localhost:8080` |
| Mailpit | `http://localhost:8025` |
| RabbitMQ Management | `http://localhost:15672` |
| Seq | `http://localhost:8341` |

Los servicios .NET se ejecutan con `dotnet watch`; los microservicios no
publican puertos directos al host. Comprueba el loop de desarrollo con:

```bash
make -C backend doctor
```

Detén el entorno sin borrar la base local con:

```bash
docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml down
```

### 2. Verificar cambios del backend

El gate local ejecuta las pruebas unitarias e integración de un servicio,
combina su cobertura y exige al menos 95 % de ramas:

```bash
make -C backend gate SVC=session-operations-service
make -C backend gate-all
```

Antes de integrar, usa `ci`: añade los controles estructurales y de Arquitectura
Limpia, compila la API y reproduce el punto de entrada de GitHub Actions. Para
el API Gateway, que no tiene un gate de Coverlet representativo, `ci` ejecuta su
build y sus pruebas.

```bash
make -C backend ci SVC=session-operations-service
make -C backend ci SVC=api-gateway
make -C backend ci-all
```

Las pruebas de integración usan Testcontainers, por lo que requieren Docker,
pero no el stack persistente de Compose.

Más detalles: [backend/README.md](backend/README.md) y
[backend/docs/local-ci.md](backend/docs/local-ci.md).

### 3. Trabajar en las interfaces

El frontend incluido en Compose sirve una compilación de la app. Para usar la
recarga de Next.js, conserva el backend levantado, libera el puerto 3000 y sigue
la guía del frontend:

```bash
docker compose -f backend/docker-compose.yml -f backend/docker-compose.override.yml stop frontend
cd frontend
pnpm install
pnpm dev
```

Consulta [frontend/README.md](frontend/README.md) para variables y pruebas.

Para la aplicación Expo:

```bash
cd mobile
cp .env.example .env
npm install
npm start
```

Consulta [mobile/README.md](mobile/README.md) para configurar la URL correcta en
simulador, emulador o dispositivo físico.

## Estructura del repositorio

```text
umbral/
├── backend/
│   ├── api-gateway/
│   ├── services/
│   │   ├── users-service/
│   │   ├── mission-design-service/
│   │   ├── session-operations-service/
│   │   └── scoring-monitoring-service/
│   ├── docker-compose.yml
│   └── docs/local-ci.md
├── frontend/
├── mobile/
├── CONTEXT-MAP.md
└── AGENTS.md
```
