# Tasks: Encurtador de links

**Input**: Design documents from `specs/001-url-shortener/`

**Prerequisites**: `plan.md`, `spec.md`, `research.md`, `data-model.md`,
`contracts/links-api.md`, `quickstart.md`

**Tests**: Test tasks are included because the constitution requires unit coverage for
business rules and an integration happy path for every endpoint.

**Organization**: Tasks are grouped by user story. Every task has a sequential ID, an exact
file path, and a story label when it belongs to a user-story phase.

## Phase 1: Setup

**Purpose**: Create the solution and the projects using the stack and dependencies justified
in `research.md` R-14.

- [X] T001 Create `Encurtador.slnx`, `src/Encurtador.Api/Encurtador.Api.csproj`, and `tests/Encurtador.Tests/Encurtador.Tests.csproj` targeting .NET 10; add the API, EF Core SQLite/Design, xUnit, test SDK, and `Microsoft.AspNetCore.Mvc.Testing` dependencies, and omit `coverlet.collector` and `Microsoft.AspNetCore.OpenApi`.
- [X] T002 [P] Pin `dotnet-ef` 10.0.x in `.config/dotnet-tools.json`.
- [X] T003 [P] Configure `ConnectionStrings:Links` and `ShortLinks:PublicBaseUrl` in `src/Encurtador.Api/appsettings.json`.

---

## Phase 2: Foundational

**Purpose**: Establish persistence, shared service boundaries, HTTP error handling, and test
infrastructure required by every story.

- [X] T004 Create `src/Encurtador.Api/Domain/Link.cs` with `Id` "Gerado pelo banco. Uso interno; nunca é exposto."; `Code` "4 a 20 caracteres de A-Z1-9", automatic codes "7 caracteres do alfabeto sem I e O", "Imutável e nunca reutilizado"; `TargetUrl` "Absoluta, http/https, ≤ 2048 caracteres" and "Imutável"; `IsCustomCode`; `CreatedAtUtc` as "Instante da criação, via TimeProvider"; nullable UTC `ExpiresAtUtc` stored as the exclusive instant `data informada + 1 dia, 00:00:00Z`; `AccessCount` as a non-null `long`, "INTEGER, default 0" and "≥ 0"; and non-null `ManagementKeyHash` as `byte[] (32)`, SHA-256, unique, never plaintext.
- [X] T005 Configure `src/Encurtador.Api/Infrastructure/LinksDbContext.cs` with unique indexes on `Code` and `ManagementKeyHash` and the data-model field constraints, then generate the initial migration under `src/Encurtador.Api/Infrastructure/Migrations/`.
- [X] T006 Define `ILinkRepository` in `src/Encurtador.Api/Application/Abstractions/ILinkRepository.cs` and implement it in `src/Encurtador.Api/Infrastructure/LinkRepository.cs` with code lookup, insert results distinguishing code conflicts from management-key-hash conflicts, lookup by code, and an atomic access update filtered by link validity.
- [X] T007 Configure `src/Encurtador.Api/Program.cs` for controllers, dependency injection, SQLite WAL, startup migrations, and `ProblemDetails`; return pt-BR `ValidationProblemDetails` for invalid model state and malformed JSON, and do not enable HTTP logging.
- [X] T008 Create `tests/Encurtador.Tests/Integration/ApiFactory.cs` to start the real HTTP pipeline against a temporary file-backed SQLite database, with a way to reopen a host against the same file for restart tests.
- [X] T009 Create `tests/Encurtador.Tests/TestSupport/FakeTimeProvider.cs` to control UTC time in expiration tests without waiting for wall-clock time.

**Checkpoint**: Solution, database, HTTP host, and reusable integration-test support are
available before story implementation begins.

---

## Phase 3: User Story 1 - Encurtar uma URL e ser redirecionado (Priority: P1) - MVP

**Goal**: Create a link with an automatic code and one-time management key, redirect visitors,
count accesses, reject invalid URLs, and preserve data after restart.

**Independent Test**: Create an automatic link, verify its seven-character code and one-time
key, visit it and receive a non-cacheable redirect, then restart the host and verify the link,
destination, and access count persist.

### Tests for User Story 1

