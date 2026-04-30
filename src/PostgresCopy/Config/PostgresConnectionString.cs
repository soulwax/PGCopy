using System.Text;
using Npgsql;

namespace PostgresCopy.Config;

public static class PostgresConnectionString
{
    private static readonly StringComparer KeyComparer = StringComparer.OrdinalIgnoreCase;

    public static DatabaseEndpoint Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ValidationException("Connection string cannot be empty.");
        }

        try
        {
            var builder = IsPostgresUrl(value)
                ? FromPostgresUrl(value)
                : new NpgsqlConnectionStringBuilder(value);

            if (string.IsNullOrWhiteSpace(builder.Host))
            {
                throw new ValidationException("Connection string must include a host.");
            }

            if (string.IsNullOrWhiteSpace(builder.Database))
            {
                throw new ValidationException("Connection string must include a database.");
            }

            var connectionString = builder.ConnectionString;
            var redacted = Redact(builder).ConnectionString;
            var comparisonKey = BuildComparisonKey(builder);

            return new DatabaseEndpoint(connectionString, redacted, comparisonKey);
        }
        catch (ValidationException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new ValidationException($"Invalid PostgreSQL connection string: {ex.Message}");
        }
    }

    public static string Redact(string value) => Parse(value).RedactedConnectionString;

    private static bool IsPostgresUrl(string value)
    {
        return Uri.TryCreate(value, UriKind.Absolute, out var uri)
            && (uri.Scheme.Equals("postgres", StringComparison.OrdinalIgnoreCase)
                || uri.Scheme.Equals("postgresql", StringComparison.OrdinalIgnoreCase));
    }

    private static NpgsqlConnectionStringBuilder FromPostgresUrl(string value)
    {
        var uri = new Uri(value);
        var userInfoParts = uri.UserInfo.Split(':', 2);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Database = uri.AbsolutePath.TrimStart('/')
        };

        if (uri.Port > 0)
        {
            builder.Port = uri.Port;
        }

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
}
