# Data Quality Audit (Aurora)

This project can accidentally create low-signal / wrong nodes (pronouns as people, cross-type duplicates like place+person, event nodes without real money signals). This audit dumps the key derived tables and produces a compact report so you can see what exists and what should be cleaned.

## Output location

The audit writes files under `.runlogs/data-quality/<timestamp>/` (ignored by git).

Files:

- `audit/report.md` (human-readable summary)
- `audit/stats.json` (counts + suspicious signal counters)
- `audit/*.json` (top suspicious lists)
- `export/*.jsonl` (table dumps; entry content excluded by default)

## Run

From repo root:

```powershell
./docs/operations/scripts/audit-data-quality.ps1
```

Target a single user:

```powershell
./docs/operations/scripts/audit-data-quality.ps1 -UserId "00000000-0000-0000-0000-000000000000"
```

Include full entry content in the export (sensitive):

```powershell
./docs/operations/scripts/audit-data-quality.ps1 -IncludeEntryContent
```

Skip exporting JSONL (faster, audit-only):

```powershell
./docs/operations/scripts/audit-data-quality.ps1 -NoExport
```

## What to look for

- `pronoun_person_nodes.json`: nodes like `lei/lui` stored as kind `person`
- `cross_kind_collisions.json`: same normalized name appears with multiple kinds (e.g. `place` + `person`)
- `duplicate_within_kind.json`: same normalized name appears multiple times in the same kind (failed canonicalization)
- `memory_events_missing_amounts.json`: events created without `EventTotal/MyShare` (usually extraction false positives)

## Remediation options (high level)

- Fix ingestion guards (pronoun filtering, stronger “financial event” gating).
- Run normalization/merge where safe (place/person collisions).
- Use the Feedback System templates for deterministic corrections:
  - block token, add alias, force-link, change type, merge entities.

