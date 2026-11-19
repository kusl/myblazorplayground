ALTER TABLE work_item_event ADD COLUMN seq bigint GENERATED ALWAYS AS IDENTITY;
CREATE INDEX work_item_event_item_seq ON work_item_event (work_item_id, seq);
DROP INDEX work_item_event_item;

ALTER TABLE audit_event ADD COLUMN seq bigint GENERATED ALWAYS AS IDENTITY;
CREATE UNIQUE INDEX audit_event_seq ON audit_event (seq DESC);
DROP INDEX audit_event_occurred_at;
