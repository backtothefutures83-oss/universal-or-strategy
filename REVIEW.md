# V12 Sovereign Review Instructions

## What IMPORTANT means here

Reserve Important ONLY for findings that would:

- Create ghost orders, double-fills, or missed stop-loss entries
- Cause false sharing / cache-line thrashing on ring buffer indices
- Skip a required Thread.MemoryBarrier() before hot-path memory reads
- Allow duplicate orders through the dedup map (race on FNV-1a probe)
- Introduce lock() inside src/ (BANNED by V12 Platinum Standard)
- Leak non-ASCII into a C# string literal (compiler gate violation)
- Expose GCP project IDs or API keys in workflow YAML

## What NIT means here

- Style, naming, formatting
- Missing XML doc comments
- Suboptimal LINQ (unless on hot path)
- Minor efficiency suggestions

## Do NOT report

- Anything already enforced by CI (dotnet-build.yml, check_ascii.py)
- Generated files under bench/, obj/, bin/
- Changes to docs/ or .agent/ unless they introduce a SEC-002 class leak

## Always check (V12 Critical Gates)

- src/: zero lock() statements (grep confirm)
- Any new struct with adjacent int/long fields: verify [StructLayout(LayoutKind.Explicit)] with 64-byte padding
- Any new Thread or Task: verify Thread.MemoryBarrier() placement before ring buffer read
- Workflow YAML: verify no project IDs, tokens, or credentials are hardcoded

## Verification bar

Behavior claims MUST include a file:line citation from the actual source.
Do NOT flag based on naming inference alone.

## Re-review convergence

After the first review, suppress new Nits. Post Important findings only.

## Summary shape

Open every review summary with: `[V12 Review] N Important, M Nit`
Lead with "No blocking issues" when N=0.
