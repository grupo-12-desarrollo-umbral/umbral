# Evidencia de principios SOLID en UMBRAL

Este documento justifica la aplicación de **SRP, OCP, LSP, ISP y DIP** mediante
componentes reales del backend de UMBRAL. Su propósito es aportar evidencia
trazable para la memoria técnica y la defensa académica; no pretende afirmar
que toda clase del sistema sea un ejemplo perfecto de los cinco principios.

## Resumen

| Principio | Evidencia principal | Verificación automatizada |
| --- | --- | --- |
| SRP | Un enlace por regla de validación contextual de evidencias | `EvidenceValidationChainTests` |
| OCP | Nuevos enlaces de validación sin cambiar el algoritmo de la cadena | `EvidenceValidationChainTests` |
| LSP | Estados de sesión sustituibles mediante `ILiveSessionState` | `LiveSessionTests` y `LiveSessionGuardBranchTests` |
| ISP | Puertos de difusión separados por tipo de notificación | Pruebas unitarias de difusores y manejadores |
| DIP | La orquestación depende de puertos de Application; EF Core y SignalR son adaptadores externos | `TriviaRoundOrchestratorFacadeTests` y pruebas de adaptadores |

## SRP — Principio de responsabilidad única

**Criterio aplicado:** una clase debe tener una sola responsabilidad principal
y, por tanto, una sola razón de negocio para cambiar.

La validación contextual de evidencias separa cada regla en un enlace concreto:

- [`ActiveSubstageBindingLink`](../services/session-operations-service/src/Application/Sessions/Common/EvidenceValidation/Validators/ActiveSubstageBindingLink.cs)
  comprueba exclusivamente que la evidencia permanezca asociada a la subetapa
  activa resuelta.
- [`SubmissionOriginLink`](../services/session-operations-service/src/Application/Sessions/Common/EvidenceValidation/Validators/SubmissionOriginLink.cs)
  comprueba exclusivamente que el origen declarado corresponda al tipo de
  evidencia registrado.
- [`SubmissionWindowLink`](../services/session-operations-service/src/Application/Sessions/Common/EvidenceValidation/Validators/SubmissionWindowLink.cs)
  comprueba exclusivamente que un escaneo de búsqueda del tesoro ocurra dentro
  de la ventana temporal permitida.

Una modificación en la regla de tiempo no obliga a cambiar las reglas de origen
o asociación con la subetapa. Cada regla puede probarse y evolucionar de forma
independiente.

La composición y el orden se verifican en
[`EvidenceValidationChainTests`](../services/session-operations-service/tests/Application.UnitTests/Sessions/Facades/EvidenceValidationChainTests.cs).

### Límite reconocido

SRP se evalúa por responsabilidades y razones de cambio, no por el número de
líneas. Aun así, componentes grandes de la interfaz web, como
`frontend/app/dashboard/DashboardClient.tsx`, constituyen deuda de diseño y no
se presentan como evidencia positiva de este principio.

## OCP — Principio abierto/cerrado

**Criterio aplicado:** la lógica estable debe estar cerrada a modificaciones
accidentales, pero abierta a incorporar nuevas variantes mediante extensiones.

[`EvidenceValidationLink`](../services/session-operations-service/src/Application/Sessions/Common/EvidenceValidation/EvidenceValidationLink.cs)
define el algoritmo estable: ejecutar la comprobación del enlace y, si tiene
éxito, delegar al siguiente. Cada regla concreta solo implementa `CheckAsync`.

[`EvidenceValidationChain`](../services/session-operations-service/src/Application/Sessions/Common/EvidenceValidation/EvidenceValidationChain.cs)
construye la secuencia a partir de una colección inyectada. Una nueva regla se
incorpora creando otro `EvidenceValidationLink` y registrándolo en el punto de
composición. No es necesario modificar el algoritmo de la cadena ni las reglas
existentes.

Por ejemplo, una futura comprobación `TeamMembershipLink` podría agregarse sin
cambiar `SubmissionWindowLink`, `SubmissionOriginLink` o
`ActiveSubstageBindingLink`. La raíz de composición sí debe conocer la nueva
implementación. Ese cambio de configuración es deliberado y no altera
la lógica estable del patrón.

[`EvidenceValidationChainTests`](../services/session-operations-service/tests/Application.UnitTests/Sessions/Facades/EvidenceValidationChainTests.cs)
verifica la ejecución en orden, la cadena vacía y la interrupción cuando un
enlace rechaza la solicitud.

## LSP — Principio de sustitución de Liskov

**Criterio aplicado:** las implementaciones de un contrato deben poder
sustituirse sin obligar al consumidor a conocer su tipo concreto ni romper las
precondiciones y resultados definidos por ese contrato.

El ciclo de vida de una sesión se expresa mediante
[`ILiveSessionState`](../services/session-operations-service/src/Domain/Services/SessionStates/ILiveSessionState.cs).
Sus implementaciones representan los estados Scheduled, Preparing, Active,
Paused, Finished y Cancelled. Todas se resuelven por el mismo contrato mediante
[`LiveSessionStateFactory`](../services/session-operations-service/src/Domain/Services/SessionStates/LiveSessionStateFactory.cs).

`LiveSession` usa el estado resuelto sin comprobar la clase concreta para:

- determinar transiciones válidas;
- aplicar la entrada al nuevo estado;
- consultar y actualizar los temporizadores;
- autorizar o rechazar el registro de evidencias;
- autorizar o rechazar el avance de subetapa.

