# 0013 Dependencies

Status: accepted

- Runtime: `Npgsql` 10.0.3 and `Dapper` 2.1.89, versions in `Directory.Packages.props` only. Nothing else beyond the ASP.NET Core shared framework, and no CSS or JavaScript library.
- Tests: `xunit.v3`, and `vitest`, `vite`, `@vitest/coverage-v8` with exact versions.
- Dependabot opens weekly grouped updates for NuGet, npm, Actions and container images.