- [X] T010 [P] [US1] Add unit tests in `tests/Encurtador.Tests/Unit/LinkCodeRulesTests.cs` and `tests/Encurtador.Tests/Unit/CodeGeneratorTests.cs` for automatic-code length and alphabet, excluding `I` and `O` as decided in R-03.
- [X] T011 [P] [US1] Add unit tests in `tests/Encurtador.Tests/Unit/TargetUrlRulesTests.cs` for absolute HTTP/HTTPS URLs, the 2048-character maximum, malformed URLs, and rejection of other schemes without contacting the destination.
- [X] T012 [P] [US1] Add unit tests in `tests/Encurtador.Tests/Unit/ManagementKeyHasherTests.cs` for unpredictable key generation, at least 128 random bits, hash verification, and distinct keys.
- [X] T013 [P] [US1] Add unit tests in `tests/Encurtador.Tests/Unit/LinkServiceCreateTests.cs` proving creation succeeds on an available automatic code, refuses after exactly five generated-code collisions, regenerates the management key after a `ManagementKeyConflict` without consuming the code-collision budget, and allows repeated successful creations without applying a quota (FR-019, FR-020).
- [X] T014 [P] [US1] Add unit tests in `tests/Encurtador.Tests/Unit/LinkServiceAccessTests.cs` proving valid accesses delegate one atomic increment, nonexistent and expired links do not increment, and access operations do not change the stored destination.
- [X] T015 [P] [US1] Add integration tests in `tests/Encurtador.Tests/Integration/LinksControllerCreateTests.cs` for the POST happy path, 201 response, returned short URL/code/key, `Cache-Control: no-store`, invalid URL 400 `ValidationProblemDetails`, and absence of the readable key from database records and captured logs.
- [X] T016 [P] [US1] Add integration tests in `tests/Encurtador.Tests/Integration/RedirectControllerTests.cs` for a 302 redirect to the saved destination with `Cache-Control: no-store`, access counting, no readable management key in response headers or body, 400 `ValidationProblemDetails` for malformed codes, and 404 for a well-formed unknown code.
- [X] T017 [P] [US1] Add a restart integration test in `tests/Encurtador.Tests/Integration/LinkPersistenceTests.cs` that creates and visits a link, starts a new host over the same SQLite file, and verifies the redirect and persisted access count.

### Implementation for User Story 1

- [X] T018 [P] [US1] Implement `src/Encurtador.Api/Domain/LinkCodeRules.cs` and `src/Encurtador.Api/Domain/CodeGenerator.cs` for the automatic seven-character alphabet and cryptographically random generation.
- [X] T019 [P] [US1] Implement `src/Encurtador.Api/Domain/TargetUrlRules.cs` to enforce an absolute, well-formed HTTP/HTTPS URL of at most 2048 characters without making a network request.
- [X] T020 [P] [US1] Implement `src/Encurtador.Api/Domain/ManagementKeyHasher.cs` to generate a 32-byte Base64Url key, compute its SHA-256 hash, and verify hashes with a constant-time comparison; persist only the hash.
- [X] T021 [US1] Implement creation and redirect operations in `src/Encurtador.Api/Application/ILinkService.cs`, `src/Encurtador.Api/Application/LinkService.cs`, and `src/Encurtador.Api/Application/Results/LinkOperationResults.cs`; keep all business rules out of controllers, retry automatic-code collisions up to five total attempts, regenerate the key and retry on `ManagementKeyConflict` without consuming the code-collision budget, and never log or persist the readable management key.
- [X] T022 [P] [US1] Create `src/Encurtador.Api/Contracts/CreateLinkRequest.cs` and `src/Encurtador.Api/Contracts/CreateLinkResponse.cs`, and implement `POST /api/links` in `src/Encurtador.Api/Controllers/LinksController.cs` to return 201 with `shortUrl = ShortLinks:PublicBaseUrl/{code}` (falling back to the request scheme and host), `code`, and the one-time `managementKey`, plus pt-BR validation errors and 503 after five automatic-code collisions; do not impose a creation quota (FR-019).
- [X] T023 [P] [US1] Implement `GET /{code}` in `src/Encurtador.Api/Controllers/RedirectController.cs` to return 302 with the immutable destination and `Cache-Control: no-store`, 400 `ValidationProblemDetails` for malformed codes, or 404 for a well-formed unknown code.

**Checkpoint**: US1 can create, redirect, count, and retain links across an application
restart.

---

## Phase 4: User Story 2 - Acompanhar o total de acessos (Priority: P2)

**Goal**: Return only a link's access total to a caller with that link's management key, and
return the same unauthorized response for absent, wrong, and nonexistent-code cases.

**Independent Test**: Create a link, access it N times, retrieve exactly N with the matching
key, and verify that missing, empty, wrong, cross-link, and nonexistent-code credentials
receive the same 401 without revealing the total.

### Tests for User Story 2

