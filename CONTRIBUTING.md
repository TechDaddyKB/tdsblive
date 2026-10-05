# Contributing

Choose a goal from [the implementation plan](docs/implementation-plan.md), confirm
its prerequisites with current evidence, and keep work within that goal. For
Codex, use: `Complete goal G02 from docs/implementation-plan.md, including any
incomplete prerequisites.` Preserve goal IDs and record acceptance evidence.

Read [AGENTS.md](AGENTS.md) and scan each file before reading it. Never upload
original reference archives, credentials, private captures, logs or runtime data.
Use synthetic or reviewed sanitized fixtures with explicit provenance.

Use the pinned SDK/Node/npm tools and committed dependency lockfiles. Run the
README checks before submitting a pull request. Treat Windows CI and SonarQube's
quality gate as merge requirements; do not use coverage exclusions to hide
handwritten production logic. New-code coverage must be at least 80%.

Fork and Dependabot pull requests receive no Sonar credentials. The mandatory
secrets scan fails closed before source execution when those credentials are
unavailable. A maintainer must inspect the changes and promote them to a trusted
repository branch for scanning, tests and analysis. Never switch to
`pull_request_target` to run untrusted changes with secrets. No merge should bypass
the protected branch's required quality check.

Dependency updates use weekly Dependabot PRs. Update exact versions, lockfiles,
tool/action pins and documentation together, then validate Windows CI. Public
bug reports should use sanitized reproductions without private messages or keys.
