# ILAQA
GPS running game: claim territory by running closed loops.
Folders: backend/ (ASP.NET Core 8), mobile/ (Expo React Native, TypeScript), web/ (Next.js, later), docs/ (proposal).
Database: PostgreSQL + PostGIS via EF Core.

## Rules
- Backend is one project with simple layers: Controller -> Service -> Data. No Onion/Clean architecture.
- Controllers are thin. Logic lives in Services.
- Follow security section 7.3 of docs/ILAQA_FYP_Project_Proposal.md.
- Keep code short and clean. No dead code, no extra features.
- Do ONLY what I ask. If something is outside the current task, ask first.

## Current focus (weeks 1-3)
Base app, login/signup, database, authentication and authorization. Nothing else.