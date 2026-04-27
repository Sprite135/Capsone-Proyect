namespace LicitIA.Api.Services;

public class ScrapedOpportunity
{
    public string ProcessCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string EntityName { get; set; } = string.Empty;
    public decimal EstimatedAmount { get; set; }
    public DateTime? ClosingDate { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Modality { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? PublishedDate { get; set; }
    public string Location { get; set; } = string.Empty;
    public int MatchScore { get; set; }
    public int MatchedKeywordsCount { get; set; }
}
