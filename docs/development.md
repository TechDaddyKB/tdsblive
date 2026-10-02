# Development

Normal users should follow the [user guide](user-guide/Home.md). Developers should start with the exact pinned toolchains and build/test commands in [README](../README.md#development), then [architecture](architecture.md), [testing](testing.md) and [contributing](../CONTRIBUTING.md).

Before reading a workspace file, run `sonar analyze secrets <path>` as required by [AGENTS.md](../AGENTS.md). Stop if a secret is reported. Original references, raw captures, credentials, local MCP settings, application data, dependencies, builds and test reports stay out of public Git. Use reviewed sanitized fixtures and disposable data; replay/test events must not run production actions or change ordinary financial totals by default.

Build frontend assets before building the host. The editor and overlay runtime are separate bundles. The host's public OpenAPI snapshot and generated TypeScript contracts must remain synchronized; regeneration instructions are in [foundation API](foundation-api.md). New migrations and protocol/API compatibility behavior belong in their owning goal's documentation and tests.

Windows builds, ZIP/installer packaging and native package checks run in GitHub Actions. Backend tests emit OpenCover/TRX; frontend tests emit LCOV. SonarQube imports both coverage reports and enforces the new-code gate. Fork code never receives trusted secrets; do not bypass protected checks.

For a bounded task, invoke a stable goal from [the implementation plan](implementation-plan.md), check its actual prerequisites and record acceptance evidence. Compiling or passing mock tests does not establish live integration, audible output or Windows qualification. Current MVP release evidence is in [G10 qualification](g10-qualification.md); G11–G13 remain separate later milestones.
