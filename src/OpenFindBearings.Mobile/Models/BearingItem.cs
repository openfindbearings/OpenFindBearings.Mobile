using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace OpenFindBearings.Mobile.Models;

/// <summary>轴承摘要（列表项）</summary>
public class BearingItem
{
    [JsonPropertyName("partNumber")] public string PartNumber { get; set; } = "";
    [JsonPropertyName("brandName")] public string BrandName { get; set; } = "";
    [JsonPropertyName("bearingTypeName")] public string BearingTypeName { get; set; } = "";
}

/// <summary>轴承分页查询结果</summary>
public class PagedBearingResult
{
    [JsonPropertyName("items")] public List<BearingItem> Items { get; set; } = new();
    [JsonPropertyName("totalCount")] public int TotalCount { get; set; }
}
