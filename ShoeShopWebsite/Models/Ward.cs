using System.Text.Json.Serialization;

public class Ward
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("division_type")]
    public string DivisionType { get; set; }

    public int DistrictId { get; set; }
    public District District { get; set; }
}