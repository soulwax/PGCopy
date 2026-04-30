using System.Text.Json;

namespace PostgresCopy.Web;

public static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new(JsonSerializerDefaults.Web);
}
