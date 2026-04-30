# PostgresCopy

PostgresCopy is a small C# CLI for copying PostgreSQL table data from one database to another.

The MVP is intentionally narrow:

- PostgreSQL only.
- Origin and destination are supplied as connection strings or `postgres://` URLs.
- Destination schema and tables must already exist.
- Data is copied table by table with PostgreSQL binary `COPY`.
- Destructive actions are not implemented yet.

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
--verbose
```

## Local Web App

For a no-terminal workflow, run the small local web app:

```bash
dotnet run --project src/PostgresCopy.Web
```

Open the shown local URL, paste the origin and destination database URLs, choose optional schema/table filters, and click **Run copy**. The operations log streams progress as the migration runs.

## Safety

PostgresCopy refuses to run when origin and destination normalize to the same database. Passwords are redacted in console output, and a migration plan is printed before any data copy starts.

This version does not drop, truncate, recreate, or overwrite schema objects. If destination tables are missing or incompatible, the migration fails loudly and exits non-zero.

## Development

```bash
dotnet restore
dotnet build
dotnet test
```

The current project targets `net10.0`.

## Integration Check

The repo includes a small Docker-backed integration check with two PostgreSQL databases:

```powershell
.\scripts\integration-test.ps1
```

It starts an origin database with sample data, starts a destination database with matching empty tables, runs PostgresCopy, then compares row counts.
