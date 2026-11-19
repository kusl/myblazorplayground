CREATE TABLE board_message (
    id uuid PRIMARY KEY,
    text text NOT NULL,
    meta jsonb NULL,
    stored_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE slot_spin (
    id uuid PRIMARY KEY,
    text text NOT NULL,
    winner boolean NOT NULL,
    jackpot boolean NOT NULL,
    stored_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX slot_spin_winner ON slot_spin (id DESC) WHERE winner;
CREATE INDEX slot_spin_jackpot ON slot_spin (id DESC) WHERE jackpot;

CREATE TABLE latency_run (
    id uuid PRIMARY KEY,
    text text NOT NULL,
    browser_median integer NULL,
    server_median integer NULL,
    stored_at timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX latency_run_browser ON latency_run (browser_median, id) WHERE browser_median IS NOT NULL;
CREATE INDEX latency_run_server ON latency_run (server_median, id) WHERE server_median IS NOT NULL;

CREATE TABLE canvas_stroke (
    id uuid PRIMARY KEY,
    points integer[] NOT NULL,
    stored_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE canvas_reset (
    id uuid PRIMARY KEY,
    stored_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE postcard (
    id uuid PRIMARY KEY,
    text text NOT NULL,
    meta jsonb NULL,
    stored_at timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE postcard_image (
    postcard_id uuid NOT NULL REFERENCES postcard (id) ON DELETE CASCADE,
    position integer NOT NULL,
    content_type text NOT NULL,
    width integer NOT NULL,
    height integer NOT NULL,
    data bytea NOT NULL,
    PRIMARY KEY (postcard_id, position)
);

ALTER TABLE postcard_image ALTER COLUMN data SET STORAGE EXTERNAL;
