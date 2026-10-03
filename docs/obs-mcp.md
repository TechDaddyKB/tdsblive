# OBS MCP roles for TDSBLive

The project uses two complementary OBS MCP servers as development and
qualification tools. They are not bundled with TDSBLive or required by its users.
Streamer.bot remains the authority for platform integration and action execution;
TDSBLive owns its event persistence, overlays, rules and durable execution records.

## Choose a server

| Work | Preferred server | Tools and reason |
|---|---|---|
| Inspect OBS connectivity, versions and output state | `obs_studio` (Tom-R-Main/OBS-MCP) | `obs-get-status`, `obs-get-version`, `obs-get-stream-status`, `obs-get-record-status`; primary diagnostic baseline |
| Set up TDSBLive browser sources and inspect rendering | `obs_studio` | `obs-create-input`, `obs-set-input-settings`, scene-item transforms, `obs-get-source-screenshot`; keep local HTTP overlay URLs supported |
| Inspect or change audio, filters, media, transitions and canvases | `obs_studio` | Explicit protocol tools, audio tracks/monitoring, media controls and canvas discovery; use only capabilities advertised by the connected OBS version |
| Plan scene changes and retain an undo point | `obs_studio` | `obs-apply-scene` plans before applying; `obs-snapshot` and `obs-restore` cover settings, audio and scene-item state |
| Qualify an explicitly requested local recording | `obs_studio` | `obs-preflight`, `obs-record-clip`, `obs-take-*`, `obs-contact-sheet`, `obs-trim-take`; supports watched takes and file inspection |
| Execute a protocol operation without a dedicated wrapper | `obs_studio` | Describe the request with `obs-describe-request` first; prefer explicit tools over `obs-call-request` or `obs-batch` |
| Save and reuse named visibility arrangements | `obs_workflows` (ironystock/agentic-obs) | `save_scene_preset`, `list_scene_presets`, `get_preset_details`, `apply_scene_preset`, `rename_scene_preset`, `delete_scene_preset`; persistent SQLite presets |
| Monitor overlay changes across time | `obs_workflows` | `create_screenshot_source`, `configure_screenshot_cadence`, `list_screenshot_sources`, `remove_screenshot_source`; periodic captures, image resources and local dashboard |
| Discover agentic workflows and tool groups | `obs_workflows` | `help`, `get_tool_config`, `list_tool_groups`, `set_tool_config`; workflow prompts and resource discovery supplement the selected tools |
| Product rules, financial/event processing and Streamer.bot actions | TDSBLive / existing `streamerbot` MCP | Preserve the implementation plan's authority boundaries and simulation isolation |

