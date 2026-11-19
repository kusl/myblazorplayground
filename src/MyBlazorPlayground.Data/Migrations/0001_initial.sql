CREATE TABLE person (
    id uuid PRIMARY KEY,
    user_name text NOT NULL,
    display_name text NOT NULL,
    password_hash text NOT NULL,
    security_stamp uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    last_sign_in_at timestamptz NULL,
    disabled_at timestamptz NULL,
    CONSTRAINT person_user_name_lower CHECK (user_name = lower(user_name))
);

CREATE UNIQUE INDEX person_user_name_key ON person (user_name);

CREATE TABLE site_role (
    person_id uuid NOT NULL REFERENCES person (id) ON DELETE CASCADE,
    role text NOT NULL CHECK (role IN ('administrator')),
    granted_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    granted_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (person_id, role)
);

CREATE TABLE site_setting (
    key text PRIMARY KEY,
    value text NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE audit_event (
    id uuid PRIMARY KEY,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    actor_id uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    actor_name text NULL,
    action text NOT NULL,
    subject_type text NOT NULL,
    subject_id text NOT NULL,
    summary text NOT NULL DEFAULT '',
    client text NULL
);

CREATE INDEX audit_event_occurred_at ON audit_event (occurred_at DESC);
CREATE INDEX audit_event_subject ON audit_event (subject_type, subject_id);

CREATE TABLE data_protection_key (
    friendly_name text PRIMARY KEY,
    xml text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE page_editor (
    page_key text NOT NULL,
    person_id uuid NOT NULL REFERENCES person (id) ON DELETE CASCADE,
    granted_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    granted_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (page_key, person_id)
);

CREATE INDEX page_editor_person ON page_editor (person_id);

CREATE TABLE project (
    id uuid PRIMARY KEY,
    key text NOT NULL,
    name text NOT NULL,
    description text NOT NULL DEFAULT '',
    next_item_number integer NOT NULL DEFAULT 1,
    created_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz NULL,
    deleted_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    CONSTRAINT project_key_format CHECK (key ~ '^[A-Z][A-Z0-9]{1,9}$')
);

CREATE UNIQUE INDEX project_key_key ON project (key);

CREATE TABLE project_member (
    project_id uuid NOT NULL REFERENCES project (id) ON DELETE CASCADE,
    person_id uuid NOT NULL REFERENCES person (id) ON DELETE CASCADE,
    role text NOT NULL CHECK (role IN ('editor', 'member')),
    added_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    added_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (project_id, person_id)
);

CREATE INDEX project_member_person ON project_member (person_id);

CREATE TABLE sprint (
    id uuid PRIMARY KEY,
    project_id uuid NOT NULL REFERENCES project (id) ON DELETE CASCADE,
    name text NOT NULL,
    goal text NOT NULL DEFAULT '',
    starts_on date NULL,
    ends_on date NULL,
    state text NOT NULL DEFAULT 'planned' CHECK (state IN ('planned', 'active', 'completed')),
    created_at timestamptz NOT NULL DEFAULT now(),
    started_at timestamptz NULL,
    completed_at timestamptz NULL,
    deleted_at timestamptz NULL,
    deleted_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    CONSTRAINT sprint_dates CHECK (starts_on IS NULL OR ends_on IS NULL OR ends_on >= starts_on)
);

CREATE INDEX sprint_project ON sprint (project_id);
CREATE UNIQUE INDEX sprint_one_active ON sprint (project_id) WHERE state = 'active' AND deleted_at IS NULL;

CREATE TABLE work_item (
    id uuid PRIMARY KEY,
    project_id uuid NOT NULL REFERENCES project (id) ON DELETE CASCADE,
    number integer NOT NULL,
    kind text NOT NULL CHECK (kind IN ('epic', 'story', 'task', 'bug')),
    title text NOT NULL,
    description text NOT NULL DEFAULT '',
    status text NOT NULL DEFAULT 'backlog' CHECK (status IN ('backlog', 'todo', 'in_progress', 'review', 'done')),
    priority integer NOT NULL DEFAULT 3 CHECK (priority BETWEEN 1 AND 4),
    points integer NULL CHECK (points IS NULL OR points BETWEEN 0 AND 999),
    assignee_id uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    reporter_id uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    sprint_id uuid NULL REFERENCES sprint (id) ON DELETE SET NULL,
    parent_id uuid NULL REFERENCES work_item (id) ON DELETE SET NULL,
    rank bigint NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    updated_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz NULL,
    deleted_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    CONSTRAINT work_item_number_key UNIQUE (project_id, number)
);

CREATE INDEX work_item_project_rank ON work_item (project_id, rank);
CREATE INDEX work_item_sprint ON work_item (sprint_id);
CREATE INDEX work_item_assignee ON work_item (assignee_id);
CREATE INDEX work_item_parent ON work_item (parent_id);

CREATE TABLE work_item_event (
    id uuid PRIMARY KEY,
    work_item_id uuid NOT NULL REFERENCES work_item (id) ON DELETE CASCADE,
    actor_id uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    occurred_at timestamptz NOT NULL DEFAULT now(),
    field text NOT NULL,
    old_value text NULL,
    new_value text NULL
);

CREATE INDEX work_item_event_item ON work_item_event (work_item_id, occurred_at);

CREATE TABLE work_item_comment (
    id uuid PRIMARY KEY,
    work_item_id uuid NOT NULL REFERENCES work_item (id) ON DELETE CASCADE,
    author_id uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    body text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    edited_at timestamptz NULL,
    deleted_at timestamptz NULL,
    deleted_by uuid NULL REFERENCES person (id) ON DELETE SET NULL
);

CREATE INDEX work_item_comment_item ON work_item_comment (work_item_id, created_at);

CREATE TABLE attachment (
    id uuid PRIMARY KEY,
    project_id uuid NOT NULL REFERENCES project (id) ON DELETE CASCADE,
    work_item_id uuid NULL REFERENCES work_item (id) ON DELETE CASCADE,
    uploaded_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    file_name text NOT NULL,
    content_type text NOT NULL,
    length bigint NOT NULL DEFAULT 0,
    sha256 bytea NULL,
    complete boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz NULL,
    deleted_by uuid NULL REFERENCES person (id) ON DELETE SET NULL
);

CREATE INDEX attachment_item ON attachment (work_item_id);
CREATE INDEX attachment_project ON attachment (project_id);

CREATE TABLE attachment_chunk (
    attachment_id uuid NOT NULL REFERENCES attachment (id) ON DELETE CASCADE,
    sequence integer NOT NULL,
    data bytea NOT NULL,
    PRIMARY KEY (attachment_id, sequence)
);

ALTER TABLE attachment_chunk ALTER COLUMN data SET STORAGE EXTERNAL;

CREATE TABLE nest_entry (
    id uuid PRIMARY KEY,
    kind text NOT NULL CHECK (kind IN ('link', 'fact', 'comment')),
    keys text[] NOT NULL,
    url text NULL,
    fields jsonb NULL,
    text text NULL,
    stored_at timestamptz NOT NULL DEFAULT now(),
    deleted_at timestamptz NULL,
    deleted_by uuid NULL REFERENCES person (id) ON DELETE SET NULL,
    purged_at timestamptz NULL,
    CONSTRAINT nest_entry_purged CHECK (purged_at IS NULL OR deleted_at IS NOT NULL)
);

CREATE INDEX nest_entry_keys ON nest_entry USING gin (keys);
