using DiarioIntelligente.API.Services;
using DiarioIntelligente.Core.DTOs;
using Xunit;

namespace DiarioIntelligente.Tests;

public class EntryAnalysisSanitizerTests
{
    [Fact]
    public void Reclassifies_Vehicle_Model_Year_And_Time_From_Bad_AI_Types()
    {
        var analysis = new AiAnalysisResult
        {
            Concepts =
            [
                new ExtractedConcept { Label = "auto", Type = "person" },
                new ExtractedConcept { Label = "Citroen DS 5", Type = "organization" },
                new ExtractedConcept { Label = "2012", Type = "problem" },
                new ExtractedConcept { Label = "12:30", Type = "person" }
            ]
        };

        var sanitized = EntryAnalysisSanitizer.Sanitize(
            "La mia auto Citroen DS 5 del 2012 alle 12:30 e dal meccanico.",
            analysis);

        Assert.Contains(sanitized.Concepts, concept => concept.Label == "auto" && concept.Type == "vehicle");
        Assert.Contains(sanitized.Concepts, concept => concept.Label == "Citroen DS 5" && concept.Type == "product_model");
        Assert.Contains(sanitized.Concepts, concept => concept.Label == "2012" && concept.Type == "year");
        Assert.Contains(sanitized.Concepts, concept => concept.Label == "12:30" && concept.Type == "time");

        Assert.DoesNotContain(sanitized.Concepts, concept => concept.Label == "auto" && concept.Type == "person");
        Assert.DoesNotContain(sanitized.Concepts, concept => concept.Label == "Citroen DS 5" && concept.Type == "organization");
        Assert.DoesNotContain(sanitized.Concepts, concept => concept.Label == "2012" && concept.Type == "problem");
    }

    [Fact]
    public void Applies_Override_NotEntity_To_Remove_Concept()
    {
        var analysis = new AiAnalysisResult
        {
            Concepts =
            [
                new ExtractedConcept { Label = "auto", Type = "vehicle" }
            ]
        };

        var sanitized = EntryAnalysisSanitizer.Sanitize(
            "La mia auto e dal meccanico.",
            analysis,
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["auto"] = "not_entity"
            });

        Assert.DoesNotContain(sanitized.Concepts, concept => concept.Label == "auto");
    }

    [Fact]
    public void Adds_Deterministic_Concepts_From_Content_When_AI_Misses_Them()
    {
        var analysis = new AiAnalysisResult();

        var sanitized = EntryAnalysisSanitizer.Sanitize(
            "Ho comprato una Citroen DS 5 del 2012 alle 12:30 per 8500 euro.",
            analysis);

        Assert.Contains(sanitized.Concepts, concept => concept.Label == "Citroen DS 5" && concept.Type == "product_model");
        Assert.Contains(sanitized.Concepts, concept => concept.Label == "2012" && concept.Type == "year");
        Assert.Contains(sanitized.Concepts, concept => concept.Label == "12:30" && concept.Type == "time");
        Assert.Contains(sanitized.Concepts, concept => concept.Label.Contains("8500", StringComparison.OrdinalIgnoreCase) && concept.Type == "amount");
    }
}
