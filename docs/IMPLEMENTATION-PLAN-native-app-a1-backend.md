# Native App · A1 — BACKEND stream (`Student.Serial` — the one migration — surfaced on `/api/me/profile`)

> Status: **Planned — not yet built** · Created 2026-06-25 · The **engine half** of phase **A1** in `docs/IMPLEMENTATION-PLAN-native-app.md` (§A1, lines 191–199). Almost nothing the player needs is new on the backend — the **entire 5C video gate** (`…/playback`, `…/playback/redeem`, `…/hls.key`, `PlaybackHandoffDto`/`PlaybackManifestDto`, the handoff/Redis-GETDEL/signed-URL machinery) and the `/api/me/profile` read already EXIST and are reused as-is. This stream adds exactly **one field**: `Student.Serial` — randomly generated, **tenant-unique**, `STU-XXXXXX` Crockford base32 — minted at registration, **backfilled** for existing rows, and surfaced as `serial` on the profile read. **This is THE ONLY migration in the entire native-app slice.**
>
> Satisfies: `FR-APP-VID-003` (randomly-generated unique serial = the watermark identity; the player renders `"{serial} · {fullName}"`). Implements **`docs/contracts/native-app-playback.md` §C** verbatim (the `serial` field, its position, its format). **Change the contract (`docs/contracts/native-app-playback.md`) first if anything moves.**
>
> Run in its **own** Claude session, parallel with the app stream (player) and ahead of the wiring stream. **File ownership: `backend/**` only.** Gate: `dotnet test -c Release` green (the one pre-existing `QuestionBank` image-test failure is the known baseline); then the **wiring** stream proves the live `serial` flows into the watermark on the Aspire stack.

---

## Design reference
No UI. This stream owns only one new wire-level field — `StudentProfileDto.serial` — consumed by the prototype's **`PLAYER`** banner watermark (`.claude/Salah Bahzad App/Secure Video App (standalone).html`), where the app composes `"{serial} · {fullName}"` into the dual-layer tiled wash + repositioning chip. Behaviour authority: contract §C + `FR-APP-VID-003`. The app side already carries `serial` in `app/lib/data/dtos/student_profile.dart` (it exposes `watermarkLabel => '$serial · $fullName'`); it only needs the backend to **populate** it.

## 1. Frozen contract (this stream)
Implements `docs/contracts/native-app-playback.md`:
- **§C** — `GET /api/me/profile` (EXISTS, `RequireStudent`) → `200 StudentProfileDto`, **+NEW `serial`**. The field sits **right after `id`, before `fullName`** in the response:
  ```jsonc
  { "id": "guid",
    "serial": "STU-7K2M9",   // NEW — randomly-generated unique serial (FR-APP-VID-003)
    "fullName": "string",     // watermark renders: "{serial} · {fullName}"
    … }
  ```
- **§C note (verbatim):** *"`Serial` is added to `Student` — randomly generated, unique, human-readable (`STU-XXXXXX`, Crockford base32; ambiguous chars excluded; uniqueness-checked). Minted at registration (`Student.Register`) and backfilled for existing students — the one migration. The watermark = `serial + fullName` (not phone)."*
- **§J** (frozen vs. stream-owned) — **Backend owns:** "the `Serial` field + migration + backfill + registration mint" and the integration tests. The routes, the DTO shape, and the device-agnostic stance are frozen; this stream does **not** widen the read, touch the gate, or alter `app-exchange`/`refresh` (those landed in A0).
- The min-version `426 outdated_app` enforcement at `redeem` is **NOT A1** — it lands in **A4** (contract §F). Do not add it here.

