-- Removes deprecated entry-level feedback policies.
-- Keep this script idempotent and safe to re-run.

DELETE FROM "PersonalPolicies"
WHERE "PolicyKey" IN ('entity_kind_override', 'entity_feedback_note');