- [X] T024 [P] [US2] Add unit tests in `tests/Encurtador.Tests/Unit/LinkServiceStatsTests.cs` for valid-key totals on active and expired links, wrong or empty keys, nonexistent codes, no counter increment, and stats results that expose only the total without reissuing the management key.
- [X] T025 [P] [US2] Add integration tests in `tests/Encurtador.Tests/Integration/LinksControllerStatsTests.cs` asserting the successful response body contains exactly one JSON property, `accessCount`, with the expected numeric total and no other link data or management key; also test 400 `ValidationProblemDetails` for malformed codes regardless of key and identical 401 `ProblemDetails` for missing, empty, wrong, cross-link, and well-formed nonexistent-code keys.
- [X] T026 [P] [US2] Add an integration test in `tests/Encurtador.Tests/Integration/AccessCountConcurrencyTests.cs` that performs 100 simultaneous redirects to one valid link and verifies via stats that the total increased by exactly 100.

### Implementation for User Story 2

- [X] T027 [US2] Implement the stats operation in `src/Encurtador.Api/Application/LinkService.cs` and `src/Encurtador.Api/Application/Results/LinkOperationResults.cs`; verify the supplied key without revealing whether the code exists, compare against a dummy hash for missing codes, allow stats for expired links, and return only the access count.
- [X] T028 [US2] Add `GET /api/links/{code}/stats` to `src/Encurtador.Api/Controllers/LinksController.cs`; call `LinkService` and translate its invalid-code result to 400 `ValidationProblemDetails` before translating well-formed-code authorization results, return 200 with only `accessCount` when valid, and otherwise return the same pt-BR 401 `ProblemDetails` with `WWW-Authenticate: ManagementKey` and `Cache-Control: no-store`. Code-format validation remains in `LinkService`.

**Checkpoint**: US2 exposes only the total to the holder of the corresponding key and
preserves exact concurrent access totals.

---

## Phase 5: User Story 3 - Escolher um código personalizado (Priority: P3)

**Goal**: Allow valid custom codes and reject invalid or previously used codes without
automatic retries.

**Independent Test**: Create a link with a valid custom code and verify it is used verbatim;
verify invalid format returns 400 and a repeated code returns 409.

### Tests for User Story 3

- [X] T029 [P] [US3] Add unit tests in `tests/Encurtador.Tests/Unit/LinkServiceCustomCodeTests.cs` for the full custom alphabet `A-Z1-9`, lengths 4–20, invalid zero/lowercase/space/special characters, and immediate conflict without retries.
- [X] T030 [P] [US3] Add integration tests in `tests/Encurtador.Tests/Integration/LinksControllerCustomCodeTests.cs` for custom-code creation, duplicate-code 409, invalid-code 400 `ValidationProblemDetails`, and two simultaneous creations with the same custom code producing exactly one 201, one 409, and one persisted link.

### Implementation for User Story 3

- [X] T031 [US3] Extend `src/Encurtador.Api/Contracts/CreateLinkRequest.cs`, `src/Encurtador.Api/Application/LinkService.cs`, and `src/Encurtador.Api/Controllers/LinksController.cs` to accept optional custom codes matching `^[A-Z1-9]{4,20}$`, preserve the supplied code exactly, and return 409 immediately when it is already reserved.

**Checkpoint**: US3 accepts only the specified custom-code format and preserves global
uniqueness, including for expired links.

---

## Phase 6: User Story 4 - Definir data de expiração (Priority: P4)

**Goal**: Accept an optional UTC calendar expiration date, keep the link valid through that
day, and stop redirects/counting at the following midnight.

**Independent Test**: Create a link expiring today, verify it redirects at the final instant
of that UTC day, and verify it returns 410 without incrementing at the next midnight.

### Tests for User Story 4

- [X] T032 [P] [US4] Add unit tests in `tests/Encurtador.Tests/Unit/ExpirationRulesTests.cs` for `yyyy-MM-dd` parsing, rejection of past UTC dates, acceptance of today, and the exclusive next-midnight expiration instant.
- [X] T033 [P] [US4] Add integration tests in `tests/Encurtador.Tests/Integration/LinkExpirationTests.cs` using `FakeTimeProvider` for valid redirect through the expiration date, 410 with no increment at the next midnight, past-date 400, statistics availability after expiration, and code non-reuse after expiration.

### Implementation for User Story 4

- [X] T034 [US4] Implement `src/Encurtador.Api/Domain/ExpirationRules.cs` to parse `yyyy-MM-dd`, compare dates in UTC, reject dates before today, and calculate the exclusive expiration instant at 00:00 UTC on the following day.
- [X] T035 [US4] Extend `src/Encurtador.Api/Contracts/CreateLinkRequest.cs`, `src/Encurtador.Api/Application/LinkService.cs`, and `src/Encurtador.Api/Controllers/LinksController.cs` to validate and persist optional `expiresAtUtc`, reject expired redirects with 410 without incrementing, and keep statistics available after expiration.

