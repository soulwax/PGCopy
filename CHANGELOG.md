# Changelog

All notable changes to PostgresCopy are documented in this file, going forward from 0.2.0.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/), and this project uses [Semantic Versioning](https://semver.org/).

For narrative, per-release summaries see [RELEASE_NOTES.md](RELEASE_NOTES.md).

## [Unreleased]

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

[Unreleased]: https://github.com/soulwax/PGCopy/compare/v0.2.0...HEAD
[0.2.0]: https://github.com/soulwax/PGCopy/compare/v0.1.0...v0.2.0
[0.1.0]: https://github.com/soulwax/PGCopy/releases/tag/v0.1.0
