CREATE TABLE person_totp (
    person_id uuid PRIMARY KEY REFERENCES person (id) ON DELETE CASCADE,
    secret text NOT NULL,
    last_step bigint NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL DEFAULT now(),
    enabled_at timestamptz NULL
);

CREATE TABLE person_recovery_code (
    person_id uuid NOT NULL REFERENCES person (id) ON DELETE CASCADE,
    code_hash text NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    used_at timestamptz NULL,
    PRIMARY KEY (person_id, code_hash)
);
