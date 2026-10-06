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

## Change-aware CI for agents

Every PR commit is classified automatically using the protected base's policy and
the cumulative PR diff, not just its latest commit. Baseline build/tests, coverage,
Sonar and managed-host browser qualification always run. Desktop/lifecycle,
packaging, shipped-guide, dependency/build/CI/tooling changes and unknown paths
require full native Windows/Linux qualification. Main and manual runs are full.

To request more evidence for a risky or cross-cutting change, add a standalone
`CI: full` trailer to a commit message. It raises scope for the whole PR while that
commit remains in PR history; there is no skip or reduced-scope override. Preserve
the required co-author trailer. Do not use `[skip ci]`, `[ci skip]`, skipped-check
workarounds, coverage exclusions or workflow edits to lower the required scope.

After scanning `tools/ci_plan.py`, preview committed changes with
`python tools/ci_plan.py select --event pull_request --base origin/main --head HEAD`.
The workflow summary records the selected tier and reason. Local previews use the
working copy's policy; Actions uses protected-base policy. Routing-policy changes
therefore take effect after merge and their own PR receives full qualification.
See [testing](docs/testing.md#change-aware-pr-validation) for the complete contract.
