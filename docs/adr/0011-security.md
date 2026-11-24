# 0011 Headers, CSP and forms

Status: accepted

- The chaos policy stays: `default-src 'self'`, scripts and styles only from this site, no inline scripts or `style` attributes, `img-src 'self' blob:`, `connect-src 'self'` plus the latency endpoints, `frame-ancestors 'none'`, `form-action 'self'`. Progress bars use classes, not inline widths.
- Chaos and nest replaced antiforgery with a cookieless stub because they had no forms. This site has forms, so the standard antiforgery cookie and tokens are back. JSON endpoints validate the token from the `RequestVerificationToken` header.
- `/_blazor` only accepts WebSockets from this origin (or `Site__AllowedOrigins`), with per-client and total connection limits. Negotiation offers only WebSockets, so a refused browser does not fall back to server-sent events or long polling (which the guard answers with 400). Requests are rate limited per client address after `X-Forwarded-For`.
- A client address is often shared: a household behind one router, an office, a mobile carrier's NAT. The per-client limit (64) is sized for many tabs and devices on one address; the total (400) protects the server. A tab that is refused keeps its server-rendered page and says that live updates could not start, instead of the generic error.
- Return URLs after sign-in are accepted only when they are local paths.
- Rendered user content goes through `RichText` or Razor encoding only.
- HTML responses add `no-transform` to `Cache-Control` so proxies leave the markup alone. Cloudflare may still inject an inline bot-detection script on quick tunnels; the CSP blocks it on purpose and the console warning about it is harmless.
