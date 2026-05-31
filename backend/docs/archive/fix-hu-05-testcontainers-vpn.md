# FIX: Testcontainers Timeout in identity-access-service (HU-05)

## Problem

All 28 integration tests in `identity-access-service` were failing with:

```
Npgsql.NpgsqlException: The operation has timed out
```

The Testcontainers container started successfully and the wait strategy passed, but EF Core's `MigrateAsync()` could never establish a TCP connection to the mapped host port.

## Root Cause

**The VPN (`tun0`) sets the iptables `FORWARD` chain policy to `DROP`.** Docker uses DNAT + the FORWARD chain to route packets from the host into containers via port mappings. With that policy active, packets to `localhost:PORT` were silently dropped — the container appeared bound but was unreachable.

This also killed **Ryuk** (Testcontainers' resource reaper), which requires its own container to start cleanly.

Confirmed via:
```
Chain FORWARD (policy DROP)   ← set by VPN
```

A previous fix attempt had added a static constructor, a custom wait strategy, and switched the image from `postgres:16-alpine` to `postgres:16` — none of those addressed the actual network block.

## Fixes Applied

### 1 — Move `TESTCONTAINERS_RYUK_DISABLED` to runsettings

File: `tests/IntegrationTests/integration.runsettings`

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <ResultsDirectory>TestResults</ResultsDirectory>
    <EnvironmentVariables>
      <TESTCONTAINERS_RYUK_DISABLED>true</TESTCONTAINERS_RYUK_DISABLED>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

The `<RunSettingsFilePath>` was already wired up in the `.csproj`. Container cleanup is handled by `PostgreSqlFixture.DisposeAsync()`.

### 2 — Restore `PostgreSqlFixture.cs` to a clean default

Removed the static constructor and custom wait strategy introduced by the failed fix attempt. The fixture now simply uses:

```csharp
private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder()
    .WithImage("postgres:16")
    .Build();
```

### 3 — Restore Docker FORWARD rules (current session)

```bash
sudo iptables -I FORWARD 1 -i docker0 -j ACCEPT
sudo iptables -I FORWARD 1 -o docker0 -j ACCEPT
```

### 4 — Make FORWARD rules permanent via NetworkManager dispatcher

File: `/etc/NetworkManager/dispatcher.d/99-docker-vpn`

```bash
#!/bin/bash
if [ "$2" = "up" ] && [[ "$1" == tun* ]]; then
    iptables -I FORWARD 1 -i docker0 -j ACCEPT
    iptables -I FORWARD 1 -o docker0 -j ACCEPT
fi
```

This script runs automatically every time the VPN connects, so no manual intervention is needed on future sessions.

## Result

```
Test summary: total: 28, failed: 0, succeeded: 28, skipped: 0, duration: 19.2s
```
