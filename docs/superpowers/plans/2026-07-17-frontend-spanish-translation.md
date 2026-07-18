# Frontend Spanish Translation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Translate only the visible UI text in `TeamsPanel` and `TriviasPanel` to Spanish, then update affected frontend tests that assert the old English copy.

**Architecture:** Keep the change local and inline inside the existing dashboard panels. Update only user-facing strings, preserve test ids and backend contract values, and then adjust Playwright assertions that depend on the translated copy.

**Tech Stack:** Next.js App Router, React client components, TypeScript, Playwright

## Global Constraints

- Translate only visible UI text in `frontend/app/dashboard/TeamsPanel.tsx` and `frontend/app/dashboard/TriviasPanel.tsx`.
- Translate labels, headings, buttons, empty states, helper text, placeholders, confirmation text, and user-facing error messages shown by these two panels.
- Translate readiness messages generated inside `computeReadiness` in `TriviasPanel.tsx`.
- Leave technical terms untranslated when they appear in visible text, such as `webhook`, `QR`, and `target`.
- Do not change `data-testid`, component names, DTO names, action names, TypeScript types, or backend contract values.
- Do not introduce a broader i18n layer for this task.
- Do not change behavior, request flow, or state handling.

---

### Task 1: Translate Teams Panel Visible Copy

**Files:**
- Modify: `frontend/app/dashboard/TeamsPanel.tsx`

**Interfaces:**
- Consumes: Existing `TeamsPanel({ role }: { role: DashboardRole })` render branches and `TeamForm(...)` props.
- Produces: Spanish visible copy for all user-facing text rendered by `TeamsPanel` and `TeamForm`, with existing `data-testid` attributes unchanged.

- [ ] **Step 1: Write the failing test**

Use the existing E2E coverage as the failing test target by updating one expectation mentally before code changes. The concrete assertion that should fail before implementation is in `frontend/tests/e2e/participants.spec.ts`:

```ts
await page.getByRole('button', { name: 'Cancelar' }).first().click()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm playwright test tests/e2e/participants.spec.ts --grep "admin can cancel assign form without network call"`
Expected: FAIL because the UI still renders `Cancel` in English.

- [ ] **Step 3: Write minimal implementation**

Translate only visible strings inside `frontend/app/dashboard/TeamsPanel.tsx`, including:

```tsx
setListError('No se pudieron cargar los equipos.')
setParticipantsError('No se pudieron cargar los participantes.')
setDeactivateError('Este equipo ya está inactivo.')
setDeactivateError('No se pudo desactivar el equipo. Inténtalo de nuevo.')
setFormError('Ya existe un equipo con este código.')
setFormError('No se pudo crear el equipo. Inténtalo de nuevo.')
setFormError('Este equipo ya no existe.')
setFormError('No se pudieron guardar los cambios. Inténtalo de nuevo.')
setAssignError('Este equipo está inactivo y no puede aceptar nuevos integrantes.')
setAssignError('Este usuario ya está asignado al equipo.')
setAssignError('El usuario seleccionado no tiene el rol Participante.')
setAssignError('No se pudo realizar la asignación. Inténtalo de nuevo.')
setSessionsError('No se pudieron cargar tus sesiones asignadas.')
setSessionAssignError('Este equipo ya está asociado a esa sesión.')
setSessionAssignError('Los equipos inactivos no se pueden asignar a una sesión.')
setSessionAssignError('Solo las sesiones programadas pueden aceptar equipos nuevos.')
setSessionAssignError('El equipo o la sesión ya no existen.')
setSessionAssignError('No se pudo asignar el equipo a la sesión seleccionada.')
```

Also translate rendered copy such as:

```tsx
← Equipos
Editar
Desactivar
Confirmar
Cancelar
Nombre para mostrar
Código del equipo
Estado
Activo
Inactivo
Creado
Última actualización
Participantes
+ Asignar participante
Seleccionar participante
— Selecciona un participante —
Asignar
Cargando…
Todavía no hay participantes asignados.
Usuario
Equipo nuevo
Editar equipo
Equipos registrados
Registro de equipos. Los equipos inactivos se conservan para auditoría.
Catálogo de equipos de solo lectura.
+ Nuevo equipo
Nombre
Código
Acciones
Página {listData.page} de {listData.totalPages} ({listData.totalCount} equipos)
← Anterior
Siguiente →
Asignar equipo a una sesión
Cerrar
Estado del equipo
Sesiones disponibles
Elige una de tus sesiones programadas. Las sesiones ya en preparación o en vivo no pueden aceptar equipos nuevos.
No tienes sesiones programadas disponibles para este equipo.
Operador responsable
Tú
Asignar a la sesión
```