Upstream references: [OBS-MCP](https://github.com/Tom-R-Main/OBS-MCP) and
[agentic-obs](https://github.com/ironystock/agentic-obs).

The two servers overlap on scenes, sources, audio, filters, transitions, outputs,
studio mode and hotkeys. Route those operations through `obs_studio` by default.
`obs_workflows` exposes just 15 selected tools, including its status and discovery
tools, to avoid loading a second full control inventory. Its Core, Layout and
Visual groups register 39 tools internally; Codex's allowlist exposes only 15.
Sources, Audio, Design, Filters, Transitions and Automation are disabled in the
server's persisted project configuration.

Agentic-obs also has nine event/schedule automation tools and convenient typed
source creation helpers. These are available in the installed code but are not
enabled for ordinary project work. If a task specifically needs a unique helper,
enable its group with `set_tool_config`, add only the needed names to the project
`enabled_tools` list, and reconnect. Enabling a backend group alone does not
override Codex's allowlist. Do not create a second production rule engine that
duplicates TDSBLive or Streamer.bot actions.

## Use with the implementation plan

- G05/G06/G08: configure owned browser sources; inspect transparency, layout,
  scene-item size and donor/alert updates in OBS. Use periodic monitoring only
  for the duration needed, then remove the capture source.
- G09: inspect OBS audio routing and capture an explicitly requested local test
  take. A screenshot, audio setting or successful API response alone does not
  establish audible output.
- G10/G13: collect OBS version, output statistics and visual/media evidence
  alongside the application restart/reconnect/restore checks. Distinguish this
  Linux OBS instance from Wine application evidence and native Windows evidence.

Use one server to change a given source or scene at a time, then inspect the
result. OBS-MCP control leases do not coordinate changes from agentic-obs or
Streamer.bot. Do not assume a lease protects the entire tool stack.

Scene presets restore visibility, not a complete scene collection. OBS-MCP
snapshots also have limits: they cannot recreate deleted inputs/items or remove
items added later. Keep the project's normal backup/restore acceptance checks.

Installation authorizes tool setup and read-only connection verification.
Starting/stopping a broadcast or output, switching a live program scene, and
changing production automation require authorization within the actual task.
OBS-MCP's live-action confirmation setting is enabled, but it is not a blanket
guard for every state-changing tool. Prefer isolated owned examples for tests.
Never expose stream-service credentials or private source settings in reports.

## Local installation and startup

The machine-specific configuration is `.codex/config.toml`, locally excluded from
Git. It preserves the existing `streamerbot` server and adds `obs_studio` and
`obs_workflows`. Codex loads project MCP configuration for trusted projects;
start a new project session to load these tools. See
[official Codex MCP configuration](https://learn.chatgpt.com/docs/extend/mcp?surface=cli).

Local installation details and maintenance notes are in
`../.codex/obs-mcp-setup.md`. Checkouts, dependencies, executables, database,
captures and snapshots remain outside the public repository.

OBS must be running with WebSocket v5 enabled at `ws://127.0.0.1:4455`.
OBS-MCP can discover tools while OBS is closed and reconnect in the background.
Agentic-obs requires OBS during startup; reconnect its MCP server after starting
OBS if startup failed. Neither installer changes OBS's WebSocket configuration.

Both servers obtain the local OBS credential without printing it or embedding it
in project settings. Environment-provided credentials take precedence. The
agentic launcher uses the same OBS-MCP local credential resolver; its local
adaptation prevents copying that credential into SQLite.

Agentic-obs serves its dashboard and screenshot HTTP endpoints at
`http://127.0.0.1:17477` while its MCP process is running. This is separate from
the TDSBLive host at port 17474. HTTP works without certificates.

## Installation evidence — 2026-10-02

- OBS-MCP 1.1.1, commit `fb6d12234a74de10e53afee7fdc1208e1c01fb8d`:
  locked install using Node 24.21.0/npm 11.19.0; build, test typecheck, 236 unit
  tests, 14 end-to-end tests and manifest validation passed. Runtime dependency
  audit reported zero vulnerabilities. The upstream development-only packaging
  dependencies have three audit findings (one moderate and two high); no MCPB
  package was built or installed.
- Agentic-obs commit `89fcf6c68675229e01c7e702e3234cf0cf53ebe7`, built with
  Go 1.26.2 and the upstream `go.mod`/`go.sum` in readonly module mode:
  all upstream Go test packages passed after the local adaptations.
- An MCP SDK client launched each server with the exact project command,
  arguments, working directory and environment. All configured tool names were
  present. OBS-MCP exposed 171 tools; agentic-obs exposed 15 selected tools.
- Both read real OBS 32.2.2 / WebSocket 5.7.4 state. Streaming and recording
  were inactive. Agentic preset and screenshot inventories were empty;
  Automation was disabled. Dashboard and health endpoints returned HTTP 200.
- Verification clients closed and their servers shut down afterward. No scene,
  source, audio, recording, streaming or automation action was performed.
- Agentic's SQLite credential field is empty and its database permissions are
  0600. An isolated startup against an unavailable OBS port exited within the
  timeout without prompts or non-protocol stdout. A subsequently observed
  agentic process was owned by Codex; no independent background service was added.

This evidence establishes installation and live read-only connectivity. It does
not close rendering, audible output, recording, recovery or Windows acceptance
gates in `docs/implementation-plan.md`.
