# 0011 Headers, CSP and forms

Status: accepted

- The chaos policy stays: `default-src 'self'`, scripts and styles only from this site, no inline scripts or `style` attributes, `img-src 'self' blob:`, `connect-src 'self'` plus the latency endpoints, `frame-ancestors 'none'`, `form-action 'self'`. Progress bars use classes, not inline widths.
- Chaos and nest replaced antiforgery with a cookieless stub because they had no forms. This site has forms, so the standard antiforgery cookie and tokens are back. JSON endpoints validate the token from the `RequestVerificationToken` header.
- `/_blazor` only accepts WebSockets from this origin (or `Site__AllowedOrigins`), with per-client and total connection limits. Requests are rate limited per client address after `X-Forwarded-For`.
- Return URLs after sign-in are accepted only when they are local paths.
- Rendered user content goes through `RichText` or Razor encoding only.
- HTML responses add `no-transform` to `Cache-Control` so proxies leave the markup alone. Cloudflare may still inject an inline bot-detection script on quick tunnels; the CSP blocks it on purpose and the console warning about it is harmless.
