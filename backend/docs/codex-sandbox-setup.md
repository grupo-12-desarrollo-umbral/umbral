# Codex sandbox setup for the agent workflow

The driver/generator agents run their build, test, and git gates under a sandbox.
For **Claude Code** this is handled automatically by the repo's
`.claude/settings.json` — `make`/`dotnet`/`docker`/`git`/`gh` are listed in
`sandbox.excludedCommands`, so they run outside the sandbox and never hit a
fail-then-retry loop.

**Codex has no equivalent committed config.** `sandbox.excludedCommands` is a
Claude-only mechanism, so each developer must configure Codex once in their
personal `~/.codex/config.toml`. Without it, every `make`/`dotnet`/`docker`/`git`
call is blocked → surfaced for approval → re-run, so each verbose build/test dump
lands in context two or three times — the single largest token sink in a
verification-only phase (this is what inflated HU-10A phase X.3).

This config is **personal and global** (it lives in your home directory and is
never committed), which is why the repo can only *document* it.

## One-time setup

Add the following to `~/.codex/config.toml`. The `approval_policy` and
`sandbox_mode` keys must be **top-level** — placed above any `[projects."…"]`
table, since TOML assigns bare keys to whichever table precedes them:

```toml
# run sandboxed without per-command prompts; only ask if a command actually fails
approval_policy = "on-failure"
sandbox_mode    = "workspace-write"

[sandbox_workspace_write]
# nuget restore, git push/fetch, gh, and the vstest loopback socket all need the network
network_access = true
# a git worktree keeps its index/refs under the MAIN repo's .git/worktrees/<name>/,
# not under the worktree dir — without this, `git add` in a worktree fails with
# "Read-only file system" on index.lock. One entry covers every worktree.
writable_roots = ["/absolute/path/to/your/umbral-clone/.git"]
```

Set `writable_roots` to the absolute path of **your main clone's `.git`**. Find
it with:

```sh
git -C /path/to/your/umbral-clone rev-parse --absolute-git-dir
```

## How to run

Launch Codex **from inside the worktree** (`cd ../umbral-hu-NN` first). In
`workspace-write` mode the current directory is the writable workspace, so the
worktree's source files are writable and the shared main `.git` is covered by
`writable_roots` above.

## What this buys you (verified 2026-06-20)

Running `make -C backend test SVC=mission-design-service` from inside a worktree:
**420 tests passed single-pass, with no approval round-trip**, including 43
Testcontainers integration tests. So:

- `make` / `dotnet` / `git` / `gh` run first-try — no fail-then-retry doubling.
- The **Docker daemon socket is reachable** under `workspace-write` +
  `network_access = true`, so Testcontainers works as-is. You do **not** need to
  add `/var/run/docker.sock` to `writable_roots`. Only revisit this if a real run
  ever round-trips on a `docker` step (e.g. a host with a different/rootless
  socket path, or `network_access` turned off).

## Notes

- `on-failure` is a safety net, not a wide-open door: the worst case is one retry
  per genuinely-failing command — it does not reintroduce the unbounded prompt
  loop.
- See also the Codex / non-Claude note in `backend/.agents/driver-agent.md`
  pre-flight step 1, which describes the same problem in the driver's own terms.
