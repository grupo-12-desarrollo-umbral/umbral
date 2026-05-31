# Mission Design Service — Basics Q&A

## 1. What's the "builder" in MissionConfiguration.cs?

It's EF Core's way of configuring how the `Mission` entity maps to the database table. You use `EntityTypeBuilder<Mission>` to say things like "this column can have max 200 characters" or "this property is required".

## 2. Where do the MissionActivation enum values come from?

From `Domain/Enums/MissionActivation.cs`. Right now it only has one value: `Draft`. More will be added later as the business decides what other states a mission needs (like "ready for use" or "archived").

## 3. How does IdentityService.cs work?

It doesn't really do anything — it's a placeholder. Every method just returns a fake/default value because identity features (login, roles, etc.) aren't implemented in the mission-design-service. It exists so the code compiles but won't actually work.

## 4. What does `DbSet<Mission> Missions => Set<Mission>()` mean?

It's a shortcut to expose a `Missions` table from the database. `Set<Mission>()` is an EF Core method that returns the `DbSet` for the `Mission` entity. It's just a cleaner way to write `public DbSet<Mission> Missions { get { return Set<Mission>(); } }`.

## 5. Are we using Moq and xUnit for tests?

- **xUnit? No** — the project uses **NUnit** (not xUnit).
- **Moq? Yes** — but only in the unit test project (for mocking dependencies). The integration and functional tests don't use Moq.
