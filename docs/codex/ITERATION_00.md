# ITERATION 00 — Solution Skeleton

## Context

Repository currently contains:
- README.md
- SPEC.md
- .gitignore

We are building a minimal production-ready Telegram bot skeleton.

Target:
- .NET 10
- Two projects:
    - BotTemplate.Api
    - BotTemplate.Core

No business logic yet.
No Telegram integration yet.
Only structure and infrastructure skeleton.

---

## Task

1. Create solution file in root:
   - BotTemplate.sln

2. Create folder structure:

/src
  /BotTemplate.Api
  /BotTemplate.Core

3. BotTemplate.Api:
   - ASP.NET Core Minimal API
   - Target framework: net10.0
   - Basic Program.cs
   - Add health endpoints:
        GET /health/live
        GET /health/ready
   - Configure dependency injection container (empty for now)

4. BotTemplate.Core:
   - Class library
   - No external dependencies yet
   - Create folder:
        /Jobs
   - Create placeholder class:
        Job.cs

5. Add project reference:
   BotTemplate.Api -> BotTemplate.Core

6. Add Dockerfile in root:
   - Multi-stage build
   - Use:
        mcr.microsoft.com/dotnet/sdk:10.0
        mcr.microsoft.com/dotnet/aspnet:10.0
   - Expose port 8080
   - Set ASPNETCORE_URLS=http://+:8080

7. Add fly.toml:
   - app name: telegram-bot-starter-dotnet
   - internal_port = 8080
   - minimal config

---

## Constraints

- Do NOT add Telegram.Bot yet.
- Do NOT add EF Core.
- Do NOT add OpenTelemetry.
- Do NOT add background workers.
- Do NOT add logging libraries.
- No extra abstractions.

Keep it minimal and clean.

---

## Acceptance Criteria

- dotnet build succeeds
- dotnet run exposes /health/live
- Docker build succeeds
- No warnings