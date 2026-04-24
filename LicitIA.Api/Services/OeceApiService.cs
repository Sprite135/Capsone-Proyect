using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace LicitIA.Api.Services;

public class OeceApiService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OeceApiService> _logger;

    public OeceApiService(HttpClient httpClient, ILogger<OeceApiService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.BaseAddress = new Uri("https://contratacionesabiertas.oece.gob.pe/api/v1/");
    }

    public async Task<List<ScrapedOpportunity>> DownloadOeceDataAsync(int maxPages = 10, DateTime? fromDate = null, CancellationToken cancellationToken = default, int? maxResults = null)
    {
        var opportunities = new List<ScrapedOpportunity>();
        int currentPage = 1;
        int totalOpportunities = 0;

        _logger.LogInformation("[OeceApi] Iniciando descarga de datos de OECE API REST...");
        if (fromDate.HasValue)
        {
            _logger.LogInformation($"[OeceApi] Filtrando por fecha de publicación desde: {fromDate:yyyy-MM-dd}");
        }
        if (maxResults.HasValue)
        {
            _logger.LogInformation($"[OeceApi] Limitando a {maxResults} resultados más recientes");
        }

        try
        {
            while (currentPage <= maxPages)
            {
                _logger.LogInformation($"[OeceApi] Descargando página {currentPage}...");

                var response = await _httpClient.GetAsync($"records?page={currentPage}", cancellationToken);
                response.EnsureSuccessStatusCode();

                var content = await response.Content.ReadAsStringAsync(cancellationToken);
                var jsonDoc = JsonDocument.Parse(content);

                var records = jsonDoc.RootElement.GetProperty("records");

                if (records.GetArrayLength() == 0)
                {
                    _logger.LogInformation($"[OeceApi] No hay más registros en la página {currentPage}.");
                    break;
                }

                foreach (var record in records.EnumerateArray())
                {
                    var compiledRelease = record.GetProperty("compiledRelease");
                    var opportunity = ParseOeceRecord(compiledRelease);

                    if (opportunity != null)
                    {
                        // Filtrar por fecha de publicación si se especificó
                        if (fromDate.HasValue && opportunity.PublishedDate.HasValue)
                        {
                            if (opportunity.PublishedDate.Value < fromDate.Value)
                            {
                                // Si la fecha es anterior a la fecha mínima, continuar pero no agregar
                                continue;
                            }
                        }

                        opportunities.Add(opportunity);
                        totalOpportunities++;

                        // Si alcanzamos el máximo de resultados, detener
                        if (maxResults.HasValue && totalOpportunities >= maxResults)
                        {
                            _logger.LogInformation($"[OeceApi] Se alcanzó el máximo de {maxResults} resultados");
                            break;
                        }
                    }
                }

                _logger.LogInformation($"[OeceApi] Página {currentPage} procesada. Total oportunidades: {totalOpportunities}");

                // Si alcanzamos el máximo, detener el bucle
                if (maxResults.HasValue && totalOpportunities >= maxResults)
                {
                    break;
                }

                // Verificar si hay más páginas
                if (!jsonDoc.RootElement.TryGetProperty("links", out var links) ||
                    !links.TryGetProperty("next", out var nextLink))
                {
                    _logger.LogInformation($"[OeceApi] No hay más páginas disponibles.");
                    break;
                }

                currentPage++;
            }

            // Ordenar por fecha de publicación descendente (más recientes primero)
            if (opportunities.Count > 0)
            {
                opportunities = opportunities
                    .Where(o => o.PublishedDate.HasValue)
                    .OrderByDescending(o => o.PublishedDate.Value)
                    .ToList();

                _logger.LogInformation($"[OeceApi] Oportunidades ordenadas por fecha de publicación (más recientes primero)");
            }

            _logger.LogInformation($"[OeceApi] Descarga completada. Total oportunidades: {totalOpportunities}");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[OeceApi] Error al descargar datos: {ex.Message}");
            throw;
        }

        return opportunities;
    }

    private ScrapedOpportunity? ParseOeceRecord(JsonElement compiledRelease)
    {
        try
        {
            var tender = compiledRelease.GetProperty("tender");
            
            // Extraer campos básicos
            var processCode = tender.TryGetProperty("id", out var idElement) ? idElement.GetString() : "";
            var title = tender.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : "";
            var description = tender.TryGetProperty("description", out var descElement) ? descElement.GetString() : "";
            
            // Extraer entidad compradora
            string entityName = "";
            if (compiledRelease.TryGetProperty("buyer", out var buyer))
            {
                entityName = buyer.TryGetProperty("name", out var buyerName) ? buyerName.GetString() : "";
            }
            else if (tender.TryGetProperty("procuringEntity", out var procuringEntity))
            {
                entityName = procuringEntity.TryGetProperty("name", out var procuringName) ? procuringName.GetString() : "";
            }

            // Extraer monto estimado
            decimal estimatedAmount = 0;
            if (tender.TryGetProperty("value", out var value))
            {
                if (value.TryGetProperty("amount", out var amountElement))
                {
                    estimatedAmount = amountElement.TryGetDecimal(out var amount) ? amount : 0;
                }
            }

            // Extraer fecha de cierre
            DateTime? closingDate = null;
            if (tender.TryGetProperty("tenderPeriod", out var tenderPeriod))
            {
                if (tenderPeriod.TryGetProperty("endDate", out var endDateElement))
                {
                    if (DateTime.TryParse(endDateElement.GetString(), out var parsedEndDate))
                    {
                        closingDate = parsedEndDate;
                    }
                }
            }

            // Calcular fecha de publicación basada en el año del título y fecha de cierre
            DateTime? publishedDate = CalculatePublishedDateFromTitle(title, closingDate);

            // Extraer categoría y modalidad
            string category = "";
            string modality = "";
            if (tender.TryGetProperty("procurementMethodDetails", out var methodDetails))
            {
                modality = methodDetails.GetString() ?? "";
            }
            if (tender.TryGetProperty("mainProcurementCategory", out var mainCategory))
            {
                category = NormalizeCategory(mainCategory.GetString() ?? "");
            }

            // Si no hay fecha de cierre, usar una fecha por defecto (30 días después de publicación)
            if (closingDate == null && publishedDate.HasValue)
            {
                closingDate = publishedDate.Value.AddDays(30);
            }

            // Si no hay fecha de publicación, usar una fecha por defecto (30 días antes del cierre)
            if (publishedDate == null && closingDate.HasValue)
            {
                publishedDate = closingDate.Value.AddDays(-30);
            }

            return new ScrapedOpportunity
            {
                ProcessCode = processCode,
                Title = title,
                EntityName = entityName,
                EstimatedAmount = estimatedAmount,
                ClosingDate = closingDate,
                Category = category,
                Modality = modality,
                Description = description,
                PublishedDate = publishedDate
            };
        }
        catch (Exception ex)
        {
            _logger.LogWarning($"[OeceApi] Error al parsear registro: {ex.Message}");
            return null;
        }
    }


    private string NormalizeCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category))
            return "Servicios";

        // Normalizar categorías de OCDS a español
        return category.ToLower() switch
        {
            "goods" => "Bienes",
            "services" => "Servicios",
            "works" => "Obras",
            _ => category // Mantener otras categorías como están
        };
    }

    private DateTime? CalculatePublishedDateFromTitle(string title, DateTime? closingDate)
    {
        // Extraer el año del título
        int yearFromTitle = ExtractYearFromTitle(title);

        if (yearFromTitle > 0 && closingDate.HasValue)
        {
            // Usar el año del título y calcular 30 días antes del cierre
            var publishedDate = new DateTime(yearFromTitle, closingDate.Value.Month, closingDate.Value.Day).AddDays(-30);
            return publishedDate;
        }
        else if (yearFromTitle > 0)
        {
            // Si no hay fecha de cierre, usar el año del título y fecha actual
            return new DateTime(yearFromTitle, DateTime.Now.Month, DateTime.Now.Day);
        }
        else if (closingDate.HasValue)
        {
            // Si no se puede extraer el año, usar fecha de cierre menos 30 días
            return closingDate.Value.AddDays(-30);
        }

        return null;
    }

    private int ExtractYearFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title))
            return 0;

        // Buscar un año de 4 dígitos en el título (ej: 2024, 2025)
        var match = System.Text.RegularExpressions.Regex.Match(title, @"\b(20\d{2})\b");
        if (match.Success)
        {
            if (int.TryParse(match.Value, out int year))
            {
                // Validar que el año sea razonable (entre 2020 y 2030)
                if (year >= 2020 && year <= 2030)
                {
                    return year;
                }
            }
        }

        return 0;
    }
}
