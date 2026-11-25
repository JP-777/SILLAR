-- FASE A CANDIDATE ONLY: DO NOT APPLY TO POSTGRESQL.
-- Illustrative DDL, not a registered EF migration. Before FASE B, generate
-- the provider-specific migration and snapshot from the adopted model.
-- The action list mirrors AuditAction.All after the FASE A candidate edit.
BEGIN;
ALTER TABLE core.audit_log DROP CONSTRAINT ck_audit_log_action;
ALTER TABLE core.audit_log ADD CONSTRAINT ck_audit_log_action
CHECK (action IN (
  'create', 'update', 'delete', 'activate', 'deactivate', 'login',
  'login_failed', 'logout', 'setup', 'email_send', 'export'
));
COMMIT;
