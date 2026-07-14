# HU-14A — Manual Test: admin removes a trivia question

Goal: an **administrator** deletes an individual question (and its options)
from a draft `TriviaQuiz` via the dashboard UI. The remaining questions
re-sequence automatically, and the action is blocked for published/archived
quizzes.

You only need the web dashboard on `localhost:3000`.

---

## 1. Bring up the stack (once)

```bash
# backend + gateway + Keycloak
cd backend && docker compose up -d

# frontend
cd ../frontend && pnpm install && pnpm dev   # -> http://localhost:3000
```

## 2. Credentials

| Who  | Where                | Login                  |
| ---- | -------------------- | ---------------------- |
| Admin | web `localhost:3000` | `admin` / `admin123`   |

## 3. Run it

### Step A — Create a draft quiz with two questions

1. Sign in as **admin** at `localhost:3000`.
2. Left nav → **Trivias** → click **Create trivia quiz**.
3. Fill title (e.g. "Delete Question Test") and description → **Submit**.
4. On the detail view, click **Add question**. Fill prompt, sequence order `1`,
   score, timer, two options (mark one correct) → **Submit**.
5. Add a second question with sequence order `2` → **Submit**.
   The detail view now lists two questions.

### Step B — Remove the first question

1. On the first question row, click the **delete** (trash) button.
2. A confirmation dialog appears → click **Confirm**.
3. The question disappears. The remaining question now has sequence order `1`
   (reconciled from the backend response).

### What you must see

- Only one question remains, numbered `1`.
- No error banner appears.
- The quiz stays in **Draft** status.

That delete is HU-14A working. ✅

## 4. Quick extra checks (optional)

| Check                        | How                                                              | Expect                                                                      |
| ---------------------------- | ---------------------------------------------------------------- | --------------------------------------------------------------------------- |
| Cancel does not delete       | Open delete confirmation on a question → click **Cancel**        | Question stays, no network call                                             |
| Blocked for published quiz   | Publish the quiz, reopen detail                                  | No delete button visible on any question row                                |
| Blocked for archived quiz    | Archive a quiz, reopen detail                                    | No delete button visible on any question row                                |
| Sequence reconciliation      | Create quiz with 3 questions, delete #2                          | Remaining questions re-number 1, 2 (no gap)                                 |
| Error state resets on nav    | Trigger a delete error (e.g. delete already-deleted), then click **Add question** | `question-error` clears, form opens cleanly            |

---

## Troubleshooting

- **No delete button on question row.** The quiz is published or archived —
  only draft quizzes allow deletion. Create a new draft quiz.
- **Delete returns 409.** The quiz was published/archived between page load and
  click. Refresh and verify status.
- **Sequence order not updating.** The response may not have returned the full
  quiz. Reload the detail view to confirm.
