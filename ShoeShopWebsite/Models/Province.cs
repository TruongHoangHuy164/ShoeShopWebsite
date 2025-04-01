using System.Collections.Generic;
using System.Text.Json.Serialization;

public class Province
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("division_type")]
    public string DivisionType { get; set; }

    [JsonPropertyName("districts")]
    public List<District> Districts { get; set; } = new List<District>();
}