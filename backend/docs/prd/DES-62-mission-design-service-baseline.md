# DES-62 PRD - Primera implementación de MissionDesign service (HU-09 a HU-14)

Source: https://linear.app/desarrollo-equipo-12/issue/DES-62/prd-primera-implementacion-de-missiondesign-service-hu-09-a-hu-14

## Problem Statement

El equipo ya cuenta con la documentación macro del dominio, el `Context Map`, la estructura base de Clean Architecture por servicio y el backlog refinado en Linear, pero todavía no existe una especificación de producto y de implementación para aterrizar la primera entrega del bounded context `MissionDesign` en un conjunto ejecutable de slices.

Desde la perspectiva del equipo, el problema no es descubrir el dominio desde cero, sino convertir la definición existente de `Mission`, `MissionNode`, `TriviaQuiz`, `TriviaQuestion`, `TriviaOption`, `MissionActivation` y source readiness en una primera implementación concreta, ordenada y verificable que respete las reglas de negocio de `HU-09` a `HU-14`.

Sin un PRD operativo para esta primera implementación, existe riesgo de:

- empezar por carpetas o capas en lugar de empezar por vertical slices
- mezclar reglas de autoría con reglas de runtime de `SessionOperations`
- modelar estructuras incompletas o ambiguas para misiones y trivia
- retrasar la construcción por falta de una secuencia clara entre dominio, aplicación, persistencia y pruebas

## Solution

Implementar la primera entrega de `mission-design-service` como una secuencia explícita de vertical slices alineados con el backlog y con las reglas del dominio ya documentado.

La solución cubre los casos `HU-09` a `HU-14` en este orden:

1. `HU-09` gestión de misiones
2. `HU-10A` estructura jerárquica de misiones
3. `HU-10B` validaciones estructurales de misión
4. `HU-11` creación y edición de quizzes de trivia
5. `HU-14A` gestión de preguntas y opciones de trivia
6. `HU-14B` reglas de validación de preguntas de trivia
7. `HU-12` publicación y archivado de quizzes de trivia
8. `HU-13` duplicación y retiro de quizzes usados

La implementación debe comenzar por el núcleo de dominio de `Mission` y luego avanzar hacia el núcleo de `TriviaQuiz`, manteniendo la separación entre:

- borradores de autoría
- reglas de validez estructural
- reglas de source readiness para uso en sesiones en vivo

La entrega debe producir módulos profundos y estables alrededor de:

- `Mission` como aggregate root de authoring de misiones
- `MissionStructurePolicy` para invariantes de jerarquía
- `MissionActivationPolicy` para readiness de misión
- `TriviaQuiz` como aggregate root de authoring de trivia
- `TriviaPublicationPolicy` para jugabilidad y publicación

## User Stories

