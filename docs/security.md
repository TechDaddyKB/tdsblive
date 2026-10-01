# Security and local HTTP

**HTTPS is not required.** Default host access is loopback HTTP on port 17474.
The scaffold exposes only product/status metadata. It has no credential setup,
ledger or asset upload routes yet. G02 adds privileged endpoint protection;
optional LAN operation must be explicit, authenticated and origin/CSRF protected.
HTTP on LAN does not encrypt traffic; optional HTTPS may be used without becoming
a core requirement. Never expose a privileged editor through an unauthenticated
LAN binding.

Store integration credentials with DPAPI on Windows. Redact Rumble URLs/query
credentials, stream keys, integration passwords and tokens before persistence,
display or diagnostics. Widgets never receive credentials. Sandbox custom widgets
and mediate permissions in G12. Validate files/package traversal and size limits.

## Repository and CI safeguards

The original reference directory, credentials, raw captures/databases, logs,
backups, local overrides, dependencies and generated output are ignored. Curated
sanitized/synthetic fixtures and lockfiles stay tracked. Scanner success does not
replace staged publication review or private-string audits.

Windows CI uses read-only repository permissions and checkout without persisted
credentials. Sonar credentials exist only in the two trusted scanner steps,
never in test/report artifacts or source files. Fork/Dependabot code receives no
Sonar token. Required Sonar checks must pass on reviewed trusted branches before
merge; `pull_request_target` is intentionally absent.

The Sonar analysis token is stored in the repository's `SONAR_TOKEN` Actions
secret. The current token expires at **2026-12-30 00:00 UTC** (December 29, 2026,
6:00 p.m. America/Chicago). Rotate it before expiry in SonarQube Cloud account
security and update the GitHub secret without printing or committing the value.
`SONAR_TOKEN_EXPIRES` is non-secret maintenance metadata. Do not revoke unrelated
tokens or read token-bearing local files while diagnosing authentication.

GitHub secret scanning/push protection and Dependabot security updates are
enabled where available. Dependency/action updates remain reviewed and locked.

Security reports must omit credential values and private capture content. Rotate
an exposed credential at its provider rather than relying on Git history cleanup.
