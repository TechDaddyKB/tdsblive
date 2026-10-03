# Security and local HTTP

**HTTPS is not required.** Default host access is loopback HTTP on port 17474.
The foundation exposes editor, configuration, diagnostics and credential setup
routes with host/origin validation and CSRF protection. Optional LAN operation is
explicit and requires a Windows DPAPI admin credential before startup.
HTTP on LAN does not encrypt traffic; optional HTTPS may be used without becoming
a core requirement. Never expose a privileged editor through an unauthenticated
LAN binding.

Antiforgery uses ASP.NET Data Protection's supported managed AES-256-CBC and
HMAC-SHA256 implementation. This retains authenticated encryption while avoiding
the default Windows CNG SP800-108 provider, which fails in the qualified Wine
environment. Key storage and Windows DPAPI credential protection remain enabled;
CSRF checks are not disabled. Changing the encryptor affects newly generated
keys, not existing keys. See [Microsoft's managed algorithm configuration](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/configuration/overview?view=aspnetcore-10.0#specifying-custom-managed-algorithms).

Sonar rules S5332 (HTTP listener) and S2092 (non-Secure session cookie) are accepted
for these specific foundation locations with documented rationale: HTTPS cannot
be mandatory under the approved requirement, and Secure cookies cannot support
required HTTP session authentication. This accepts the unencrypted transport
tradeoff; it does not claim HTTP confidentiality. Other security findings remain
subject to review and remediation, and no broad rule exclusion is configured.

CodeQL's two matching HTTP-cookie alerts (session and antiforgery) use the same
specific transport acceptance. Its two `cs/user-controlled-bypass` alerts in
RequestSecurity were reviewed as false positives: oversized Content-Length and
cross-site Sec-Fetch-Site cause error responses and immediate returns, denying
the whole request. They do not grant access. Smaller/absent length and other/absent
fetch headers continue through mandatory remote-peer authentication, origin
validation and write CSRF checks. Kestrel independently limits actual body reads.
The sensitive authentication call is bypassed only when the request is rejected.
Tests verify denial and actual non-loopback authentication without relying on
these client headers. Review rationale is recorded on GitHub alerts 1, 2, 3 and 5.

Admin login has a bounded session count and rate limit; rotation invalidates
sessions atomically. Store integration credentials with DPAPI on Windows. Redact Rumble URLs/query
credentials, stream keys, integration passwords and tokens before persistence,
display or diagnostics. Widgets never receive credentials. Sandbox custom widgets
and mediate permissions in G12. Validate files/package traversal and size limits.

## Repository and CI safeguards

The original reference directory, credentials, raw captures/databases, logs,
backups, local overrides, dependencies and generated output are ignored. Curated
sanitized/synthetic fixtures and lockfiles stay tracked. Scanner success does not
replace staged publication review or private-string audits.

Windows CI uses read-only repository permissions and checkout without persisted
credentials. Sonar credentials exist only in trusted analysis and deterministic secrets-scan steps,
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


G05 preserves complete public GIF URLs only inside explicitly typed GIF metadata.
The exception permits recognized Giphy media hosts, `/media/` GIF paths and the
public `cid`, `ep`, `rid`, `ct` query keys. It rejects known vault values, credential
assignments, userinfo, fragments, other hosts/endpoints and unknown query keys.
Unstructured/query URLs still receive the existing redaction. This is required
for Twitch's supplied GIF URLs; it does not allow credentials or establish support
for an unobserved CDN format. Regression tests cover persistence and socket delivery.

## Custom widget boundary

G12 custom JavaScript executes in a dedicated worker with a virtual DOM inside an opaque `allow-scripts` iframe. A constrained trusted renderer prevents code, navigation, nested frames and event-handler attributes from entering the browser DOM. Worker CSP denies networking by default and permits only explicitly granted exact HTTPS domains; browser scripts cannot navigate the worker. Frame identity, opaque origin and a fresh capability bind bounded storage messages to a widget. Tokens remain in parent headers and never enter custom source/session/config. Financial, chat, redacted raw data, storage, media/audio and network capabilities require separate grants. Imported packages revoke those grants. See [custom widget security and package limits](g12-custom-widgets.md).

G12 security review (2026-10-03): frame-to-parent messages target the exact
application origin; the frame checks the sender's origin, WindowProxy and fresh
capability. Parent-to-frame messages must use `*` because sandboxed frames have
opaque origins. These four `S2819` findings are individually accepted: sends
target only the owned iframe's WindowProxy, carry its fresh capability, and
contain granted events/config/session or scoped store results, never access
tokens. The trusted frame cannot navigate through custom worker code or inert
rendered markup; a replacement frame receives a new capability. Real-browser
tests verify parent/navigation denial and spoof rejection; unit tests reject
sibling frames, incorrect origins and channels. This is a narrow reviewed
protocol requirement, with no rule exclusion. C# package/version regexes use
non-backtracking matching and a 100 ms timeout. Custom worker programs are
browser-compiled blob scripts; production `eval`/`Function` and `unsafe-eval`
are absent.

G13's local compatibility adapter receives only the existing mediated SBX
capabilities. It cannot grant access to cloud services, account-wide stores,
production action queues or parent tokens. Initial and updated worker sessions
are restricted to connected/preview/muted booleans; arbitrary parent session
properties are dropped. Unsupported-call warnings use fixed public API names
and never echo arbitrary property names. The reviewed opaque-frame wildcard
sends retain the same WindowProxy/capability boundary.

Private inspector fixtures and logs are credential-redacted, not public-safe.
Shape exports remove all scalar values and replace unknown field names, with
depth/array bounds. Aggregate diagnostic export uses an explicit allowlist of
application metadata and event/outbox counts; it never includes configuration,
logs or user records. Developer file utilities require deterministic scanning
before parsing, including gzip expanded bytes. Scanning failures fail closed;
error output never includes source values, paths, scanner text or parser text.
See [G13 sharing and utility limits](g13-compatibility.md).
