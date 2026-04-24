using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace LicitIA.Api.Services
{
    public class OeceDataService
    {
        private readonly HttpClient _httpClient;
        private readonly string _baseUrl = "https://data.open-contracting.org/en/publication/135/download";

        public OeceDataService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public async Task<List<ScrapedOpportunity>> DownloadOeceDataAsync(int year, CancellationToken cancellationToken = default)
        {
            Console.WriteLine($"[OeceData] Descargando datos de OECE para el año {year}...");

            string url = $"{_baseUrl}?name={year}.jsonl.gz";
            
            try
            {
                var response = await _httpClient.GetAsync(url, cancellationToken);
                response.EnsureSuccessStatusCode();

                var compressedData = await response.Content.ReadAsByteArrayAsync(cancellationToken);
                Console.WriteLine($"[OeceData] Archivo descargado: {compressedData.Length} bytes");

                // Descomprimir Gzip
                using var compressedStream = new MemoryStream(compressedData);
                using var gzipStream = new GZipStream(compressedStream, CompressionMode.Decompress);
                using var decompressedStream = new MemoryStream();
                await gzipStream.CopyToAsync(decompressedStream, cancellationToken);
                decompressedStream.Position = 0;

                Console.WriteLine($"[OeceData] Archivo descomprimido: {decompressedStream.Length} bytes");

                // Parsear JSONL
                var opportunities = new List<ScrapedOpportunity>();
                using var reader = new StreamReader(decompressedStream);
                string? line;
                int count = 0;

                while ((line = await reader.ReadLineAsync(cancellationToken)) != null)
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;

                    try
                    {
                        var jsonDoc = JsonDocument.Parse(line);
                        var opportunity = ParseOeceJson(jsonDoc, year);
                        
                        if (opportunity != null)
                        {
                            opportunities.Add(opportunity);
                            count++;
                        }

                        // Limitar a 1000 oportunidades para encontrar datos más recientes
                        if (count >= 1000) break;
                    }
                    catch (JsonException ex)
                    {
                        Console.WriteLine($"[OeceData] Error al parsear línea: {ex.Message}");
                    }
                }

                Console.WriteLine($"[OeceData] Se parsearon {opportunities.Count} oportunidades");
                return opportunities;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[OeceData] Error al descargar datos: {ex.Message}");
                throw;
            }
        }

        private ScrapedOpportunity? ParseOeceJson(JsonDocument jsonDoc, int year)
        {
            try
            {
                var root = jsonDoc.RootElement;
                
                // Extraer datos del formato OCDS real
                // El formato tiene tender y buyer directamente en el nivel raíz
                JsonElement tender;
                if (!root.TryGetProperty("tender", out tender))
                {
                    return null;
                }

                string processCode = "";
                string title = "";
                string entityName = "";
                decimal estimatedAmount = 0;
                DateTime closingDate = DateTime.Now.AddDays(30);
                DateTime publishedDate = DateTime.Now;
                string category = "Servicios";
                string modality = "Licitación Pública";
                string description = "";

                // Extraer código de proceso
                if (tender.TryGetProperty("id", out var idElement))
                {
                    processCode = idElement.GetString() ?? "";
                }

                // Extraer título
                if (tender.TryGetProperty("title", out var titleElement))
                {
                    title = titleElement.GetString() ?? "";
                }

                // Extraer entidad
                if (root.TryGetProperty("buyer", out var buyerElement))
                {
                    if (buyerElement.TryGetProperty("name", out var buyerName))
                    {
                        entityName = buyerName.GetString() ?? "";
                    }
                }

                // Extraer monto
                if (tender.TryGetProperty("value", out var valueElement))
                {
                    if (valueElement.TryGetProperty("amount", out var amountElement))
                    {
                        estimatedAmount = amountElement.GetDecimal();
                    }
                }

                // Extraer fecha de cierre
                if (tender.TryGetProperty("tenderPeriod", out var tenderPeriod))
                {
                    if (tenderPeriod.TryGetProperty("endDate", out var endDateElement))
                    {
                        if (DateTime.TryParse(endDateElement.GetString(), out var parsedDate))
                        {
                            closingDate = parsedDate;
                        }
                    }
                }

                // Extraer categoría
                if (tender.TryGetProperty("mainProcurementCategory", out var categoryElement))
                {
                    category = NormalizeCategory(categoryElement.GetString() ?? "Servicios");
                }

                // Extraer modalidad
                if (tender.TryGetProperty("procurementMethodDetails", out var modalityElement))
                {
                    modality = modalityElement.GetString() ?? "Licitación Pública";
                }

                // Extraer descripción
                if (tender.TryGetProperty("description", out var descElement))
                {
                    description = descElement.GetString() ?? "";
                }

                // Extraer fecha de publicación - intentar diferentes campos del formato OCDS
                // Primero intentar tender.datePublished
                if (tender.TryGetProperty("datePublished", out var publishedDateElement))
                {
                    if (DateTime.TryParse(publishedDateElement.GetString(), out var parsedPublishedDate))
                    {
                        publishedDate = parsedPublishedDate;
                    }
                }
                // Si no existe, intentar tender.tenderPeriod.startDate
                else if (tender.TryGetProperty("tenderPeriod", out var tenderPeriod2))
                {
                    if (tenderPeriod2.TryGetProperty("startDate", out var startDateElement))
                    {
                        if (DateTime.TryParse(startDateElement.GetString(), out var parsedStartDate))
                        {
                            publishedDate = parsedStartDate;
                        }
                    }
                }
                // Si no existe, intentar datePublished en el nivel raíz
                else if (root.TryGetProperty("datePublished", out var rootPublishedDate))
                {
                    if (DateTime.TryParse(rootPublishedDate.GetString(), out var parsedRootPublishedDate))
                    {
                        publishedDate = parsedRootPublishedDate;
                    }
                }

                // Si no hay datos suficientes, retornar null
                if (string.IsNullOrWhiteSpace(processCode) && string.IsNullOrWhiteSpace(title))
                {
                    return null;
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
                Console.WriteLine($"[OeceData] Error al parsear JSON: {ex.Message}");
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
    }
}
