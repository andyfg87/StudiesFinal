using System.Text.Json;
using System.Text.RegularExpressions;

namespace StudiesFinal.Web.Extensions
{
    public static class LoggingExtensions
    {
        private static readonly string[] _sensitiveFields =
        {
            "Password", "ConfirmPassword", "PasswordHash", "Token", "Secret",
            "ApiKey", "ConnectionString", "AccessToken"
        };

        public static string ToSanitizedJson(this object obj)
        {
            if (obj == null) return "null";

            try
            {
                var options = new JsonSerializerOptions
                {
                    WriteIndented = false,
                    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                    ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles
                };

                var json = JsonSerializer.Serialize(obj, options);
                return SanitizeSensitiveData(json);
            }
            catch (Exception ex)
            {
                return $"{{\"error\": \"Failed to serialize: {ex.Message}\"}}";
            }
        }

        private static string SanitizeSensitiveData(string json)
        {
            foreach (var field in _sensitiveFields)
            {
                // "field": "valor"
                var pattern = $@"""{field}""\s*:\s*""[^""]*""";
                json = Regex.Replace(json, pattern, $@"""{field}"": ""***REDACTED***""", RegexOptions.IgnoreCase);

                // "field": null
                pattern = $@"""{field}""\s*:\s*null";
                json = Regex.Replace(json, pattern, $@"""{field}"": ""***REDACTED***""", RegexOptions.IgnoreCase);
            }

            return json;
        }
    }
}
