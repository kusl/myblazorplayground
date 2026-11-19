# 0009 Markdown with pasted screenshots

Status: accepted

- Descriptions and comments are stored as Markdown text and rendered on the server by `RichText`, which escapes everything first and then builds a fixed set of elements: paragraphs, line breaks, headings, bold, italic, strikethrough, inline and fenced code, lists, task lists, quotes, rules, links and images. There is no HTML pass-through and no third-party sanitizer, so there is nothing to bypass; tests check that only those tags and attributes come out.
- Links may be `http`, `https`, `mailto` or a local path. Images are shown only from local paths, so pasted screenshots work and remote images cannot track readers (the CSP would block them anyway). `#12` links to item 12 of the same project.
- Pasting or dropping files into an editor uploads each one immediately to `POST /work/{key}/attachments` (raw body, file name in `X-File-Name`, antiforgery token in a header) and inserts `![name](url)` or `[name](url)` at the cursor. Screenshots from the clipboard are named by time. Pasted HTML is converted to Markdown in the browser.
- Uploaded files are served from `/work/files/{id}/{name}` to project members only. Images (PNG, JPEG, GIF, WebP, sniffed from the bytes) are shown inline; everything else downloads as `application/octet-stream` with `nosniff` and a sandboxing CSP.
- The size limit is `Work__MaxAttachmentBytes`, default 95 MiB, because Cloudflare refuses request bodies over 100 MB on the free plan. Storage itself has no practical limit (ADR 0004).
- Preview sends the text to `POST /work/{key}/preview`, which returns the same server rendering.
