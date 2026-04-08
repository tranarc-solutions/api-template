# Tranarc API Template — Project Scaffold CLI

## Overview
.NET Global Tool with Spectre.Console for scaffolding new ASP.NET Core API projects.

## Install & Run
```bash
dotnet tool install -g tranarc-api-template
tranarc new          # interactive
tranarc new --name X --modules consumer,slack  # non-interactive
tranarc add notifications  # add module to existing project
```

## Naming Rules
- NO domain-specific references in generated code
- All names use {{project_name}}, {{company_name}} template variables
- Schema name defaults to {{project_name}}

## Tech Stack (for the CLI tool itself)
- Spectre.Console — interactive prompts
- Scriban — template rendering
- Spectre.Console.Cli — command parsing

## Always Included (Core)

### Projects
- {{Name}}.Host/ — API host (FastEndpoints)
- Core/ — business logic
- Shared/ — DTOs, contracts, enums
- {{Name}}.SourceGenerator/ — permissions generator
- Tests/{{Name}}.Tests.Unit/
- Tests/{{Name}}.Tests.Integration/ — TestContainers + PostgreSQL

### Infrastructure
- FastEndpoints with CustomEndpoint base classes
- EF Core + PostgreSQL (always)
- ErrorOr for error handling
- IScopedService auto-registration
- Cookie auth (MainApp + Backoffice dual scheme)
- Smart policy scheme (JWT/MainApp/Backoffice auto-select)
- Source-generated permissions
- Data Protection (Azure Key Vault + Blob Storage)
- Serilog (Console + optional Slack sink, Environment enrichment)
- CORS with env-specific overrides
- Health checks (DB + optional Service Bus)
- Azure Key Vault integration
- Rate limiting
- Request/Validator pattern
- Configuration folder pattern
- Docker multi-stage Dockerfiles
- CI/CD: GitHub Actions
- CLAUDE.md

## Optional Modules

### Consumer (MassTransit + Azure Service Bus)
- {{Name}}.Consumer/ project
- Queue prefix pattern
- Sample consumer
- Retry/redelivery policies

### Webhook
- {{Name}}.Webhook/ project
- HMAC security preprocessor
- Route prefix (api/webhooks)

### Hangfire
- HangfireJobBase base class
- Job registration pattern
- Dashboard with auth
- Sample recurring job

### WhatsApp
- WhatsAppService (template sending, sanitization, OTP buttons)
- Settings + template names config

### Payment Gateway
- IPaymentProvider abstraction
- ProviderResolver
- Wallet-to-wallet + bank transfer support

### Notifications
- AlertSendingService (SMS/Email/WhatsApp)
- Deduplication, quiet hours

### Slack
- SlackService (severity colors, env tags)
- Infrastructure vs business channels