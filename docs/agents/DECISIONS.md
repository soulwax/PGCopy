# Decisions

This is a lightweight decision log for future agents.

## 2026-04-30: Keep the CLI as the Core

The CLI remains first-class and scriptable. The web app is a thin local wrapper around the same core migration code.

Reason: PostgresCopy should work in automation and should also be easy to use without a terminal.

## 2026-04-30: Target .NET 10

Projects target `net10.0`.

Reason: the user requested the latest .NET framework available in this environment.

## 2026-04-30: Add a Local Web App, Not a Desktop App

The no-terminal experience lives in `src/PostgresCopy.Web`.

Reason: a local single-page web app is simple, cross-platform enough for .NET users, easy to inspect, and avoids GUI framework overhead.

## 2026-04-30: Destination Schema Must Match for Now

The data-copy path assumes destination schema and tables already exist.

Reason: schema generation is a separate problem. Keeping it separate makes data transfer safer and easier to verify.

## 2026-04-30: Destructive Actions Are Explicit

Destination truncation is allowed only through `--truncate-destination` or the web checkbox. CLI uses `--yes` or an interactive `TRUNCATE` confirmation. Web requires typing `TRUNCATE`.

Reason: productive repeated migrations need a way to empty destination tables, but destructive behavior must never be surprising.

## 2026-04-30: Use Npgsql Binary COPY for Data

Data transfer uses PostgreSQL binary COPY streams through Npgsql.

Reason: it is fast, PostgreSQL-native, and avoids loading full tables into memory.

## 2026-04-30: Docker Integration Is Manual

The integration setup is a PowerShell script plus Docker Compose files, not an always-on test project.

Reason: database integration tests are valuable but should not make normal unit test runs heavy or fragile.

## 2026-04-30: Verification Starts With Row Counts

`--verify` compares origin and destination row counts after copying.

Reason: row counts are cheap, understandable, and useful as a first trust check. Checksums can come later if needed.

## 2026-04-30: NuGet Sources Are Repo-Local

`NuGet.config` clears inherited package sources and uses nuget.org.

Reason: a missing user-level NuGet source caused restore failures in this environment.
