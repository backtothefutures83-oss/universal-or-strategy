# Security Policy

## Reporting a Vulnerability

Report vulnerabilities to malhitti@gmail.com.
Initial triage response target: within 48 hours.
Do not open a public issue for unpatched security findings.

## Supported Versions

Supported versions for security response:

- `main`
- `1101E.H-STABILITY-CERTIFIED`
- `v12.966-atomic-unification`

## Policy

The `main` branch should enforce these protections:

- Require pull requests before merge.
- Require CODEOWNERS review.
- Require status checks: `.NET Desktop Build`, `SonarCloud`, `dotnet-test`, `gitleaks`, `CodeQL`.
- Dismiss stale reviews on push.
- Require linear history.

## Zero-Trust Reminders

- Keep IPC binds on loopback only.
- Keep command lengths bounded.
- Decode inbound text as strict UTF-8.
- Do not commit secrets, tokens, DSNs, or credentials.

## Known Historical Exposures

The file `docs/brain/memory/adr019_compaction_state.md` previously committed a Sentry DSN.
The current working tree redacts that value and points operators to the `V12_SENTRY_DSN` environment variable.
Director-side rotation is still required in the Sentry UI for the `v12-sovereign-agents` project because the historical DSN is burned.
Any git-history purge using tools such as `git-filter-repo` is a separate Director-only decision and is outside Level-5 readiness scope for Task 3.
