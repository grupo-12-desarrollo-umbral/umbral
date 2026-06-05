# Umbral

Umbral es una plataforma para la ejecución de sesiones en vivo de tipo misión y trivia, orientada a contextos educativos y competitivos. Permite a operadores diseñar misiones con estructura de pistas y etapas, crear cuestionarios de trivia, lanzar sesiones con equipos de participantes, registrar evidencia y obtener resultados con rankings auditables en tiempo real.

---

## Descripción general

El sistema está construido como un monorepo con dos cargas de trabajo independientes:

- **Backend** — microservicios .NET expuestos a través de un API Gateway centralizado.
- **Frontend** — aplicación Next.js consumida por operadores, administradores y participantes.

El frontend se comunica exclusivamente con el API Gateway, que valida cada request contra Keycloak e inyecta la identidad del actor como headers de confianza hacia los servicios internos. Los servicios se integran entre sí mediante eventos de dominio.

---

## Microservicios

### Identity Access

`**backend/services/identity-access-service`**

Gestiona identidad, roles y acceso dentro de la plataforma. La autenticación está delegada a Keycloak; este servicio se encarga del aprovisionamiento post-login que registra al actor como usuario conocido por la plataforma.

- Registro y gestión de usuarios con su rol (Administrador, Operador, Participante).
- Emisión y validación de `JoinToken` para el flujo de ingreso a sesiones.
- Catálogo de equipos de referencia y asociaciones para el lobby de participantes.
- Devolución de hechos de acceso (identidad, rol, validez del token) usados por los demás contextos.

> La decisión final de admisión a una sesión en vivo no pertenece a este servicio; esa autoridad recae en Session Operations.

---

### Mission Design

`**backend/services/mission-design-service**`

Permite a operadores y administradores diseñar el contenido que luego se ejecuta en sesiones en vivo. Gestiona dos tipos de fuente: misiones con estructura jerárquica de pistas y cuestionarios de trivia.

- Autoría de misiones con su árbol de nodos (Etapa → Subetapa → Pista) y puntos de validación por QR.
- Autoría de cuestionarios de trivia con preguntas y opciones de respuesta.
- Control de activación: decide si una misión o quiz está listo para ser usado como fuente de una sesión en vivo.

> Este servicio no controla la ejecución en tiempo real; es exclusivamente la autoridad sobre la estructura y preparación del contenido.

---

### Session Operations

`**backend/services/session-operations-service**`

Es el núcleo de la ejecución en vivo. Recibe una fuente (misión o trivia) y la convierte en una sesión activa con equipos, progresión de pistas y registro de evidencia.

- Ciclo de vida completo de la sesión: Programada → Preparando → Activa → Pausada → Finalizada / Cancelada.
- Decisión final de admisión de participantes (late join, reconexión, capacidad, asignación de equipo).
- Gestión de equipos, participantes y contextos de ingreso en el ámbito de sesión.
- Liberación de pistas a equipos durante la sesión activa.
- Recepción y validación de evidencia (respuestas, escaneos QR, respuestas de trivia).
- Emisión de eventos de sesión para trazabilidad, auditoría y supervisión.

> Este servicio es la única autoridad sobre el estado de runtime de la sesión. Ningún otro servicio puede mutar su estado directamente.

---

### Scoring Monitoring

`**backend/services/scoring-monitoring-service**`

Calcula, persiste y expone vistas derivadas de puntuación y monitoreo a partir de los eventos emitidos por Session Operations.

- Registro trazable de puntos otorgados o penalizados por equipo.
- Aplicación de penalizaciones con razón justificada.
- Derivación del ranking de equipos con tiempo de resolución como criterio de desempate.
- Preservación del historial de auditoría de eventos de sesión.
- Proyecciones de monitoreo para supervisión en tiempo real por operadores.

> Este servicio no controla el estado de la sesión; consume hechos de runtime y expone exclusivamente vistas de lectura.

---

## Estructura del repositorio

```
umbral/
├── backend/
│   ├── api-gateway/
│   ├── services/
│   │   ├── identity-access-service/
│   │   ├── mission-design-service/
│   │   ├── session-operations-service/
│   │   └── scoring-monitoring-service/
│   └── docker-compose.yml
├── frontend/
├── mobile/
├── CONTEXT-MAP.md
└── AGENTS.md
```

---

## Cómo levantar el proyecto

### Prerrequisitos

- [Docker](https://docs.docker.com/get-docker/) y Docker Compose v2.24+
- [Node.js](https://nodejs.org/) 20+ y [pnpm](https://pnpm.io/) (para el frontend)
- [Node.js](https://nodejs.org/) 20+ y npm (para el móvil)
- [Expo CLI](https://docs.expo.dev/get-started/installation/) (`npm install -g expo-cli`)

---

### Backend

El backend corre completamente en Docker. El comando `docker compose up` auto-carga el override de desarrollo que monta el código fuente y usa `dotnet watch run` para hot reload.

```bash
cd backend

# Desarrollo con hot reload (recompila al guardar archivos .cs)
docker compose up

# Build de producción sin hot reload
docker compose -f docker-compose.yml up --build
```

**Servicios expuestos en el host:**


| Servicio            | Puerto            |
| ------------------- | ----------------- |
| API Gateway         | `localhost:8000`  |
| Keycloak            | `localhost:8080`  |
| RabbitMQ Management | `localhost:15672` |
| PostgreSQL          | `localhost:5432`  |


Para compilar o testear un servicio individualmente:

```bash
make -C backend build SVC=<service>   # compilar
make -C backend test  SVC=<service>   # ejecutar tests
make -C backend gate  SVC=<service>   # gate de cobertura
```

Donde `<service>` es el nombre de la carpeta del servicio, por ejemplo `session-operations-service`.

---

### Frontend

La aplicación web Next.js consume el API Gateway. Requiere que el backend esté corriendo.

```bash
cd frontend

# Copiar variables de entorno y completar los valores
cp .env.local.example .env.local

# Instalar dependencias
pnpm install

# Iniciar servidor de desarrollo
pnpm dev
```

La app estará disponible en `http://localhost:3000`.

**Variables de entorno relevantes (`.env.local`):**

```env
KEYCLOAK_URL=http://localhost:8080
KEYCLOAK_REALM=umbral
KEYCLOAK_CLIENT_SECRET=      # obtener desde la consola de Keycloak
API_GATEWAY_URL=http://localhost:8000
```

---

### Mobile

La aplicación móvil está construida con Expo (React Native). Puede correrse en simulador iOS, emulador Android, o dispositivo físico vía Expo Go.

```bash
cd mobile

# Copiar variables de entorno y completar los valores
cp .env.example .env

# Instalar dependencias
npm install

# Iniciar el servidor de desarrollo
npm start
```

**Variables de entorno (`.env`):**

```env
# iOS Simulator → localhost | Android Emulator → 10.0.2.2 | Dispositivo físico → IP local
EXPO_PUBLIC_API_BASE_URL=http://<host>:8000
EXPO_PUBLIC_KEYCLOAK_URL=http://<host>:8080
EXPO_PUBLIC_KEYCLOAK_REALM=umbral
EXPO_PUBLIC_KEYCLOAK_CLIENT_ID=umbral-mobile
```

Para correr directamente en una plataforma:

```bash
npm run android   # emulador Android
npm run ios       # simulador iOS (requiere macOS)
```

