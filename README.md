# PostgresCopy

PostgresCopy is a small C# tool for copying PostgreSQL table data from one database to another.

The MVP is intentionally narrow:

- PostgreSQL only.
- Origin and destination are supplied as connection strings or `postgres://` URLs.
- Destination schema and tables must already exist.
- Data is copied table by table with PostgreSQL binary `COPY`.
- Destination truncation is explicit and confirmed.

## Usage

```bash
dotnet run --project src/PostgresCopy -- \
  --origin "postgres://postgres:secret@localhost:5432/source" \
  --destination "postgres://postgres:secret@localhost:5433/target"
```

By default, PostgresCopy copies all base tables in the `public` schema.

Useful options:

```bash
--schema public
--table users
--tables users,orders,products
--dry-run
--verify
--truncate-destination --yes
--verbose
```

## Try It Now

Run the native desktop app:

```powershell
.\Start-PostgresCopy-Desktop.cmd
```

Run the CLI from source:

```bash
dotnet run --project src/PostgresCopy -- --help
```

Build the self-contained CLI:

```powershell
.\scripts\publish-cli.ps1
```

The published CLI lands at:

```powershell
.\artifacts\PostgresCopy-cli-win-x64\PostgresCopy.exe --help
```

## Native Desktop App

For no-terminal use, PostgresCopy includes a small native C# desktop app over the existing migration core. It does not need a separate local web server just to collect an origin URL, a destination URL, options, and a progress log.

The native GUI stays as small as the CLI:

- one window
- origin and destination URL fields
- schema/table filters
- dry-run first
- verify counts
- explicit destination truncation with `TRUNCATE` confirmation
- live operations log
- cancel button
- no stored credentials
- no background service

Run it from source:

```powershell
.\Start-PostgresCopy-Desktop.cmd
```

Or publish and run the self-contained desktop app:

```powershell
.\scripts\publish-desktop.ps1
.\Start-PostgresCopy-Desktop-Published.cmd
```

The current `src/PostgresCopy.Web` project is an interim prototype for the no-terminal workflow. Keep it useful while it exists, but do not treat a localhost web app as the preferred final UI.

## Interim Web Prototype

Until the native desktop app exists, the local web prototype can still be run for manual testing:

```powershell
.\Start-PostgresCopy-Web.cmd
```

It is local only, does not store database URLs, and should not grow into a hosted dashboard or background service.

## Copy Checklist

1. Make sure the destination schema and tables already exist.
2. Start the native desktop app, CLI, or interim web prototype.
3. Paste origin and destination URLs.
4. Keep **Dry run** checked and click **Run dry run**.
5. Review the operations log, especially destination row counts.
6. If destination tables contain data and you want to replace it, check **Truncate destination** and type `TRUNCATE`.
7. Uncheck **Dry run**.
8. Keep **Verify counts** checked.
9. Click **Run copy**.

## Safety

PostgresCopy refuses to run when origin and destination normalize to the same database. Passwords are redacted in console output, and a migration plan is printed before any data copy starts.

This version does not drop, recreate, or overwrite schema objects. Destination tables can be truncated only with the explicit `--truncate-destination` flag or an equivalent GUI checkbox, and both require confirmation. If destination tables are missing or incompatible, the migration fails loudly before copying.

PostgresCopy also refuses to append into non-empty destination tables. Use explicit truncation when you want to replace destination data.

Dry-run mode still connects to both databases, checks destination readiness, and reports origin/destination row counts. It does not copy or truncate data.

If you filter to specific tables, PostgresCopy validates that those tables exist in the origin before checking or copying the destination.

## Development

```bash
dotnet restore
dotnet build
dotnet test
```

The current project targets `net10.0`.

Run the local non-Docker check suite:

```powershell
.\scripts\check.ps1
```

Run it with Docker integration enabled:

```powershell
.\scripts\check.ps1 -IncludeIntegration
```

## Publish

Create a self-contained Windows CLI build:

```powershell
.\scripts\publish-cli.ps1
```

Create a self-contained Windows desktop build:

```powershell
.\scripts\publish-desktop.ps1
```

The interim web prototype also has a publish script while it remains in the repo:

```powershell
.\scripts\publish-web.ps1
```

Publish scripts write to `artifacts/`.

## Known Limits

- Destination schemas and tables must already exist.
- Schema copy is deliberately separate from data copy and is not implemented yet.
- Copies are table-data transfers, not upserts or conflict resolution.
- Foreign-key ordering covers discoverable dependencies in the selected tables.
- The current web UI is a temporary prototype, not the intended long-term UI.
- Docker is required only for the integration script.

## Current Release

Current version: `0.1.0`

See [RELEASE_NOTES.md](RELEASE_NOTES.md) for the included behavior and deliberate omissions.

## Integration Check

The repo includes a small Docker-backed integration check with two PostgreSQL databases:

```powershell
.\scripts\integration-test.ps1
```

It starts an origin database with sample data, starts a destination database with matching empty tables, runs PostgresCopy, then compares row counts.


## FAQ

#### Why PostgreSQL only?

Because I needed a clear scope and this exact tool for myself.

#### Why PostgreSQL in general?

It can do the following things other databases only can dream of:
- Store JSON documents and query them with SQL.
- Perform complex queries with CTEs, window functions, and more.
- Handle large datasets efficiently with features like partitioning and indexing.
- Support advanced data types like arrays, hstore, and custom types.
- Be extended with custom functions, operators, and data types.
- Be used for both OLTP and OLAP workloads.

#### Why a CLI?

- It's simple and scriptable. The native C# desktop GUI is the no-terminal companion.

#### Why not keep the web app as the main GUI?

- A local web server is more machinery than this tool needs. The no-terminal experience should feel like a small desktop utility: paste two URLs, dry-run, copy, watch progress, done.

#### Why C#?

- It's a clean language with great libraries for database access and CLI development. I also wanted to practice my C# skills.

#### Why not use an existing tool?

- I wanted a tool that is simple, focused, and easy to run without installing anything extra. Existing tools often have more features than I need and require additional setup. This way I can control exactly how the copy works and learn a lot in the process.

## License

This project is licensed under the GPLv3.0 License. See the [LICENSE](LICENSE.md) file for details.