**Checkpoint**: US4 honors the entire UTC expiration day and never reuses an expired
link's code.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Align the run guide with the delivered contract and verify the complete feature.

- [X] T036 [P] Add `tests/Encurtador.Tests/Integration/LinkCreationPerformanceTests.cs` to measure POST creation with the host already started and a temporary SQLite database, asserting the response completes within 5 seconds (SC-001).
- [X] T037 [P] Add `tests/Encurtador.Tests/Integration/RedirectPerformanceTests.cs` to issue 100 sequential redirects with the host already started, not following destinations, and assert each response arrives within 1 second (SC-002).
- [X] T038 [P] Update `specs/001-url-shortener/quickstart.md` with the final endpoints, statuses, management-key header, temporary SQLite configuration, performance criteria, and runnable commands for the implemented scenarios.
- [X] T039 Run `dotnet tool restore`, `dotnet build Encurtador.slnx`, `dotnet test Encurtador.slnx`, and the scenarios in `specs/001-url-shortener/quickstart.md`; fix failures and confirm the Constitution Check and documented dependency justifications in `specs/001-url-shortener/plan.md`.

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies; T002 and T003 can run in parallel with T001.
- **Foundational (Phase 2)**: Depends on Setup; blocks every user story.
- **User Stories (Phases 3–6)**: Depend on the Foundational phase and are ordered by
  specification priority.
- **Polish (Phase 7)**: Depends on the stories selected for delivery.

### User Story Dependencies

- **US1 (P1)**: First independently deliverable increment after Foundational.
- **US2 (P2)**: Depends on US1's link creation and management-key issuance.
- **US3 (P3)**: Depends on US1's creation flow and repository uniqueness guarantee.
- **US4 (P4)**: Depends on US1's creation and redirect flow.
- US2–US4 are kept in priority order; they extend the shared `LinkService` and/or
  `LinksController`, so implementing them concurrently would create file conflicts.

### Parallel Opportunities

- Setup: T002 and T003 can run alongside T001 because they edit different files.
- US1: T010–T017 are separate test files; T018–T020 are separate domain files; T022 and
  T023 are separate controllers after T021 is complete.
- US2: T024–T026 use separate test files and can be developed in parallel; service and
  controller changes (T027–T028) remain sequential.
- US3: T029 and T030 are separate test files and can be developed in parallel.
- US4: T032 and T033 are separate test files and can be developed in parallel.
- Performance: T036 and T037 use separate integration-test files and can run independently
  after their respective endpoint implementations are available.

### Parallel Examples

```text
US1 tests: T010 + T011 + T012 + T013 + T014 + T015 + T016 + T017
US1 domain: T018 + T019 + T020
US1 endpoints: T022 + T023 (after T021)
US2 tests: T024 + T025 + T026
US3 tests: T029 + T030
US4 tests: T032 + T033
Performance tests: T036 + T037
```

## Implementation Strategy

### MVP First (User Story 1)

1. Complete Setup and Foundational phases.
2. Complete US1: automatic code, one-time key issuance, create, redirect, counting, and
   persistence.
3. Validate US1 independently with its unit and integration tests and confirm restart
   persistence.

### Incremental Delivery

1. Add US2 for protected statistics and concurrent-count verification.
2. Add US3 for custom codes and uniqueness conflicts.
3. Add US4 for UTC expiration.
4. Add and run SC-001/SC-002 integration measurements, then run quickstart and full solution
   validation after all selected stories.

### Independent Test Criteria by Story

- **US1**: Create and visit a link; verify seven-character automatic code, one-time key,
  302/no-store, destination, count, and persistence after host restart.
- **US2**: Access a link N times; correct key returns exactly N and only the total; all
  invalid or absent keys, including for nonexistent codes, return the same 401.
- **US3**: Valid custom code is preserved; invalid format returns 400; duplicate active or
  expired code returns 409.
- **US4**: Today and future dates are accepted; past dates return 400; link works through
  its UTC expiration date and returns 410 without counting after the boundary.

## Notes

- Every task uses `- [ ] Tnnn`, includes a story label only in a user-story phase, and names
  the exact implementation or test path.
- Tests are included because the constitution requires unit tests for business rules and a
  happy-path integration test for every endpoint.
- Tasks marked `[P]` use different files and have no prerequisite on another incomplete task.
