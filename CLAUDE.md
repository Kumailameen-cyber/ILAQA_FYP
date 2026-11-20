# ILAQA — context for Claude Code

ILAQA is a Final Year Project (SZABIST Karachi): a GPS running game where runners claim city territory by walking/running closed loops around blocks in **Karachi only**. Team: Muneeb (lead on Feature 1), Kumail Ameen, Abdul Rehman. Teammates also push to `main` — always `git pull` before starting.

The full shared history lives in `docs/status-board.md` (local only, git-ignored). Read it before any task. The SRS, proposal and ERD are in the claude.ai project "ILAQA"; the rules below are the parts that matter for code.

## Repo layout
- `backend/ILAQA/` — ASP.NET Core 8 Web API, EF Core 8 + Npgsql + NetTopologySuite, Supabase PostgreSQL/PostGIS.
  - `Features/Auth|Runner|Runs/` — thin Controller + Service + Dtos per area (accepted layout).
  - `Services/` — `TerritoryDetectionService` (GPS cleaning, loops, rules), `TerritoryClaimService` (claims, takeovers, merges, shields, run completion), `TerritoryInactivityService` (hourly: vulnerable land, failed-run retry, 6 h abandoned runs), `GoogleRoadsService`, `JwtService`, etc.
  - `Models/`, `Data/ApplicationDbContext.cs`, `Migrations/`, `Program.cs`.
- `backend/ILAQA.Tests/` — xUnit: `TerritoryDetectionTests.cs` (main), `ApiContractTests.cs`, `JwtServiceTests.cs`, `GlobalExceptionHandlerTests.cs`.
- `mobile/` — Expo SDK 57 / React Native 0.86. NOT Expo Router (ignore that part of mobile/AGENTS.md): `App.tsx` switches tabs; screens in `screens/`, logic in `hooks/useRunTracking.ts` and `hooks/useMapTerritories.ts`, map in `components/RunMap.tsx`, API in `config/api.ts`, `services/api.ts`, `services/runSync.ts`, `services/http.ts`, `services/session.ts`, offline queue in `services/runStorage.ts` (SQLite), GPS in `services/backgroundLocationTask.ts`.
- `guest-website/` — Next.js guest site (demo data, not connected to the API yet).
- `.github/workflows/build-ilaqa-image.yml` — a push to `main` (backend/**, Dockerfile) builds and deploys to Azure Container App `ilaqa-api-test`.

## Commands
- Backend tests: `dotnet test backend` (all must pass; last known 185+).
- Mobile: `cd mobile && npx tsc --noEmit`; tests in `mobile/tests/` (see #12 below).
- Run the API locally on port 5246; the phone finds it via the Expo host IP (`config/api.ts`).

## Feature 1 rules (approved — don't change without Muneeb's explicit OK)
- GPS cleaning: accuracy ≤ 30 m, inside Karachi box (lat 24.70–25.50, lng 66.50–67.60), duplicates < 0.5 s dropped, jumps > 8 m/s dropped, never join across a pause (LegNo), break the route on a jump > 150 m or > 120 s silence with > 20 m movement (never invent a line).
- Loops: close within 20 m or by self-crossing; crossing routes are split into faces; each face ≥ 100 m² and ≥ 16 m wide. Out-and-back routes never become territory. Lasso counts only the circled block.
- Vehicle: 10 s average > 5.5 m/s for ≥ 3 s marks a ride; > 15 s total = ride; only loops that use the ride are rejected (VEHICLE_SPEED); ride distance never counts toward the 200 m shield renewal.
- LIVE CLAIMING: each GPS upload claims closed loops at once — free land; the free part next to shielded land; the covered part of a rival's vulnerable land. Own land is not re-claimed. One global advisory lock + run row lock + one transaction; first valid commit wins (BR-04). Same-owner merge and shield renewal happen at Finish only.
- Shield 3 days; renewed by a finished run with ≥ 200 m genuine distance (login never renews). Vulnerable after 3 days (hourly job). Server auto-finishes runs with no GPS for 6 h. No phone idle auto-finish.
- Map: every Active/Vulnerable territory visible to everyone at once; private owners show "Anonymous profile"; raw GPS and routes are owner-only (FR-21). Owner colour = SHA-256 → HSL.
- Google Roads snapping only for 15–30 m accuracy points; currently disabled in deployment (no Google billing). Keep the key in user-secrets / Azure env `GoogleRoads__ApiKey`; never log it or put it in mobile `.env`.
- Auth: 15-min JWT + refresh token in SecureStore, single active session per user (`ActiveSessionId`). Don't change.
- Anti-cheat beyond the vehicle rule (step counter, fake GPS, flags) is FYP-II — out of scope.

## Working rules
- Ask Muneeb before editing; one fix at a time; failing test first, then fix, then run tests, then show the diff.
- Don't commit, push, merge, deploy or run migrations without being asked; never write to production Supabase; give the commands instead.
- No new packages, tables or patterns without approval. Keep code simple and readable (it is defended in a viva).
- Never read or print `.env`, `env.local`, `appsettings.Development.json` or keys.
- Never commit: `.agents/`, `.claude/`, `skills-lock.json`, `mobile/env.local`, `backend/ILAQA/publish-linux/`, `Claude outputs/`, `docs/`. Stage files by name (no "Commit All").
- Muneeb edits in Visual Studio / VS Code: ask him to close a file before you edit it.

## Next tasks (in order; details in docs/status-board.md)
1. Confirm the 10–11 Nov deploy: GitHub Actions green, Azure updated, 3 read-only Supabase checks (claim counts, no duplicate claims, no overlapping land). Then a two-phone walk test of Feature 1.
2. Mobile A5: when an upload returns new land, draw it solid with a short fill animation + toast "Territory claimed · +N m²" (no new packages).
3. Mobile #29: dark Google map style (`mobile/theme/mapStyle.ts` + `RunMap.tsx` customMapStyle, dark UI, buildings/indoors/toolbar off). Do together with A5 (both touch RunMap). Colours: base #121615, blocks #171C1A, streets #262D2A, main roads #3A433F, highways #4A5550, parks #123020, water #0B1A21, labels #8F9891 / #6B7570.
4. Backend #30: per-account login limit 10/min (keep per-IP) + 429 Retry-After. Backend #31: HSTS in production only.
5. #6 territory holes: optional `holes` field in territory responses + `holes` prop on the map Polygon.
6. #12 dead code: delete `mobile/services/territoryDetection.ts` and `mobile/tests/territoryDetection.test.ts` (server does detection), and any backend test contradicting the crossing rule.
7. Part B (design only, needs approval + migration): "loop must go around the block on roads, not inside a ground" using a one-time OpenStreetMap Karachi road import into PostGIS.
8. Then: Volt Night restyle (JSX/styles only), dashboard/leaderboards (FR-22/23), guest website connected to the public territory API (FR-24).
