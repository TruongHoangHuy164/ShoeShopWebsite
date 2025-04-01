using System.Collections.Generic;
using System.Text.Json.Serialization;
using ShoeShopWebsite.Models;

public class District
{
    [JsonPropertyName("code")]
    public int Code { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("division_type")]
    public string DivisionType { get; set; }

    [JsonPropertyName("wards")]
    public List<Ward> Wards { get; set; } = new List<Ward>(); // Thuộc tính điều hướng tới Wards

    public int ProvinceId { get; set; } // Khóa ngoại tới Province
    public Province Province { get; set; } // Thuộc tính điều hướng tới Province
}