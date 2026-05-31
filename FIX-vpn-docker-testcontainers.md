# FIX: Docker Testcontainers Failing While VPN Is Active

## Symptoms

- `ResourceReaperException: Initialization has been cancelled` (Ryuk timeout ~60s)
- `Npgsql.NpgsqlException: The operation has timed out` connecting to `localhost:PORT`
- Integration tests pass without VPN, fail immediately after connecting VPN

## Root Cause

Two separate issues triggered by the VPN (`tun0`):

1. **Ryuk (Testcontainers resource reaper)** cannot initialize because the VPN intercepts its container startup.

2. **iptables FORWARD chain** — the VPN inserts a blanket `DROP` policy that kills Docker's DNAT forwarding. When a test process connects to `localhost:PORT`, the packet goes through a DNAT → FORWARD path. With the VPN's DROP rule in place, the forwarded packet is silently dropped, causing the connection to time out.

Confirmed via:
```
Chain FORWARD (policy DROP)   ← VPN sets this
```
And routing table showing `0.0.0.0/1 via tun0` (VPN full-tunnel).

---

## Fix 1 — Disable Ryuk via `.runsettings` (committed to repo)

File: `services/mission-design-service/tests/IntegrationTests/integration.runsettings`

```xml
<?xml version="1.0" encoding="utf-8"?>
<RunSettings>
  <RunConfiguration>
    <EnvironmentVariables>
      <TESTCONTAINERS_RYUK_DISABLED>true</TESTCONTAINERS_RYUK_DISABLED>
    </EnvironmentVariables>
  </RunConfiguration>
</RunSettings>
```

Referenced in `Infrastructure.IntegrationTests.csproj`:
```xml
<RunSettingsFilePath>$(MSBuildProjectDirectory)\integration.runsettings</RunSettingsFilePath>
```

Container cleanup is handled by the `PostgreSqlFixture.DisposeAsync()` instead.

---

## Fix 2 — Restore Docker FORWARD rules after VPN connects (one-time system setup)

### Apply immediately (current session)

```bash
sudo iptables -I FORWARD 1 -i docker0 -j ACCEPT
sudo iptables -I FORWARD 1 -o docker0 -j ACCEPT
```

### Make permanent via NetworkManager dispatcher

```bash
sudo cp /tmp/99-docker-vpn /etc/NetworkManager/dispatcher.d/99-docker-vpn
sudo chmod +x /etc/NetworkManager/dispatcher.d/99-docker-vpn
```

Contents of `/etc/NetworkManager/dispatcher.d/99-docker-vpn`:
```bash
#!/bin/bash
if [ "$2" = "up" ] && [[ "$1" == tun* ]]; then
    iptables -I FORWARD 1 -i docker0 -j ACCEPT
    iptables -I FORWARD 1 -o docker0 -j ACCEPT
fi
```

This script runs automatically every time a VPN connection (`tun*` interface) comes up.

---

## Fix 3 — Remove `EnsureDeletedAsync()` from integration tests

`EnsureDeletedAsync()` tried to drop the `postgres` database while connected to it, causing:
```
Npgsql.PostgresException : 55006: cannot drop the currently open database
```

Since `IClassFixture<PostgreSqlFixture>` provides a fresh container per test class, calling `EnsureDeletedAsync()` is unnecessary. Replaced with `EnsureCreatedAsync()` only.

---

## Final State

| Test project | Tests | Duration |
|---|---|---|
| `Application.UnitTests` | 4 passed | ~190 ms |
| `Api.UnitTests` | 3 passed | ~70 ms |
| `Infrastructure.IntegrationTests` | 1 passed | ~1 s |

All 8 tests pass with VPN active.