## 2. Pre-flight — confirm what already EXISTS (do **not** rebuild)
Re-read `backend/CLAUDE.md` (EF Core conventions, Multi-tenancy, Testing standards, Naming conventions) + master plan §A1. Then confirm in code:
- **`Student` has no `Serial` today** — `Student.cs:17-64` (property list FirebaseUid → soft-delete cols); the `Register` factory at `Student.cs:71-112` sets every field but mints no serial; `Resubmit` at `Student.cs:155-193` overwrites the editable fields only. Confirmed: nothing to remove, only to add.
- **Generator precedent to mirror** — `CodeSerialGenerator.cs:11-37`: `Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"` (`:14`, excludes ambiguous **I/L/O/U**), `GroupLength = 5` (`:15`), `Next()` (`:18`), `NextUnique(ISet<string> taken, int maxAttempts = 1000)` (`:24-34`), `RandomGroup()` via `RandomNumberGenerator.GetItems` (`:36`). **Do not edit it** — write a sibling type.
- **Seed-the-set split to replicate** — `CodeBatch.Generate(…, ISet<string> existingSerials)` (`CodeBatch.cs:43-78`) loops `CodeSerialGenerator.NextUnique(existingSerials)` (`:72`) and hands the pre-made serial string to the domain factory `Code.Mint(tenantId, batchId, sessionId, value, serial)` (`Code.cs:47-61`). This is the canonical *"application factory seeds the existing serials → the domain takes a ready-made serial"* split — replicate it for `Student.Register`.
- **EF unique-index precedent** — `CodeConfiguration.cs:21` (`builder.Property(c => c.Serial).HasMaxLength(20).IsRequired();`) and `:45` (`builder.HasIndex(c => new { c.TenantId, c.Serial }).IsUnique();`). The `:43-44` comment is the rationale to copy: *includes soft-deleted rows so a serial is never reissued; the generator seeds from this set with `IgnoreQueryFilters`.*
- **Add-column migration precedent** — `20260619143015_AddStudentPhoneNumber.cs`: `AddColumn<string>("PhoneNumber", "students", varchar(32), nullable:false, defaultValue:"")` in `Up`, `DropColumn` in `Down`.
- **Raw-SQL backfill precedent (the only one in the repo)** — `20260621072358_Phase5C_VideoLengthSeconds.cs:20` & `:31`: a hand-inserted `migrationBuilder.Sql("UPDATE … ;")` sequenced between schema ops.
- **The profile read needs NO change** — `MeProfileEndpoints.cs:26-32` (`GET ""` `.RequireStudent()`) returns `StudentProfileDto` straight from the query; `StudentProfileLoader.cs:48` projects through `ToProfileDto`. The new field rides through both automatically once added to the DTO + mapping.
- **House rules in play:** migrations are **gated** (never auto-applied; the integ factory applies them via `db.Database.MigrateAsync()` at `SalahBahazadApiFactory.cs:60`); mapping is manual `.ToProfileDto()`; the EF **global `TenantId` filter** means handlers never write per-call `Where(TenantId)` — except the registration handler, which runs **anonymously** (no tenant claim) and so reads with `IgnoreQueryFilters()` + an explicit `TenantId ==` (mirror its grade/student lookups at `RegisterStudentHandler.cs:44-67`).

## 3. Domain & Application

**3.1 Domain — `NEW backend/src/SalahBahazad.Domain/Common/StudentSerialGenerator.cs`**
Mirror `CodeSerialGenerator.cs` exactly, as a **dedicated type** (one-type-per-serial convention; keeps the `STU-` prefix and length independent of the code generator):
```csharp
public static class StudentSerialGenerator
{
    private const string Alphabet = "0123456789ABCDEFGHJKMNPQRSTVWXYZ"; // Crockford — no I/L/O/U
    private const int GroupLength = 6;                                   // one group of 6 → "STU-XXXXXX"

    public static string Next() => $"STU-{RandomGroup()}";              // collision space 32^6 ≈ 1.07e9 / tenant

    public static string NextUnique(ISet<string> taken, int maxAttempts = 1000)
    {
        for (var attempt = 0; attempt < maxAttempts; attempt++)
        {
            var serial = Next();
            if (taken.Add(serial)) return serial;
        }
        throw new InvalidOperationException("Unable to generate a unique student serial after many attempts.");
    }

    private static string RandomGroup() => new(RandomNumberGenerator.GetItems<char>(Alphabet, GroupLength));
}
```
> **OPEN QUESTION — serial length (flag for a deliberate pick).** Contract §C uses the placeholder `STU-XXXXXX` (**6** chars) but the example value `"STU-7K2M9"` is **5**. This doc chooses **one group of 6** (`GroupLength = 6`) to match the `STU-XXXXXX` placeholder. If the team prefers the 5-char example, set `GroupLength = 5` **and update the contract example to match** — do not let the two drift.

**3.2 Domain — `Student.cs` (field + mint at `Register`; stable on `Resubmit`)**
- Add the property after `FullName` (≈ `Student.cs:18`), with an XML summary citing `FR-APP-VID-003`:
  ```csharp
  /// <summary>Randomly-generated, tenant-unique watermark identity "STU-XXXXXX" (FR-APP-VID-003).
  /// Minted once at <see cref="Register"/> and never changed — the player renders "{Serial} · {FullName}".</summary>
  public string Serial { get; private set; } = string.Empty;
  ```