1. Como Administrador, quiero crear una `Mission` con datos básicos, para mantener disponible el contenido base de sesiones de misión.
2. Como Administrador, quiero consultar el catálogo de `Mission`, para identificar cuáles están activas, desactivadas o listas para uso futuro.
3. Como Administrador, quiero editar una `Mission` existente, para corregir briefing, dificultad o tiempo máximo sin recrearla.
4. Como Administrador, quiero desactivar una `Mission` sin eliminarla, para impedir nuevas sesiones y conservar trazabilidad histórica.
5. Como Administrador, quiero que una `Mission` desactivada no pueda ser considerada source-ready, para evitar sesiones nuevas sobre contenido retirado.
6. Como Administrador, quiero modelar la estructura de una `Mission` con `Stage`, `Substage` y `Clue`, para representar el recorrido de la experiencia.
7. Como Administrador, quiero agregar `MissionNode` en una jerarquía acotada, para que la estructura responda al lenguaje del dominio y no a un workflow genérico.
8. Como Administrador, quiero que cada `Stage` tenga `Substage`, para que la misión no quede estructuralmente incompleta para uso en vivo.
9. Como Administrador, quiero que una `Substage` pueda contener `Clue`, para preparar el avance jugable que verá el equipo participante.
10. Como Administrador, quiero que una `Clue` no pueda contener nodos hijos, para preservar la jerarquía permitida.
11. Como Administrador, quiero que el sistema rechace una `Clue` directa debajo de `Stage`, para evitar combinaciones inválidas.
12. Como Administrador, quiero que el sistema rechace una `Substage` debajo de otra `Substage`, para mantener la profundidad máxima comprometida.
13. Como Administrador, quiero recibir un rechazo explícito cuando la estructura propuesta sea inválida, para corregir el contenido sin ambigüedad.
14. Como Administrador, quiero crear un `TriviaQuiz`, para preparar contenido del modo `Trivia` antes de su publicación.
15. Como Administrador, quiero editar un `TriviaQuiz` mientras su estado lo permita, para iterar sobre su contenido antes de usarlo en vivo.
16. Como Administrador, quiero conservar juntas la información básica del `TriviaQuiz` y sus `TriviaQuestion`, para revisar el contenido completo antes de publicarlo.
17. Como Administrador, quiero agregar `TriviaQuestion` a un `TriviaQuiz`, para dejarlo listo para jugar.
18. Como Administrador, quiero registrar entre 2 y 4 `TriviaOption` por pregunta, para mantener la jugabilidad prevista por el backlog.
19. Como Administrador, quiero definir exactamente una opción correcta por pregunta, para que la resolución del juego sea inequívoca.
20. Como Administrador, quiero registrar puntaje y temporizador por pregunta dentro del rango permitido, para que cada pregunta sea jugable y comparable.
21. Como Administrador, quiero guardar una explicación opcional por pregunta, para que pueda mostrarse después del cierre de la ronda.
22. Como Administrador, quiero que el sistema rechace preguntas con reglas inválidas de opciones, puntaje o tiempo, para no publicar contenido defectuoso.
23. Como Administrador, quiero publicar solo `TriviaQuiz` válidos, para que `SessionOperations` use únicamente `SessionSource` jugables.
24. Como Administrador, quiero archivar un `TriviaQuiz`, para retirarlo de nuevas sesiones sin perder su referencia histórica.
25. Como Administrador, quiero que un `TriviaQuiz` en borrador o archivado no pueda ser usado en nuevas sesiones, para mantener control sobre la source readiness.
26. Como Administrador, quiero duplicar un `TriviaQuiz` usado, para reutilizar su estructura sin alterar su historial original.
27. Como Administrador, quiero impedir el borrado destructivo de quizzes usados en sesiones, para conservar trazabilidad académica y operativa.
28. Como Administrador, quiero consultar el detalle de una `Mission` o `TriviaQuiz`, para revisar su estado de authoring antes de activación o publicación.
29. Como `SessionOperations`, quiero consumir facts de source readiness en lugar de mutar contenido de `MissionDesign`, para respetar ownership entre bounded contexts.
30. Como equipo de desarrollo, quiero implementar `MissionDesign` por vertical slices con pruebas de dominio y de aplicación, para reducir ambigüedad y asegurar avance incremental.

## Implementation Decisions

