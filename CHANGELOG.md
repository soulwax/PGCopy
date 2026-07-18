# Changelog

All notable changes to PostgresCopy are documented in this file, going forward from 0.2.0.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses [Semantic Versioning](https://semver.org/).

For narrative, per-release summaries see [RELEASE_NOTES.md](RELEASE_NOTES.md).

## [Unreleased]

## [0.3.0] - 2026-07-18

### Added

- SSL is now required by default on both origin and destination connections (`sslmode=require`), applied automatically unless opted out. New CLI flags `--no-origin-require-ssl` / `--no-destination-require-ssl`, and matching "Require SSL" checkboxes next to the Origin URL and Destination URL fields on the desktop Connection tab (checked by default). The requirement only ever raises a weaker `sslmode` up to `require` — an already-stricter value (`verify-ca`, `verify-full`) already present in the connection string is preserved, never downgraded.
- `--all-databases` (CLI and desktop) no longer requires a database name in the Origin/Destination URL — the database name is discarded and replaced per-iteration anyway, so a bare `postgres://user:pass@host:5432` is now accepted. Single-database mode is unchanged and still requires a database name.
- Per-user, no-admin NSIS installer for the Windows desktop app (`installer/PostgresCopy.nsi`, `scripts/build-installer.ps1`), producing `PostgresCopy-Setup-<version>.exe`. Wraps the existing `install-desktop.ps1`/`uninstall-desktop.ps1` scripts rather than duplicating install logic, so the installer, the scripted install path, and the "Installed apps" uninstall entry all stay driven by the same source of truth. Requires NSIS (`winget install NSIS.NSIS`) to build; not installed automatically.

### Fixed

- `PostgresConnectionString` rejected the standard hyphenated `sslmode=verify-ca` / `sslmode=verify-full` URL query values that its own error message told users to use — Npgsql's connection-string indexer only accepts the non-hyphenated enum spellings (`VerifyCA`, `VerifyFull`). Both spellings are now normalized before being handed to Npgsql.
- `install-desktop.ps1` failed with `CommandNotFoundException` when run standalone (e.g. staged by the new NSIS installer) because it assumed a sibling `scripts\publish-desktop.ps1` and repo `artifacts\` folder existed. Added an `-AppSource` parameter so a caller can point directly at an already-built exe, skipping the repo-relative self-publish fallback entirely.

## [0.2.0] - 2026-07-18

### Added

- `--all-databases` / `--exclude-database` CLI flags: enumerate every non-system database on the origin server and drop, recreate, and copy each same-named database on the destination server. Public schema only in this release (true multi-schema enumeration is tracked in `TODO_POLISHING.md`).
- Matching desktop UI: a "Copy all databases" checkbox on the Connection tab, a "Load databases" checklist, and a typed-`OVERWRITE` confirmation dialog with a non-destructive default.
- `DestructiveActionPrompt.ConfirmOverwriteAllDatabases` — typed `OVERWRITE` confirmation for the CLI, mirroring the existing `TRUNCATE`/`DROP` confirmation words.
- `scripts/publish-cli.sh` — self-contained single-file CLI publish script for Linux and macOS (auto-detects `linux-x64`/`osx-x64`/`osx-arm64`). The desktop app remains Windows-only (WinForms); there is no Linux/macOS equivalent of `dist.ps1`.
- README section documenting the maintainer release checklist.

### Fixed

- Guarded `--all-databases` against a same-server origin/destination pair (different database name in the URL, same host:port) that would otherwise pass the existing per-database check and let the tool drop and overwrite the origin server's own databases.
- Corrected two CLI validation messages that implied `--all-databases` copies "every schema" when it only copies the public schema per database.

## [0.1.0] - 2026-04-30

First usable PostgresCopy build. See [RELEASE_NOTES.md](RELEASE_NOTES.md#010) for the full feature list.

[Unreleased]: https://github.com/soulwax/PGCopy/compare/v0.3.0...HEAD
[0.3.0]: https://github.com/soulwax/PGCopy/compare/v0.2.0...v0.3.0
[0.2.0]: https://github.com/soulwax/PGCopy/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/soulwax/PGCopy/releases/tag/v0.1.0