- `Register` (`Student.cs:71-112`): add a `string serial` parameter (it receives a ready-made serial, as `Code.Mint` does — note `Code.Mint` takes it *last*, whereas here it sits 3rd, see §3.3), guard it (`ArgumentException.ThrowIfNullOrWhiteSpace(serial);` alongside the existing guards at `:85-90`), and set `Serial = serial` in the initializer (`:95-109`). Recommended position: directly after `FirebaseUid = firebaseUid` for identity grouping.
- `Resubmit` (`Student.cs:155-193`): **do NOT** touch `Serial` — it is minted once and is stable across a reject→resubmit cycle. Add a one-line comment to that effect (mirror the `// GradeId is intentionally NOT touched …` note at `Student.cs:263`).

**3.3 Application — `RegisterStudentHandler.cs` (mint on the fresh-register branch only)**
Only the **fresh-register** branch mints a serial; the **resubmit** branch leaves the existing serial untouched.
- The resubmit branch (`:81-95`) is unchanged — `Resubmit(...)` keeps the original serial.
- In the `else` (fresh) branch (`:96-112`), **before** `Student.Register(...)`, seed the tenant's existing serials and mint a unique one — mirror the handler's own anonymous-read pattern at `:44-67` (`IgnoreQueryFilters` because there is no tenant claim here; soft-deleted rows included so a serial is never reissued — same rationale as `CodeConfiguration.cs:43-44`):
  ```csharp
  var existingSerials = await db.Students
      .IgnoreQueryFilters()
      .Where(s => s.TenantId == tenant.Id)
      .Select(s => s.Serial)
      .ToHashSetAsync(cancellationToken);
  var serial = StudentSerialGenerator.NextUnique(existingSerials);

  student = Student.Register(
      tenant.Id, claims.Uid, serial,           // ← new positional arg, mirroring the entity signature
      command.FullName, command.PhoneNumber, command.ParentPhonePrimary, command.ParentPhoneSecondary,
      command.GradeId, command.CityId, command.RegionId, command.SchoolName, command.TermsVersion, now);
  db.Students.Add(student);
  ```
- No new loader argument, no DTO command change. The `(TenantId, Serial)` unique index (§4) is the **hard** guarantee; `NextUnique` only avoids collisions up-front.

**3.4 Application — `StudentProfileDto.cs` (surface `serial`)**
- Add `string Serial` to the positional record as the **2nd** member, **after `Id`, before `FullName`** (matches contract §C ordering) — `StudentProfileDto.cs:14-28`.
- `ToProfileDto` (`StudentProfileDto.cs:47-66`): pass `s.Serial` in the **same** position. No new loader parameter.
- `StudentProfileLoader.cs` — **no change** (it returns `student.ToProfileDto(…)` at `:48`; `Serial` rides along).

## 4. Infrastructure
EF mapping only — mirror `CodeConfiguration`:
- `StudentConfiguration.cs` — after the `FirebaseUid` block (≈ `:16-17`):
  ```csharp
  builder.Property(s => s.Serial).HasMaxLength(20).IsRequired();
  ```
- With the indexes (≈ `StudentConfiguration.cs:58`):
  ```csharp
  // Serials are unique per tenant (FR-APP-VID-003). Includes soft-deleted rows so a serial is never
  // reissued; the registration handler seeds from this set with IgnoreQueryFilters.
  builder.HasIndex(s => new { s.TenantId, s.Serial }).IsUnique();
  ```
No Redis / R2 / Hangfire / rate-limit touch.

