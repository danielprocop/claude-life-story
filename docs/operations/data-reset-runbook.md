# Data Reset Runbook

Date: 2026-02-28

## Purpose

Reset all application data and restart from a clean state without dropping schema.

## Scope

- PostgreSQL tables in schema `public`
- all users and derived memory data
- preserves table structure and indexes

## Recommended (user-scoped) reset

If you only need to restart your own dataset (common during algorithm tuning), prefer the user-scoped reset:

- Dashboard button: **Operazioni memoria -> Reset miei dati**
- API: `POST /api/operations/reset/me`

This wipes all entries + derived memory for the authenticated user, without truncating the whole database.

## Command used

```powershell
$sql = @'
DO $$
DECLARE stmt text;
BEGIN
  SELECT 'TRUNCATE TABLE ' || string_agg(format('public.%I', tablename), ', ') || ' RESTART IDENTITY CASCADE'
  INTO stmt
  FROM pg_tables
  WHERE schemaname='public';
  EXECUTE stmt;
END
$$;
'@

docker run --rm postgres:16-alpine psql "host=<RDS_HOST> port=5432 dbname=<DB_NAME> user=<DB_USER> password=<DB_PASSWORD> sslmode=require" -c "$sql"
```

## Post-check

Verify key tables are zero:

```sql
select 'Users' as table_name, count(*) as total from "Users"
union all select 'Entries', count(*) from "Entries"
union all select 'CanonicalEntities', count(*) from "CanonicalEntities"
union all select 'Settlements', count(*) from "Settlements"
union all select 'MemoryEvents', count(*) from "MemoryEvents";
```

## Notes

- In Cognito mode, users are recreated on first authenticated request.
- OpenSearch indexes are not yet wired in production code, so no index cleanup is required in this phase.
