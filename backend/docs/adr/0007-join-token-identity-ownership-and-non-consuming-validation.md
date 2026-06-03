# JoinToken is owned by Identity; membership validation is non-consuming

`JoinToken` is a **`Identity`-owned** entity, not a `SessionOperations` entity, because it models entry *authorization* (who may enter which team/session pair), not live-session runtime state. Identity issues and stores tokens (`join_tokens` table) alongside the `TeamMembership` facts it already owns; `SessionOperations` receives only the resulting access facts.

The two reference ids on `JoinToken` are intentionally asymmetric:

- `TeamId` is the **Identity reference-data Team id** (HU-04 — the same id `TeamMembership` keys on). This is the only team identity `identity-access-service` can validate membership against; we assume the runtime `Team` in `session-operations-service` carries this same id as its origin.
- `LiveSessionId` is an **opaque correlation id**: Identity stores and echoes it but never validates it against local data, because Identity has no `LiveSession`. Neither field becomes an EF foreign key — cross-context FKs would collapse the bounded-context boundary.

`ValidateParticipantMembershipAccess` is **read-only / non-consuming**: it checks whether a supplied token is active and matches the target pair, but does not call `Consume()`. Single-use consumption happens at actual admission (owned by `SessionOperations`) and must remain available for reconnection (HU-07B). Burning the token at the validation step would break the join and reconnection flows.

## Token generation and hashing

Identity stores only a **hash** of the join token (`join_tokens.token_hash`), never the plaintext. The plaintext is returned once at issuance; `ValidateParticipantMembershipAccess` recomputes the hash of the supplied token and looks the row up by it. This lookup-by-hash design forces the hash to be **deterministic** — the same plaintext must always yield the same hash — which rules out per-call salted schemes (bcrypt/Argon2 with a random salt would never match on lookup).

The implementation (`IJoinTokenTokenService` in Application, realized by `JoinTokenTokenService` in Infrastructure) therefore uses:

- **`GenerateToken()`** — 256 bits from a CSPRNG (`RandomNumberGenerator`), Base64Url-encoded. The high entropy is what makes the token unguessable; the hash is not relied on for that.
- **`HashToken()`** — deterministic SHA-256, hex-encoded. Adequate against brute force precisely because the input is already 256-bit random, not a low-entropy secret.

**Upgrade path:** if defense against an offline DB leak is later required, `HashToken` can move to **HMAC-SHA256 with a server-side secret (pepper)**. It stays deterministic, so the lookup design is unaffected; the only new requirement is secret configuration.

Separately, timestamps (`IssuedAt`/`ExpiresAt`/`ConsumedAt`) are normalized to **microsecond** precision at the `JoinToken` domain boundary, because PostgreSQL `timestamptz` stores 6 fractional digits while .NET `DateTimeOffset` carries 7 (100-ns ticks). Normalizing in the domain keeps the in-memory model, persisted rows, and emitted domain events in exact agreement.

## Consequences

- A future "admit participant" path in `SessionOperations` must call back to Identity (or receive the token hash) to perform the single consumption — it cannot assume the token was consumed by the membership check.
- If `SessionOperations` ever mints runtime `Team` ids that differ from the Identity reference-data `Team` id, an explicit cross-context id mapping will be required before membership validation can resolve correctly.
- Token validation is a hash lookup, so the issued plaintext is unrecoverable from storage; a lost token can only be reissued, not retrieved.
