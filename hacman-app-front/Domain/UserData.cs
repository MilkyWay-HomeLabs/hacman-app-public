namespace hacman_app_front.Domain;

using System.Text.Json.Serialization;

public class UserData
{
    [JsonPropertyName("userName")] public string? UserName { get; set; }

    [JsonPropertyName("login")] public string? Login { get; set; }
}