- La primera implementación de `MissionDesign` se hará por vertical slices orientados al backlog, no por carpetas completas ni por una carga masiva de todo el `Domain`.
- El orden oficial de entrega será `HU-09`, `HU-10A`, `HU-10B`, `HU-11`, `HU-14A`, `HU-14B`, `HU-12`, `HU-13`.
- `MissionDesign` mantendrá dos aggregate roots: `Mission` y `TriviaQuiz`. Sus child entities serán `MissionNode`, `TriviaQuestion` y `TriviaOption`. `Target` queda reconocido como parte del bounded context, pero no es necesario para esta primera implementación.
- `Mission` encapsulará authoring de datos básicos, estructura y estado de activación. `TriviaQuiz` encapsulará authoring de preguntas, opciones y ciclo de vida de publicación.
- Se separarán explícitamente las reglas de borrador de authoring de las reglas de source readiness.
- `MissionActivation` se tratará como estado de readiness de `Mission`, no como concepto de runtime.
- `MissionStructurePolicy` se extraerá como módulo profundo para encapsular las invariantes de jerarquía de `MissionNode`.
- `MissionActivationPolicy` se extraerá como módulo profundo para decidir si una `Mission` está lista para uso en vivo.
- `TriviaPublicationPolicy` se extraerá como módulo profundo para decidir si un `TriviaQuiz` es publicable.
- Los value objects mínimos: `Difficulty`, `MaximumTime`, `QuestionTimer`.
- La jerarquía permitida de `MissionNode`: `Stage -> Substage -> Clue`. No se permite `Clue` directamente bajo `Stage`, ni `Substage` bajo `Substage`, ni hijos bajo `Clue`.
- La regla "cada `Stage` debe contener al menos una `Substage`" se trata como criterio de activación/readiness.
- El flujo mínimo de `TriviaQuiz`: `Draft`, `Published`, `Archived`.
- Cada `TriviaQuestion` deberá cumplir entre 2 y 4 `TriviaOption`, exactamente una correcta, score válido y `QuestionTimer` dentro de rango.
- Domain events prioritarios: `MissionCreated`, `MissionDetailsUpdated`, `MissionNodeAdded`, `MissionNodeUpdated`, `MissionNodeRemoved`, `MissionStructureChanged`, `MissionDeactivated`, `TriviaQuizCreated`, `TriviaQuizDetailsUpdated`, `TriviaQuestionAdded`, `TriviaQuestionUpdated`, `TriviaQuestionRemoved`, `TriviaQuizPublished`, `TriviaQuizArchived`.
- Application interfaces mínimas: `CreateMission`, `UpdateMission`, `DeactivateMission`, `AddMissionNode`, `UpdateMissionNode`, `RemoveMissionNode`, `GetMissionCatalog`, `GetMissionDetail`, `CreateTriviaQuiz`, `UpdateTriviaQuiz`, `AddTriviaQuestion`, `UpdateTriviaQuestion`, `RemoveTriviaQuestion`, `PublishTriviaQuiz`, `ArchiveTriviaQuiz`, `GetTriviaCatalog`, `GetTriviaDetail`.
  - **Estado de implementación (HU-14A):** `RemoveTriviaQuestion` —y su evento de dominio `TriviaQuestionRemoved`— quedan **diferidos**, no son un faltante. El baseline de autoría no expone un slot de eliminación de pregunta en el agregado (`TriviaQuiz` solo ofrece `AddQuestion`/`UpdateQuestion`), por lo que HU-14A acota el alcance a add/update según la condición de gate de su propio prompt (`prompt_example_feature_hu14a.md`). Implementar la eliminación exige comportamiento de dominio nuevo (método `RemoveQuestion`, evento, reconciliación de `sequenceOrder`, guardas de estado publicado) y debe planificarse como slice aparte. Rationale completo en `backend/docs/hu14a-context.md`.
- Repositories: `IMissionRepository`, `ITriviaQuizRepository`, `IMissionReadModelRepository`, `ITriviaQuizReadModelRepository`.

## Testing Decisions

- Una buena prueba validará comportamiento observable del dominio y de la aplicación, no detalles internos de implementación ni estructura de carpetas.
- Las pruebas de dominio serán la primera prioridad: invariantes, transitions y rechazo de estructuras inválidas.
- `MissionStructurePolicy` deberá tener pruebas aisladas para combinaciones válidas e inválidas entre `Stage`, `Substage` y `Clue`.
- `TriviaPublicationPolicy` deberá tener pruebas aisladas para publishable y non-publishable quizzes.
- Los handlers de aplicación probarán orquestación, uso correcto de repositories y políticas.
- Las pruebas de integración cubrirán al menos persistencia de aggregates y lectura coherente de catálogos/detalles.

## Out of Scope

- Creación de `LiveSession` o cualquier runtime authority de `SessionOperations`
- Flujos de participación, join, reconexión, liberación de pistas, evidencia, QR o progreso en vivo
- Cálculo de score, ranking, penalizaciones, monitoring o audit history
- Publicación de eventos a RabbitMQ como integración cross-service
- Modelado detallado de `Target` y flujos QR

## Further Notes

- Esta PRD usa como fuentes normativas el `MissionDesign` context local, el DDD solution model, el entity spec lógico y las user stories `HU-09` a `HU-14`.
- La primera implementación debe comenzar por el lado `Mission` y no por el lado `TriviaQuiz` en paralelo.
- Si al implementar aparecen rangos numéricos faltantes para score o timer, esos rangos deberán fijarse como reglas explícitas del dominio.
