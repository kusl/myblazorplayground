# 0011 Headers, CSP and forms

Status: accepted

- The chaos policy stays: `default-src 'self'`, scripts and styles only from this site, no inline scripts or `style` attributes, `img-src 'self' blob:`, `connect-src 'self'` plus the latency endpoints, `frame-ancestors 'none'`, `form-action 'self'`. Progress bars use classes, not inline widths.
- Chaos and nest replaced antiforgery with a cookieless stub because they had no forms. This site has forms, so the standard antiforgery cookie and tokens are back. JSON endpoints validate the token from the `RequestVerificationToken` header.
- `/_blazor` only accepts WebSockets from this origin (or `Site__AllowedOrigins`), with per-client and total connection limits. Requests are rate limited per client address after `X-Forwarded-For`.
- Return URLs after sign-in are accepted only when they are local paths.
- Rendered user content goes through `RichText` or Razor encoding only.