## 5. API — endpoints
**No change.** `MeProfileEndpoints.cs:26-32` (`GET /api/me/profile` `.RequireStudent()` → `.Produces<StudentProfileDto>()`) returns the DTO as-is; the new `serial` field flows through the existing query → loader → `ToProfileDto`. No new route, no new `RequireStudent` surface, no OpenAPI signature change beyond the auto-derived DTO schema. (This is a deliberate non-edit — record it so the reviewer doesn't go looking.)

## 6. Migration — **the one migration**
`NEW backend/src/SalahBahazad.Infrastructure/Persistence/Migrations/<timestamp>_AddStudentSerial.cs` (+ its `.Designer.cs`). Naming: `yyyyMMddHHmmss_Pascal.cs` (e.g. `20260625xxxxxx_AddStudentSerial.cs`). **This is the only schema change in the whole native-app slice.**

Generate via the **gated** EF flow (`/dotnet-claude-kit:ef-core`; build `-c Release` with Infrastructure-as-startup per the build-vs-VS-lock memory) — **never auto-applied** (`NFR-AVAIL-004`; the integ factory runs `MigrateAsync()` at `SalahBahazadApiFactory.cs:60`). Then **hand-edit `Up()`** to a **3-step** body so the unique index is never created over duplicate `""` values (mirrors the hand-inserted `Sql` in `Phase5C_VideoLengthSeconds`):

1. **Add the column** (mirror `AddStudentPhoneNumber`):
   ```csharp
   migrationBuilder.AddColumn<string>(
       name: "Serial", table: "students",
       type: "character varying(20)", maxLength: 20,
       nullable: false, defaultValue: "");
   ```
2. **Backfill existing rows** with a per-row serial. Ids are **UUIDv7**, whose **leading** hex is a shared, time-ordered timestamp prefix (so the first-N hex chars collide hard — verified live: 5 dev students collapsed to 2 distinct first-6 prefixes). Derive from the random **tail** instead — the **last** 6 hex chars (all valid Crockford `0-9A-F`):
   ```csharp
   migrationBuilder.Sql(
       "UPDATE students SET \"Serial\" = 'STU-' || upper(right(replace(\"Id\"::text, '-', ''), 6)) " +
       "WHERE \"Serial\" = '';");
   ```
   Well-distributed across any realistic set; the unique index below is the hard guard — a residual collision **fails the migration loudly**, which is the correct, visible outcome. *(⚠️ Do NOT use `substr(…, 1, 6)` — the UUIDv7 timestamp prefix makes it collide. This was caught live during wiring.)*
3. **Create the per-tenant unique index** (matches the EF config from §4 so the snapshot is consistent):
   ```csharp
   migrationBuilder.CreateIndex(
       name: "IX_students_TenantId_Serial", table: "students",
       columns: ["TenantId", "Serial"], unique: true);
   ```

`Down()` mirrors `AddStudentPhoneNumber` in reverse — **`DropIndex("IX_students_TenantId_Serial", "students")` then `DropColumn("Serial", "students")`.**

**Snapshot:** `AppDbContextModelSnapshot.cs` must update to reflect the new `Serial` property + the `IX_students_TenantId_Serial` unique index on `Student`. EF regenerates this when scaffolding — **verify it is committed alongside the migration** (a gated review point).

## 7. Tests (`dotnet test -c Release`)

**Unit — `NEW backend/tests/SalahBahazad.UnitTests/Domain/Common/StudentSerialGeneratorTests.cs`** (no existing generator test to mirror; model on the entity-test style in `StudentTests.cs`):
- `Next_returns_STU_prefixed_crockford_serial` — result matches `^STU-[0123456789ABCDEFGHJKMNPQRSTVWXYZ]{6}$` (asserts both the I/L/O/U exclusion **and** the length-6 pick from §3.1).
- `NextUnique_skips_values_already_in_the_set` — seed a set, assert the result is not in the seed and was added.
- `NextUnique_throws_after_maxAttempts` — pass `maxAttempts: 0` (or a set whose `Add` always returns false) → `InvalidOperationException` (mirrors `CodeSerialGenerator`'s contract).

**Unit — `backend/tests/SalahBahazad.UnitTests/Domain/StudentTests.cs`:**
- Update the `NewPending()` helper (`:10-22`) and the two inline `Register` calls (`:46-48`, `:56-58`) to pass a serial arg (e.g. `"STU-TEST01"`).
- New `Register_sets_the_provided_serial` — asserts `student.Serial == "STU-TEST01"`.
- New `Register_throws_when_serial_blank` (Theory `""`, `"   "`) — mirrors the existing blank-field guards (`Register_throws_when_fullName_blank` at `:41-51`).
- New `Resubmit_keeps_the_original_serial` — Register with a serial → `Reject(...)` → `Resubmit(...)` → `Serial` unchanged (proves stability per §3.2).

**Unit — `backend/tests/SalahBahazad.UnitTests/Features/Auth/AuthTestHelpers.cs`:** update the `Student.Register(...)` call at `:84-86` to pass a serial (e.g. `StudentSerialGenerator.Next()` or a literal).

**Integration (WebApplicationFactory + Testcontainers — Postgres + Redis):**
- **Seed site** — `backend/tests/SalahBahazad.IntegrationTests/SalahBahazadApiFactory.cs`: `SeedStudentAsync` calls `Student.Register(...)` at `:237-239` — pass `StudentSerialGenerator.Next()` so every seeded student gets a valid, random serial. *(No production seeder constructs `Student`s — `ReferenceData` seeds only City/Region — so this factory + the two unit helpers above are the only seed sites.)*
- **Contract mirror** — `backend/tests/SalahBahazad.IntegrationTests/StudentS6Contracts.cs`: add `string Serial` to `StudentProfileResponse` as the **2nd** member (after `Id`, `:11`) so deserialization sees it.
- `MeProfileApiTests.Get_returns_the_callers_profile_with_resolved_names_and_bound_device` (`:57-91`): assert `profile.Serial.Should().Match("STU-*")` and `raw.Should().Contain("\"serial\"")`.
- New `Register_mints_a_unique_serial_per_student` — register **two** students in the same tenant through the real handler → both serials non-empty, `STU-`-prefixed, and **distinct** (proves the handler's `NextUnique` seeding + the `(TenantId, Serial)` unique index).
- New `Backfilled_existing_students_have_a_serial` — a `SeedStudentAsync` student surfaces a non-empty `STU-`-prefixed serial on `/api/me/profile` (proves the seed + DTO path; the migration backfill SQL is implicitly exercised because the integ factory runs the real migrations at `SalahBahazadApiFactory.cs:60`).
- **Tenant isolation** — `Profile_is_isolated_to_the_callers_tenant` (`MeProfileApiTests.cs:293-317`) already covers cross-tenant; the index shape `(TenantId, Serial)` already encodes *per-tenant* uniqueness (two different tenants may legally hold the same serial string). No Serial-specific cross-tenant test required beyond confirming the index is per-tenant.
- **Regression / must stay green:** the existing profile shape assertions (no `email`/`avatar`/`tokenHash` leak, `MeProfileApiTests.cs:83-90`) and the full S0–S6 suite. Baseline: the one pre-existing `QuestionBank` image test.

## Done = ready for wiring
Contract §C satisfied — `serial` minted at `Register`, backfilled by the one migration, surfaced second on `GET /api/me/profile`, and guarded by a per-tenant `(TenantId, Serial)` unique index mirroring `CodeConfiguration`. The 5C gate, `app-exchange`, and `refresh` are untouched; no route was added or widened. Suite green minus the known `QuestionBank` baseline. **This is the only migration in the native-app slice — none follows.** Hand to `IMPLEMENTATION-PLAN-native-app-a1-wiring.md`, which proves the live `serial` reaches the player watermark on the Aspire stack (gate → redeem → key → play, view-decrement, IDOR/tenant `404`s) — **no drift vs the contract**.

---
## Kickoff prompt (paste into a fresh Claude session at the repo root)
```
You are implementing the BACKEND stream of Native App phase A1 (Student.Serial — the one migration — surfaced on /api/me/profile) for Salah Bahzad. Edit backend/** ONLY.

Read first, in order:
1. docs/contracts/native-app-playback.md (§C profile + serial — the frozen authority; §J ownership)
2. docs/IMPLEMENTATION-PLAN-native-app-a1-backend.md (this stream)
3. backend/CLAUDE.md (EF Core conventions, Multi-tenancy, Testing standards, Naming conventions)
4. The precedents to mirror: CodeSerialGenerator.cs, CodeBatch.Generate / Code.Mint, CodeConfiguration.cs (the (TenantId, Serial) unique index), 20260619143015_AddStudentPhoneNumber.cs, 20260621072358_Phase5C_VideoLengthSeconds.cs (the hand-inserted Sql backfill)

Build:
- NEW Domain/Common/StudentSerialGenerator.cs (Crockford alphabet, one group of 6 → "STU-XXXXXX", Next() + NextUnique(ISet<string>, maxAttempts=1000)). FLAG the 6-vs-5 length discrepancy vs the contract example before locking it.
- Student.cs: add Serial (private set), take it in Register (guard non-blank), leave it UNTOUCHED in Resubmit.
- RegisterStudentHandler.cs fresh-register branch only: seed existing serials (IgnoreQueryFilters + TenantId ==), NextUnique, pass into Register.
- StudentConfiguration.cs: Property(Serial).HasMaxLength(20).IsRequired() + HasIndex((TenantId, Serial)).IsUnique().
- StudentProfileDto.cs: add Serial as the 2nd record member (after Id) + in ToProfileDto. Loader + endpoint unchanged.
- ONE gated migration AddStudentSerial: hand-edit Up() to 3 steps (AddColumn defaultValue:"" → Sql backfill from Id hex → CreateIndex unique). Down() drops index then column. Commit the regenerated AppDbContextModelSnapshot.cs.
- Seed sites: SalahBahazadApiFactory.SeedStudentAsync + StudentTests.NewPending + AuthTestHelpers.NewStudent pass a serial.

Tests (dotnet test -c Release): the new StudentSerialGeneratorTests; the StudentTests serial cases (set/blank/Resubmit-stable); the integration cases — profile surfaces STU-* serial, two registrations mint distinct serials, a backfilled/seeded student surfaces a serial; existing profile + tenant-isolation tests stay green.

Green gate: `dotnet test -c Release` (known baseline: the one QuestionBank image test). Report the result.
```
