# PGCopy

PGCopy is a small C# CLI for copying PostgreSQL table data from one database to another.

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

By default, PGCopy copies all base tables in the `public` schema.

Useful options:

```bash
--schema public
--table users
--tables users,orders,products
--dry-run
--truncate-destination --yes
--verbose
```

## Local Web App

For a no-terminal workflow, run the small local web app:

```bash
dotnet run --project src/PostgresCopy.Web
```

Open the shown local URL, paste the origin and destination database URLs, choose optional schema/table filters, and click **Run copy**. The operations log streams progress as the migration runs. To empty planned destination tables first, check **Truncate destination** and type `TRUNCATE` to confirm.

## Safety

PGCopy refuses to run when origin and destination normalize to the same database. Passwords are redacted in console output, and a migration plan is printed before any data copy starts.

This version does not drop, recreate, or overwrite schema objects. Destination tables can be truncated only with the explicit `--truncate-destination` flag or the web checkbox, and both require confirmation. If destination tables are missing or incompatible, the migration fails loudly before copying.

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


## FAQ
#### Why PostgreSQL only? 

Because I needed a clear scope and this exact tool for myself.


#### Why Postgresql in general?

It can do the following things other databases only can dream of:
- Store JSON documents and query them with SQL.
- Perform complex queries with CTEs, window functions, and more.
- Handle large datasets efficiently with features like partitioning and indexing.
- Support advanced data types like arrays, hstore, and custom types.
- Be extended with custom functions, operators, and data types.
- Be used for both OLTP and OLAP workloads.


#### Why a CLI?
- It's simple and scriptable but only temporary. We will have a little user friendly window with console real time output soon.


#### Why C#?

- It's a clean language with great libraries for database access and CLI development. I also wanted to practice my C# skills.

#### Why not use an existing tool?

- I wanted a tool that is simple, focused, and easy to run without installing anything extra. Existing tools often have more features than I need and require additional setup. This way I can control exactly how the copy works and learn a lot in the process.

## License

This project is licensed under the GPLv3.0 License. See the [LICENSE](LICENSE.md) file for details.


