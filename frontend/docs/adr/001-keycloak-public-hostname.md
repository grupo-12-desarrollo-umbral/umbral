# ADR-001: Keycloak Public Hostname in Local Development

**Status:** Accepted  
**Date:** 2026-05-30  
**Context:** HU-01 Frontend — Keycloak OIDC Flow  

## Problem

Keycloak's `KC_HOSTNAME` was set to `http://keycloak:8080` in `backend/docker-compose.yml`. This works for **container-to-container** communication (e.g. the API gateway calling Keycloak), but browsers on the host machine **cannot resolve** `keycloak:8080`. After the user submitted credentials, Keycloak's login form posted to `http://keycloak:8080/...`, resulting in `ERR_NAME_NOT_RESOLVED`.

The only workaround was editing the local machine's `/etc/hosts` file, which is brittle and breaks portability across developer machines.

## Decision

Set `KC_HOSTNAME` to `http://localhost:8080` in the Keycloak container. This is a **public-facing** hostname; it only affects what Keycloak writes into its OpenID metadata and HTML forms, not what the container itself binds to internally.

Internal Docker networking continues to work because the container listens on `0.0.0.0:8080`, and other containers address it by service name (`keycloak:8080`) via Docker DNS, which is independent of `KC_HOSTNAME`.

## Changed files

| File | Before | After |
|---|---|---|
| `backend/docker-compose.yml` | `KC_HOSTNAME: http://keycloak:8080` | `KC_HOSTNAME: http://localhost:8080` |

## How it works

### Same-machine development (localhost)
1. Frontend builds auth URLs using `KEYCLOAK_URL=http://localhost:8080` (from `.env.local`)
2. Browser follows redirect to `http://localhost:8080/realms/umbral/...`
3. Keycloak serves the login page with form action `http://localhost:8080/...`
4. Browser submits credentials to `localhost:8080`
5. Keycloak redirects back to `http://localhost:3000/api/auth/callback`

No hosts file edits required.

### Cross-device testing (phone on same WiFi)

`localhost` only resolves to the machine itself. To test from another device:

1. **Find your LAN IP:**
   - Windows: `ipconfig | findstr IPv4`
   - Linux/Mac: `ip addr show` or `ifconfig`

2. **Update `backend/docker-compose.yml`:**
   ```yaml
   keycloak:
     environment:
       KC_HOSTNAME: http://192.168.1.42:8080   # your actual LAN IP
   ```

3. **Update `frontend/.env.local`:**
   ```bash
   KEYCLOAK_URL=http://192.168.1.42:8080
   NEXT_PUBLIC_KEYCLOAK_URL=http://192.168.1.42:8080
   API_GATEWAY_URL=http://192.168.1.42:8000
   ```

4. **Bind the frontend dev server to all interfaces:**
   ```bash
   pnpm dev --hostname 0.0.0.0
   ```

5. **On the other device, open:**
   ```
   http://192.168.1.42:3000/login
   ```

> **Note:** Do not use `0.0.0.0` as `KC_HOSTNAME`. Keycloak will reject it because it's not a valid hostname. Use your actual LAN IP.

## API Gateway compatibility

The API gateway's `Keycloak__Authority` remains `http://keycloak:8080/realms/umbral`. This is correct because:
- Inside Docker, `keycloak` resolves to the container's internal IP via Docker DNS.
- The gateway fetches OIDC metadata from that internal URL.
- The metadata contains the public issuer (`http://localhost:8080/realms/umbral`), which matches the JWT `iss` claim.
- The gateway validates tokens against that public issuer.

Changing `KC_HOSTNAME` does **not** break internal service communication.
