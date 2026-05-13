// File: src/PostgresCopy/Config/PostgresConnectionString.cs

using System.Text;
using Npgsql;

namespace PostgresCopy.Config;

public static class PostgresConnectionString
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;
    private const string DefaultMaintenanceDatabase = "postgres";

    public static DatabaseEndpoint Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(
                "Connection string cannot be empty." + Environment.NewLine +
                "What happened: one side of the copy has no PostgreSQL URL or Npgsql connection string." + Environment.NewLine +
                "How to resolve: paste the origin database connection string into Origin URL and the separate target database connection string into Destination URL.");
        }

        try
        {
            var builder = ParseBuilder(value, allowMissingDatabase: false);
            ValidateHostAndDatabase(builder, value, "Connection string");

            return new DatabaseEndpoint(
                builder.ConnectionString,
                Redact(builder).ConnectionString,
                BuildComparisonKey(builder));
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (UriFormatException ex)
        {
            throw BuildConnectionValidationException(
                $"Invalid PostgreSQL URL: {ex.Message}",
                "The value starts like a URL, but it is not a valid absolute URL.",
                BuildUrlTroubleshootingHint(value));
        }
        catch (ArgumentException ex)
        {
            throw BuildConnectionValidationException(
                $"Invalid PostgreSQL connection string: {ex.Message}",
                "The value is not a valid postgres:// URL or Npgsql connection string.",
                BuildUrlTroubleshootingHint(value));
        }
        catch (Exception ex)
        {
            throw BuildConnectionValidationException(
                $"Invalid PostgreSQL connection string: {ex.Message}",
                "The value is not a valid postgres:// URL or Npgsql connection string.",
                BuildUrlTroubleshootingHint(value));
        }
    }

    public static PostgresConnectionInfo ParseForInspection(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException(
                "Database URL cannot be empty." + Environment.NewLine +
                "What happened: the database peek has no PostgreSQL URL or Npgsql connection string." + Environment.NewLine +
                "How to resolve: paste a PostgreSQL URL. Omit the database name to list databases, or include one to list tables and row counts.");
        }

        try
        {
            var builder = ParseBuilder(value, allowMissingDatabase: true);

            if (string.IsNullOrWhiteSpace(builder.Host))
            {
                throw BuildConnectionValidationException(
                    "Database URL must include a host.",
                    "PostgresCopy could parse the value, but it does not say which PostgreSQL server to contact.",
                    "Include a host, for example postgres://user:password@host:5432 or Host=host;Username=user;Password=...");
            }

            var requestedDatabase = string.IsNullOrWhiteSpace(builder.Database) ? null : builder.Database;
            if (requestedDatabase is null)
            {
                builder.Database = DefaultMaintenanceDatabase;
            }

            return new PostgresConnectionInfo(
                builder.ConnectionString,
                Redact(builder).ConnectionString,
                requestedDatabase,
                requestedDatabase is not null);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (UriFormatException ex)
        {
            throw BuildConnectionValidationException(
                $"Invalid PostgreSQL database URL: {ex.Message}",
                "The value starts like a URL, but it is not a valid absolute URL.",
                BuildUrlTroubleshootingHint(value));
        }
        catch (ArgumentException ex)
        {
            throw BuildConnectionValidationException(
                $"Invalid PostgreSQL database URL: {ex.Message}",
                "The value is not a valid postgres:// URL or Npgsql connection string.",
                BuildUrlTroubleshootingHint(value));
        }
        catch (Exception ex)
        {
            throw BuildConnectionValidationException(
                $"Invalid PostgreSQL database URL: {ex.Message}",
                "The value is not a valid postgres:// URL or Npgsql connection string.",
                BuildUrlTroubleshootingHint(value));
        }
    }

    public static string Redact(string value) => Parse(value).RedactedConnectionString;

    private static NpgsqlConnectionStringBuilder ParseBuilder(string value, bool allowMissingDatabase)
    {
        return LooksLikeUrl(value)
            ? FromPostgresUrl(value, allowMissingDatabase)
            : new NpgsqlConnectionStringBuilder(value);
    }

    private static void ValidateHostAndDatabase(
        NpgsqlConnectionStringBuilder builder,
        string originalValue,
        string label)
    {
        if (string.IsNullOrWhiteSpace(builder.Host))
        {
            throw BuildConnectionValidationException(
                $"{label} must include a host.",
                "PostgresCopy could parse the value, but it does not say which PostgreSQL server to contact.",
                "Include a host, for example postgres://user:password@host:5432/database or Host=host;Database=database;Username=user;Password=...");
        }

        if (string.IsNullOrWhiteSpace(builder.Database))
        {
            var urlSpecific = LooksLikeUrl(originalValue)
                ? " Add the database name after the host, for example postgres://user:password@host:5432/my_database."
                : " Add Database=my_database to the connection string.";

            throw BuildConnectionValidationException(
                $"{label} must include a database.",
                "PostgresCopy could parse the value, but it does not name the database on that server.",
                "Include a specific database for the copy workflow." + urlSpecific);
        }
    }

    private static bool LooksLikeUrl(string value)
    {
        var schemeSeparator = value.IndexOf("://", StringComparison.Ordinal);
        if (schemeSeparator <= 0)
        {
            return false;
        }

        return value[..schemeSeparator].All(c => char.IsLetterOrDigit(c) || c is '+' or '-' or '.');
    }

    private static bool IsPostgresUrl(Uri uri)
    {
        return uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase)
            || uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase);
    }

    private static NpgsqlConnectionStringBuilder FromPostgresUrl(string value, bool allowMissingDatabase)
    {
        var uri = new Uri(value);
        if (!IsPostgresUrl(uri))
        {
            throw BuildConnectionValidationException(
                $"Unsupported URL scheme '{uri.Scheme}'.",
                "The value looks like a URL, but it is not a PostgreSQL URL.",
                "Use postgres:// or postgresql://. For key/value connection strings, use Host=...;Database=...;Username=...;Password=... instead.");
        }

        if (string.IsNullOrWhiteSpace(uri.Host))
        {
            throw BuildConnectionValidationException(
                "PostgreSQL URL is missing a host.",
                "The URL has a scheme but no server name after it.",
                "Use a URL like postgres://user:password@host:5432/database.");
        }

        if (!allowMissingDatabase && string.IsNullOrWhiteSpace(uri.AbsolutePath.TrimStart('/')))
        {
            throw BuildConnectionValidationException(
                "PostgreSQL URL is missing a database name.",
                "The URL points to a server, but the copy workflow needs a specific source or target database.",
                "Add the database name after the host, for example postgres://user:password@host:5432/my_database.");
        }

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Database = uri.AbsolutePath.TrimStart('/')
        };

        if (uri.Port > 0)
        {
            builder.Port = uri.Port;
        }

        var userInfoParts = uri.UserInfo.Split(':', 2);
        if (userInfoParts.Length > 0 && !string.IsNullOrEmpty(userInfoParts[0]))
        {
            builder.Username = Uri.UnescapeDataString(userInfoParts[0]);
        }

        if (userInfoParts.Length > 1)
        {
            builder.Password = Uri.UnescapeDataString(userInfoParts[1]);
        }

        foreach (var (key, valuePart) in ParseQuery(uri.Query))
        {
            builder[key] = valuePart;
        }

        return builder;
    }

    private static IEnumerable<KeyValuePair<string, string>> ParseQuery(string query)
    {
        var trimmed = query.TrimStart('?');
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            yield break;
        }

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

            if (!string.IsNullOrWhiteSpace(key))
            {
                yield return new KeyValuePair<string, string>(key, value);
            }
        }
    }

    private static NpgsqlConnectionStringBuilder Redact(NpgsqlConnectionStringBuilder source)
    {
        var redacted = new NpgsqlConnectionStringBuilder(source.ConnectionString);
        if (!string.IsNullOrEmpty(redacted.Password))
        {
            redacted.Password = "***";
        }

        return redacted;
    }

    private static string BuildComparisonKey(NpgsqlConnectionStringBuilder builder)
    {
        var keys = builder.Keys
            .Cast<string>()
            .Where(key => !key.Equals("Password", StringComparison.OrdinalIgnoreCase))
            .OrderBy(key => key, KeyComparer);

        var normalized = new StringBuilder();
        foreach (var key in keys)
        {
            var value = builder[key]?.ToString() ?? string.Empty;
            normalized.Append(key.ToLowerInvariant()).Append('=').Append(value).Append(';');
        }

        return normalized.ToString();
    }

    private static ValidationException BuildConnectionValidationException(
        string summary,
        string whatHappened,
        string howToResolve)
    {
        return new ValidationException(
            summary + Environment.NewLine +
            $"What happened: {whatHappened}" + Environment.NewLine +
            $"How to resolve: {howToResolve}");
    }

    private static string BuildUrlTroubleshootingHint(string value)
    {
        if (LooksLikeUrl(value))
        {
            if (value.Contains('@') && !value.Contains("%40", StringComparison.OrdinalIgnoreCase))
            {
                return "Check the URL scheme, host, port, database name, and password encoding. If the password contains @, #, ?, &, /, or :, percent-encode those characters, for example @ becomes %40.";
            }

            return "Check the URL scheme, host, port, database name, and percent-encoding for special characters in the username or password.";
        }

        return "Check for missing semicolons and required fields such as Host, Database, Username, Password, Port, and SSL Mode.";
    }
}