[`ActiveLiveSessionState`](../services/session-operations-service/src/Domain/Services/SessionStates/ActiveLiveSessionState.cs)
permite registrar evidencia y avanzar la subetapa. El comportamiento por defecto
de
[`LiveSessionStateBase`](../services/session-operations-service/src/Domain/Services/SessionStates/LiveSessionStateBase.cs)
rechaza esas operaciones en los estados que no las permiten.

Este rechazo no viola LSP: forma parte del contrato explícito del ciclo de vida.
Todas las implementaciones comunican una operación inválida mediante las mismas
excepciones de dominio y conservan las invariantes de `LiveSession`; el
consumidor no realiza conversiones de tipo ni ramificaciones por subtipo.

Las transiciones permitidas, rechazadas, terminales y sus efectos se verifican
en
[`LiveSessionTests`](../services/session-operations-service/tests/UnitTests/Domain/Entities/LiveSessionTests.cs)
y
[`LiveSessionGuardBranchTests`](../services/session-operations-service/tests/Application.UnitTests/Sessions/LiveSessionGuardBranchTests.cs).

## ISP — Principio de segregación de interfaces

**Criterio aplicado:** un consumidor no debe depender de operaciones que no
necesita.

La comunicación en tiempo real se divide en puertos orientados a capacidades:

- [`ISessionStateBroadcaster`](../services/session-operations-service/src/Application/Common/Interfaces/ISessionStateBroadcaster.cs):
  cambios de estado;
- [`ISessionTimerBroadcaster`](../services/session-operations-service/src/Application/Common/Interfaces/ISessionTimerBroadcaster.cs):
  actualizaciones del temporizador;
- [`ITeamBoardBroadcaster`](../services/session-operations-service/src/Application/Common/Interfaces/ITeamBoardBroadcaster.cs):
  actualización del panel del equipo;
- [`ISessionQuestionBroadcaster`](../services/session-operations-service/src/Application/Common/Interfaces/ISessionQuestionBroadcaster.cs):
  ciclo de preguntas, avance de subetapa y revelación de ranking.

Un caso de uso que solo publica un cambio de temporizador no necesita depender
de métodos de preguntas, tableros o cambios de estado. Esto evita una interfaz
general `IRealtimeBroadcaster` con operaciones no relacionadas.

Los adaptadores pueden compartir físicamente el mismo hub de SignalR. ISP se
aplica al contrato que consume cada caso de uso, no exige un hub diferente por
interfaz.

## DIP — Principio de inversión de dependencias

**Criterio aplicado:** la lógica de alto nivel depende de abstracciones propias
del núcleo, mientras los detalles tecnológicos dependen de esas abstracciones.

[`TriviaRoundOrchestratorFacade`](../services/session-operations-service/src/Application/Sessions/Common/TriviaRoundOrchestratorFacade.cs)
depende de los siguientes puertos y políticas:

- `ILiveSessionRepository`;
- `ISessionQuestionBroadcaster`;
- `IQuestionActivationStrategy`;
- `IQuestionActivator`;
- `ISubstageAdvanceCoordinator`.

La fachada no conoce `DbContext`, consultas de Entity Framework,
`IHubContext` ni grupos de SignalR. Los detalles se implementan en las capas
externas:

- [`LiveSessionRepository`](../services/session-operations-service/src/Infrastructure/Persistence/Repositories/LiveSessionRepository.cs)
  implementa `ILiveSessionRepository` usando EF Core.
- [`SignalRSessionQuestionBroadcaster`](../services/session-operations-service/src/Api/Hubs/SignalRSessionQuestionBroadcaster.cs)
  implementa `ISessionQuestionBroadcaster` usando SignalR.

La dirección de dependencia resultante es:

```text
Domain <- Application <- Infrastructure / Api
             ^                    |
             |_____ puertos ______|
```

Application define los contratos requeridos por sus casos de uso. Api e
Infrastructure los implementan y la raíz de composición selecciona las
implementaciones mediante inyección de dependencias.

[`TriviaRoundOrchestratorFacadeTests`](../services/session-operations-service/tests/Application.UnitTests/Sessions/Facades/TriviaRoundOrchestratorFacadeTests.cs)
sustituye esos puertos por dobles de prueba. Esto permite verificar la
orquestación sin iniciar PostgreSQL, RabbitMQ o SignalR y aporta evidencia
operativa de la inversión de dependencias.

## Relación con otros patrones y controles

SOLID no sustituye los patrones de diseño del proyecto. Los principios se
refuerzan con las realizaciones de Composición, Método plantilla, Estado, Cadena
de responsabilidad, Fachada y Proxy descritas en:

- [Matriz de patrones requeridos](required_patterns_matrix.md)
- [Realizaciones de patrones por capa](adr-0012-pattern-realizations-by-layer.md)
- [Convención de ubicación de patrones](adr/0012-design-pattern-placement-convention.md)

Los comandos `make -C backend structure-guard` y
`make -C backend layer-guard` complementan esta evidencia al verificar la
organización de Application y la dirección de dependencias entre capas.

## Uso en la defensa académica

Para cada principio, la demostración debe seguir esta secuencia:

1. nombrar el cambio de negocio que el diseño busca aislar;
2. mostrar el contrato y una implementación concreta;
3. mostrar el consumidor que depende del contrato;
4. ejecutar o citar la prueba automatizada correspondiente;
5. reconocer límites conocidos en vez de afirmar que todo el sistema es
   perfectamente SOLID.

La existencia de una interfaz, por sí sola, no demuestra SOLID. La evidencia es
la separación observable de responsabilidades, la posibilidad real de
extensión o sustitución y la dirección correcta de las dependencias.
