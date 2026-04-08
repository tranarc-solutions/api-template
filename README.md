# Tranarc API Template

.NET Global Tool for scaffolding production-ready ASP.NET Core API projects.

## Install

```bash
dotnet tool install -g tranarc-api-template
```

## Usage

### Create a new project (interactive)
```bash
tranarc new
```

### Create a new project (non-interactive)
```bash
tranarc new --name MyApp --company Acme --modules consumer,webhook,hangfire
```

### Add a module to an existing project
```bash
tranarc add consumer
tranarc add hangfire --path ./MyApp
```

## What you get

A complete ASP.NET Core API project with:

- **FastEndpoints** — endpoint-based routing with `CustomEndpoint<TReq, TRes>` base classes
- **EF Core + PostgreSQL** — repository pattern, auditable entities, soft delete
- **Auth** — Cookie (MainApp + Backoffice) + JWT (mobile), source-generated permissions
- **Serilog** — structured logging with console + optional Slack sink
- **Security** — Data Protection (Azure Key Vault), CORS, rate limiting, security headers
- **Health checks** — liveness + readiness probes
- **Docker** — multi-stage Dockerfiles for each service
- **CI/CD** — GitHub Actions workflow
- **Tests** — xUnit + NSubstitute + FluentAssertions + TestContainers

## Optional Modules

| Module | What it adds |
|---|---|
| `consumer` | MassTransit + Azure Service Bus / RabbitMQ consumer project |
| `webhook` | FastEndpoints webhook project with HMAC security |
| `hangfire` | Background job infrastructure with PostgreSQL storage |
| `slack` | SlackService with severity routing |
| `notifications` | AlertSendingService with deduplication and quiet hours |
| `whatsapp` | WhatsApp Cloud API service |
| `payment-gateway` | IPaymentProvider abstraction with ProviderResolver |

## Generated Project Structure

```
MyApp/
  MyApp.Host/          — API host
  Core/                — Business logic, auth, persistence
  Shared/              — DTOs, contracts, enums
  MyApp.SourceGenerator/ — Permissions source generator
  Tests/
    MyApp.Tests.Unit/
    MyApp.Tests.Integration/
  Configurations/      — Environment-specific JSON configs
  MyApp.Consumer/      — (if consumer module selected)
  MyApp.Webhook/       — (if webhook module selected)
```

## Tech Stack

- .NET 10
- FastEndpoints 8.1
- EF Core 10 + Npgsql
- Serilog
- xUnit + NSubstitute + FluentAssertions
- TestContainers (PostgreSQL)
- Spectre.Console (CLI)
- Scriban (templates)
