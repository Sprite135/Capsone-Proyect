using System.Globalization;
using CsvHelper;
using CsvHelper.Configuration;
using LicitIA.Api.Data;
using LicitIA.Api.Models;

namespace LicitIA.Api.Services;

public class CsvImportService
{
    private readonly OpportunityRepository _repository;

    public CsvImportService(OpportunityRepository repository)
    {
        _repository = repository;
    }

    public async Task<(int imported, int skipped, List<string> errors)> ImportFromCsvAsync(string csvContent, CancellationToken cancellationToken = default)
    {
        var imported = 0;
        var skipped = 0;
        var errors = new List<string>();

        try
        {
            using var reader = new StringReader(csvContent);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HeaderValidated = null,
                MissingFieldFound = null,
                BadDataFound = null
            });

            var records = csv.GetRecords<SeaceCsvRecord>();

            foreach (var record in records)
            {
                try
                {
                    // Verificar si ya existe
                    var existing = await _repository.GetByProcessCodeAsync(record.Nomenclatura ?? "", cancellationToken);
                    if (existing != null)
                    {
                        skipped++;
                        continue;
                    }

                    var opportunity = new Opportunity
                    {
                        ProcessCode = record.Nomenclatura ?? "",
                        Title = record.Nomenclatura ?? "",
                        EntityName = record.Entidad ?? "",
                        PublishedDate = ParseSeaceDate(record.FechaPublicacion),
                        Category = record.ObjetoContratacion ?? "",
                        Summary = record.DescripcionObjeto ?? "",
                        Modality = "",
                        EstimatedAmount = 0,
                        ClosingDate = DateTime.Now.AddDays(30),
                        MatchScore = 50,
                        Location = "Lima",
                        IsPriority = false
                    };

                    await _repository.InsertOpportunityAsync(opportunity, cancellationToken);
                    imported++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Error en registro {record.Nomenclatura}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            errors.Add($"Error general: {ex.Message}");
        }

        return (imported, skipped, errors);
    }

    private DateTime? ParseSeaceDate(string? dateStr)
    {
        if (string.IsNullOrWhiteSpace(dateStr))
            return null;

        // Formato: "23/04/2026 11:04"
        if (DateTime.TryParseExact(dateStr.Trim(), "dd/MM/yyyy HH:mm", 
            CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            return date;
        }

        return null;
    }
}

public class SeaceCsvRecord
{
    public string? Nro { get; set; }
    public string? Entidad { get; set; }
    public string? FechaPublicacion { get; set; }
    public string? Nomenclatura { get; set; }
    public string? ReiniciadoDesde { get; set; }
    public string? ObjetoContratacion { get; set; }
    public string? DescripcionObjeto { get; set; }
    public string? CodigoSnip { get; set; }
    public string? CodigoUnicoInversion { get; set; }
    public string? CuantiaContratacion { get; set; }
    public string? Moneda { get; set; }
    public string? VersionSeace { get; set; }
}