Translate `TeamForm` validation and button copy too:

```tsx
if (!displayName.trim()) errors.displayName = 'El nombre para mostrar es obligatorio.'
if (!teamCode.trim()) errors.teamCode = 'El código del equipo es obligatorio.'
```

And:

```tsx
<label htmlFor="team-display-name">Nombre para mostrar</label>
<label htmlFor="team-code">Código del equipo</label>
{mode === 'create' ? 'Crear equipo' : 'Guardar cambios'}
Cancelar
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm playwright test tests/e2e/participants.spec.ts --grep "admin can cancel assign form without network call"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add frontend/app/dashboard/TeamsPanel.tsx
git commit -m "feat: translate teams panel to Spanish"
```

### Task 2: Translate Trivias Panel Visible Copy

**Files:**
- Modify: `frontend/app/dashboard/TriviasPanel.tsx`

**Interfaces:**
- Consumes: Existing `TriviasPanel({ role }: { role: DashboardRole })`, `computeReadiness(quiz: TriviaQuizDto)`, `TriviaQuizForm(...)`, and `TriviaQuestionForm(...)`.
- Produces: Spanish visible copy for all user-facing text rendered by `TriviasPanel`, `TriviaQuizForm`, and `TriviaQuestionForm`, while keeping status values rendered from backend contract fields unchanged unless already mapped locally.

- [ ] **Step 1: Write the failing test**

Use the existing E2E selector that depends on visible copy in `frontend/tests/e2e/trivias.spec.ts`:

```ts
await page.getByRole('button', { name: '← Volver a trivias' }).click()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm playwright test tests/e2e/trivias.spec.ts --grep "edit button is disabled for non-Draft quizzes"`
Expected: FAIL because the detail screen still renders `← Back to trivia quizzes`.

- [ ] **Step 3: Write minimal implementation**

Translate only visible strings inside `frontend/app/dashboard/TriviasPanel.tsx`, including error messages:

```tsx
setListError('No se pudieron cargar las trivias.')
setListError('No se pudieron cargar los detalles de la trivia.')
setFormError('El título y la descripción son obligatorios (máximo 200 y 2000 caracteres).')
setFormError('No se pudo crear la trivia. Inténtalo de nuevo.')
setFormError('La trivia ya no existe.')
setFormError('Esta trivia ya no se puede editar (ya no está en estado Draft).')
setFormError('No se pudo actualizar la trivia. Inténtalo de nuevo.')
setQuestionError('Pregunta inválida. Revisa todos los campos y asegúrate de que haya exactamente una opción correcta.')
setQuestionError('No se pudo agregar la pregunta. Inténtalo de nuevo.')
setQuestionError('No se pudo actualizar la pregunta. Inténtalo de nuevo.')
setQuestionError('Esa pregunta ya no existe. Recarga la trivia e inténtalo de nuevo.')
setQuestionError('Esta trivia ya no se puede editar (ya no está en estado Draft).')
setQuestionError('No se pudo quitar la pregunta. Inténtalo de nuevo.')
setLifecycleError('Falló la publicación. Asegúrate de que la trivia esté en estado Draft y que todas las preguntas tengan puntaje, tiempo límite y opciones válidas.')
setLifecycleError('No se pudo publicar la trivia. Inténtalo de nuevo.')
setLifecycleError('Esta trivia no se puede archivar en su estado actual.')
setLifecycleError('No se pudo archivar la trivia. Inténtalo de nuevo.')
setLifecycleError('No se puede duplicar una trivia archivada.')
setLifecycleError('No se pudo duplicar la trivia. Inténtalo de nuevo.')
setLifecycleError(result.detail ?? 'Esta trivia no se puede retirar en su estado actual.')
setLifecycleError('No se pudo retirar la trivia. Inténtalo de nuevo.')
```

Translate readiness copy and visible labels:

```tsx
reasons.push('Se requiere al menos una pregunta.')
const questionLabel = `Pregunta ${index + 1}`
reasons.push(`${questionLabel}: el puntaje es obligatorio.`)
reasons.push(`${questionLabel}: el límite de tiempo es obligatorio.`)
reasons.push(`${questionLabel}: debe tener entre 2 y 4 opciones.`)
reasons.push(`${questionLabel}: debe haber exactamente una opción correcta.`)
```

