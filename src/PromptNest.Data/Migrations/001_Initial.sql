CREATE TABLE IF NOT EXISTS folders (
  id          TEXT PRIMARY KEY,
  name        TEXT NOT NULL,
  parent_id   TEXT REFERENCES folders(id) ON DELETE CASCADE,
  sort_order  INTEGER NOT NULL DEFAULT 0,
  created_at  INTEGER NOT NULL
);

CREATE TABLE IF NOT EXISTS prompts (
  id             TEXT PRIMARY KEY,
  title          TEXT NOT NULL,
  body           TEXT NOT NULL,
  variables_json TEXT NOT NULL DEFAULT '[]',
  folder_id      TEXT REFERENCES folders(id) ON DELETE SET NULL,
  is_favorite    INTEGER NOT NULL DEFAULT 0,
  use_count      INTEGER NOT NULL DEFAULT 0,
  last_used_at   INTEGER,
  created_at     INTEGER NOT NULL,
  updated_at     INTEGER NOT NULL,
  deleted_at     INTEGER
);

CREATE INDEX IF NOT EXISTS idx_prompts_folder
  ON prompts(folder_id);

CREATE INDEX IF NOT EXISTS idx_prompts_favorite
  ON prompts(is_favorite)
  WHERE is_favorite = 1 AND deleted_at IS NULL;

CREATE INDEX IF NOT EXISTS idx_prompts_updated
  ON prompts(updated_at DESC);

CREATE INDEX IF NOT EXISTS idx_prompts_last_used
  ON prompts(last_used_at DESC);

CREATE TABLE IF NOT EXISTS tags (
  name   TEXT PRIMARY KEY COLLATE NOCASE,
  color  TEXT,
  count  INTEGER NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS prompt_tags (
  prompt_id  TEXT NOT NULL REFERENCES prompts(id) ON DELETE CASCADE,
  tag_name   TEXT NOT NULL REFERENCES tags(name) ON DELETE CASCADE,
  PRIMARY KEY (prompt_id, tag_name)
);

CREATE TABLE IF NOT EXISTS variable_values (
  prompt_id      TEXT NOT NULL REFERENCES prompts(id) ON DELETE CASCADE,
  variable_name  TEXT NOT NULL,
  value          TEXT NOT NULL,
  updated_at     INTEGER NOT NULL,
  PRIMARY KEY (prompt_id, variable_name)
);

CREATE TABLE IF NOT EXISTS settings (
  key        TEXT PRIMARY KEY,
  value      TEXT NOT NULL,
  updated_at INTEGER NOT NULL
);

CREATE VIRTUAL TABLE IF NOT EXISTS prompts_fts USING fts5(
  prompt_id UNINDEXED,
  title,
  body,
  tags,
  tokenize='porter unicode61'
);

CREATE TRIGGER IF NOT EXISTS prompts_ai
AFTER INSERT ON prompts
WHEN new.deleted_at IS NULL
BEGIN
  INSERT INTO prompts_fts(prompt_id, title, body, tags)
  VALUES (new.id, new.title, new.body, '');
END;

CREATE TRIGGER IF NOT EXISTS prompts_au
AFTER UPDATE ON prompts
BEGIN
  DELETE FROM prompts_fts WHERE prompt_id = old.id;

  INSERT INTO prompts_fts(prompt_id, title, body, tags)
  SELECT
    new.id,
    new.title,
    new.body,
    COALESCE((SELECT group_concat(tag_name, ' ') FROM prompt_tags WHERE prompt_id = new.id), '')
  WHERE new.deleted_at IS NULL;
END;

CREATE TRIGGER IF NOT EXISTS prompts_ad
AFTER DELETE ON prompts
BEGIN
  DELETE FROM prompts_fts WHERE prompt_id = old.id;
END;

CREATE TRIGGER IF NOT EXISTS prompt_tags_ai
AFTER INSERT ON prompt_tags
BEGIN
  UPDATE tags SET count = count + 1 WHERE name = new.tag_name;

  UPDATE prompts_fts
  SET tags = COALESCE((SELECT group_concat(tag_name, ' ') FROM prompt_tags WHERE prompt_id = new.prompt_id), '')
  WHERE prompt_id = new.prompt_id;
END;

CREATE TRIGGER IF NOT EXISTS prompt_tags_ad
AFTER DELETE ON prompt_tags
BEGIN
  UPDATE tags SET count = CASE WHEN count > 0 THEN count - 1 ELSE 0 END WHERE name = old.tag_name;

  UPDATE prompts_fts
  SET tags = COALESCE((SELECT group_concat(tag_name, ' ') FROM prompt_tags WHERE prompt_id = old.prompt_id), '')
  WHERE prompt_id = old.prompt_id;
END;
