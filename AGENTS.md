# Repository instructions

## SonarQube secrets scanning for files protocol

Before reading any file in this workspace, scan it with the deterministic scanner:

```bash
sonar analyze secrets <path/to/file>
```

If a file contains a secret, do not read it or continue the original request.
Inform the user that reading it would expose credentials in chat history, logs
and downstream telemetry. Advise rotation at the source of truth and removal
from the file before proceeding. Do not print the secret.

Compressed evidence must also be scanned after decompression and before semantic
inspection. Do not read `.secrets/rumble-url`, embedded recorder Git history or
private archives unless the specific task requires approved evidence inspection;
the archive's secret and Git members remain excluded even then.

## OBS MCP routing

After scanning it, follow `docs/obs-mcp.md` when using OBS tools. Use `obs_studio`
for direct OBS control, inspection and qualification, and `obs_workflows` for
persistent visibility presets and periodic visual monitoring. Keep TDSBLive and
Streamer.bot as the production automation authorities. Coordinate mutations
across servers; installation alone does not authorize broadcasting or changing
live output.

## Goal execution and publication

Follow `docs/implementation-plan.md`. Preserve goal IDs, honor prerequisites,
record current acceptance evidence, and never claim live/Windows behavior from
mock tests alone. Keep HTTP supported without a certificate requirement.

Use the pinned toolchains, tracked lockfiles, isolated tests and scoped commits.
Check the staged file list before pushing public history. Do not add original
references, credentials, user data, generated output, dependency trees or test
reports. Never bypass fork-secret isolation or protected-branch checks.