Translate rendered copy such as:

```tsx
Preguntas
Agregar pregunta
Confirmar eliminación
Cancelar
Editar
Quitar
Agregar pregunta
Editar pregunta
← Volver a trivias
Descripción
Estado:
Lista como origen:
Sí
No
Copiada de:
Trivia #{selectedQuiz.sourceTriviaQuizId}
Tiene historial de uso
Preparación para publicar
Publicar
Confirmar publicación
Archivar
Confirmar archivado
Duplicar
Confirmar duplicación
Retirar
Confirmar retiro
Crear trivia
Editar trivia
Trivias
Título
Descripción
Lista como origen
Procedencia
Copia
Usada
Ver detalle
Crear trivia
Guardar
Cancelar
Título de la trivia
Ingresa el título de la trivia
Describe la trivia
Enunciado
Ingresa el enunciado de la pregunta
Puntaje
{scoreValue} puntos (fijo)
Tiempo límite (15–30 segundos)
Explicación (opcional)
Explica por qué la respuesta correcta es la indicada
Activa
Opciones (2–4)
Opción {index + 1}
Marcar como correcta
Correcta
+ Agregar opción
```

Translate form validation too:

```tsx
setFormError('El tiempo límite es obligatorio y debe estar entre 15 y 30 segundos.')
setFormError('Exactamente una opción debe estar marcada como correcta.')
setFormError('Todos los textos de las opciones son obligatorios.')
setFormError('Una pregunta debe tener entre 2 y 4 opciones.')
```

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm playwright test tests/e2e/trivias.spec.ts --grep "edit button is disabled for non-Draft quizzes"`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add frontend/app/dashboard/TriviasPanel.tsx
git commit -m "feat: translate trivias panel to Spanish"
```

### Task 3: Update E2E Tests for Spanish Copy

**Files:**
- Modify: `frontend/tests/e2e/participants.spec.ts`
- Modify: `frontend/tests/e2e/trivias.spec.ts`

**Interfaces:**
- Consumes: Existing Playwright selectors and unchanged `data-testid` values from `TeamsPanel.tsx` and `TriviasPanel.tsx`.
- Produces: E2E assertions aligned with the new Spanish visible copy while keeping behavior coverage unchanged.

- [ ] **Step 1: Write the failing test**

Use the full E2E files as the failing test surface after translating UI strings. The concrete text assertions that must be updated include:

```ts
await page.getByRole('button', { name: 'Cancelar' }).first().click()
await expect(page.locator('[data-testid="detail-status"]')).toContainText('Activo')
await page.getByRole('button', { name: '← Volver a trivias' }).click()
await page.locator('[data-testid="question-form"] button[type="button"]').filter({ hasText: 'Cancelar' }).click()
```

- [ ] **Step 2: Run test to verify it fails**

Run: `pnpm playwright test tests/e2e/participants.spec.ts tests/e2e/trivias.spec.ts`
Expected: FAIL on expectations that still look for English copy such as `Cancel`, `Active`, or `← Back to trivia quizzes`.

- [ ] **Step 3: Write minimal implementation**

Update only user-visible text assertions in the two E2E files:

```ts
// participants.spec.ts
await expect(page.locator('[data-testid="detail-status"]')).toHaveText(/(Activo|Inactivo)/)
if (statusText?.includes('Activo')) {
  await expect(page.locator('[data-testid="assign-participant-btn"]')).toBeVisible()
}
await expect(page.locator('[data-testid="detail-status"]')).toContainText('Activo')
await page.getByRole('button', { name: 'Cancelar' }).first().click()
```

```ts
// trivias.spec.ts
await page.getByRole('button', { name: '← Volver a trivias' }).click()
await page.locator('[data-testid="question-form"] button[type="button"]').filter({ hasText: 'Cancelar' }).click()
```

Search `frontend/tests/e2e/trivias.spec.ts` for every exact occurrence of `← Back to trivia quizzes` and replace it with `← Volver a trivias`.

- [ ] **Step 4: Run test to verify it passes**

Run: `pnpm playwright test tests/e2e/participants.spec.ts tests/e2e/trivias.spec.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add frontend/tests/e2e/participants.spec.ts frontend/tests/e2e/trivias.spec.ts
git commit -m "test: update dashboard copy assertions to Spanish"
```
