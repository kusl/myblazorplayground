# 0016 Shared styles stay generic, feature styles stay isolated

Status: accepted

- `wwwroot/app.css` holds the theme variables, element defaults and utilities that several features share on purpose: `lede`, `muted`, `hint`, `empty`, `status`, `badge`, `notice`, `form`, `field`, `actions`, `panel`, `stack`, `item-list`, `crumbs`, `danger-zone` and the like.
- Anything one component owns lives in that component's isolated stylesheet (`*.razor.css`), even when the class name looks generic: the work board (`board`, `column`, `card`), sprint progress bars, the item page (`split`, `attachments`, `comments`, `history`), the rich text editor, the project cards and the backlog filters.
- Reason: global rules with generic names leaked into the toys. The sprint `.progress` bar clipped the slots and latency counters to half a line, the work `.board` laid the latency leaderboards out sideways, `.history` shrank and indented the reading list, and `.card`, `.editor` and `.badge` changed postcards, the home details editor and slot badges.
- A component that reuses a shared utility name only adds to it. Islands keep their status line in the layout (`display: block`), because the shared `.status:empty` rule hides empty statuses.
- Status lines are errors unless JavaScript marks them with `data-tone="ok"` (good news) or `data-tone="note"` (progress, nothing to fix), through `wwwroot/js/status.js`.
- Elements that JavaScript creates carry no scope attribute, so isolated rules reach them through `::deep` from an element the component rendered.
