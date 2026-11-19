# 0002 Three projects, feature folders

Status: accepted

- `MyBlazorPlayground.Domain`: rules with no I/O: rich text rendering, password hashing, setup codes, account and work rules, the access policy, file names and image sniffing. No package references.
- `MyBlazorPlayground.Data`: PostgreSQL through Npgsql and Dapper. One class per aggregate (`PersonStore`, `ProjectStore`, `WorkItemStore`, …), the migration runner and `Deletions`. Knows nothing about HTTP.
- `MyBlazorPlayground.Web`: Blazor, endpoints and application services, by feature: `Identity`, `Work`, `Nest` (homes), `Chaos` (toys), `Administration`, `Persistence`, shared `Infrastructure`. Each feature registers itself (`AddWork`, `AddNest`, …) so `Program.cs` only composes.
- Chaos and nest were merged in rather than referenced: their shared infrastructure (outboxes, token buckets, hub guard, origin policy, security headers) was identical and now lives once in `Infrastructure`, driven by `SiteOptions`. Their feature limits keep their own `Chaos__*` and `Nest__*` sections.
- Components keep chaos and nest's paths (`Components/Islands`, `Components/Pages`, `wwwroot/js`) so their JavaScript modules and relative imports work unchanged.
