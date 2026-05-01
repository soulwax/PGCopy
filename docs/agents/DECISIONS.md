# Decisions

This is a lightweight decision log for future agents.

## 2026-04-30: Keep the CLI as the Core

The CLI remains first-class and scriptable. Any no-terminal UI must stay a thin wrapper around the same core migration code.

Reason: PostgresCopy should work in automation and should also be easy to use without a terminal.

## 2026-04-30: Target .NET 10

Projects target `net10.0`.

Reason: the user requested the latest .NET framework available in this environment.

## 2026-04-30: Use a Native C# Desktop GUI

The no-terminal experience is a small native C# desktop GUI in `src/PostgresCopy.Desktop`. The current `src/PostgresCopy.Web` project is an interim prototype and should not become the long-term product UI.

Reason: PostgresCopy only needs a few inputs, clear options, and live output. A localhost web server is unnecessary machinery for that job, while a native desktop shell keeps the app closer to a simple C# utility.

## 2026-04-30: Destination Schema Must Match for Now

The data-copy path assumes destination schema and tables already exist.

Reason: schema generation is a separate problem. Keeping it separate makes data transfer safer and easier to verify.

## 2026-04-30: Destructive Actions Are Explicit

Destination truncation is allowed only through `--truncate-destination` or an explicit GUI checkbox. CLI uses `--yes` or an interactive `TRUNCATE` confirmation. GUI paths require typing `TRUNCATE`.

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
