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

Run the GUI from source:

```powershell
.\Start-PostgresCopy-Web.cmd
```

Or build and run the self-contained GUI:

```powershell
.\scripts\publish-web.ps1
.\Start-PostgresCopy-Web-Published.cmd
```

The published CLI lands at:

```powershell
.\artifacts\PostgresCopy-cli-win-x64\PostgresCopy.exe --help
```

## Local Web App

For a no-terminal workflow, run the small local web app:

```bash
dotnet run --project src/PostgresCopy.Web
```

On Windows, you can also double-click `Start-PostgresCopy-Web.cmd`, or run:

```powershell
.\scripts\run-web.ps1
```

Open the shown local URL, paste the origin and destination database URLs, choose optional schema/table filters, and click **Run dry run**. The web app starts in dry-run mode and shows a compact readiness summary before you run. Uncheck **Dry run** when you are ready to copy. The operations log streams progress as the migration runs, and **Cancel** stops the active request. Keep **Verify counts** checked to compare origin and destination row counts after copying. To empty planned destination tables first, check **Truncate destination** and type `TRUNCATE` to confirm.

The web app is local only. It does not store database URLs or run background services.

## Copy Checklist

1. Make sure the destination schema and tables already exist.
2. Start the web app with `Start-PostgresCopy-Web.cmd`.
3. Paste origin and destination URLs.
4. Keep **Dry run** checked and click **Run dry run**.
5. Review the operations log, especially destination row counts.
6. If destination tables contain data and you want to replace it, check **Truncate destination** and type `TRUNCATE`.
7. Uncheck **Dry run**.
8. Keep **Verify counts** checked.
9. Click **Run copy**.

## Safety

PostgresCopy refuses to run when origin and destination normalize to the same database. Passwords are redacted in console output, and a migration plan is printed before any data copy starts.

This version does not drop, recreate, or overwrite schema objects. Destination tables can be truncated only with the explicit `--truncate-destination` flag or the web checkbox, and both require confirmation. If destination tables are missing or incompatible, the migration fails loudly before copying.

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

Create a self-contained Windows web-app build:

```powershell
.\scripts\publish-web.ps1
```

Both scripts write to `artifacts/`.

After publishing, run:

```powershell
.\Start-PostgresCopy-Web-Published.cmd
```

The published app opens at `http://localhost:5087` by default.

## Known Limits

- Destination schemas and tables must already exist.
- Schema copy is deliberately separate from data copy and is not implemented yet.
- Copies are table-data transfers, not upserts or conflict resolution.
- Foreign-key ordering covers discoverable dependencies in the selected tables.
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

- It's simple and scriptable. The repo also includes a small local web app for a no-terminal workflow.

#### Why C#?

- It's a clean language with great libraries for database access and CLI development. I also wanted to practice my C# skills.

#### Why not use an existing tool?

- I wanted a tool that is simple, focused, and easy to run without installing anything extra. Existing tools often have more features than I need and require additional setup. This way I can control exactly how the copy works and learn a lot in the process.

## License

This project is licensed under the GPLv3.0 License. See the [LICENSE](LICENSE.md) file for details.
