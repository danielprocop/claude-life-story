using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using DiarioIntelligente.Core.DTOs;

namespace DiarioIntelligente.API.Services;

public static class EntryAnalysisSanitizer
{
    private static readonly Regex TimeTokenRegex =
        new(@"^(?:[01]?\d|2[0-3])[:.][0-5]\d$", RegexOptions.Compiled);
    private static readonly Regex DateTokenRegex =
        new(@"^\d{1,2}[\/\-]\d{1,2}(?:[\/\-]\d{2,4})?$", RegexOptions.Compiled);
    private static readonly Regex DigitsOnlyRegex =
        new(@"^\d+$", RegexOptions.Compiled);
    private static readonly Regex AmountInTextRegex =
        new(@"(?<amount>\d+(?:[.,]\d+)?)\s*(?:€|euro|eur)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex TimeInTextRegex =
        new(@"\b(?:[01]?\d|2[0-3])[:.][0-5]\d\b", RegexOptions.Compiled);
    private static readonly Regex DateInTextRegex =
        new(@"\b\d{1,2}[\/\-]\d{1,2}(?:[\/\-]\d{2,4})?\b", RegexOptions.Compiled);
    private static readonly Regex YearInTextRegex =
        new(@"\b(?:19|20)\d{2}\b", RegexOptions.Compiled);
    private static readonly Regex BrandModelLabelRegex =
        new(@"^(?<brand>[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]{1,})\s+(?<model>[A-Za-z]{1,5}\s?\d{1,3}[A-Za-z]?)$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex BrandModelInTextRegex =
        new(@"\b(?<brand>[A-Za-zÀ-ÿ][A-Za-zÀ-ÿ'’\-]{1,})\s+(?<model>[A-Za-z]{1,5}\s?\d{1,3}[A-Za-z]?)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);
    private static readonly Regex CompactModelRegex =
        new(@"^[A-Za-z]{1,4}\s?\d{1,3}[A-Za-z]?$", RegexOptions.Compiled);
    private static readonly Regex CompactModelInTextRegex =
        new(@"\b[A-Za-z]{1,4}\s?\d{1,3}[A-Za-z]?\b", RegexOptions.Compiled);
    private static readonly Regex VehicleWordRegex =
        new(@"\b(auto|macchina|automobile|car|veicolo|suv|berlina|utilitaria|furgone|pickup)\b", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly HashSet<string> NonPersonStopTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "alle",
        "alla",
        "oggi",
        "ieri",
        "domani",
        "sono",
        "siamo",
        "abbiamo",
        "ho",
        "poi",
        "con",
        "mia",
        "mio",
        "le",
        "la",
        "il",
        "un",
        "una",
        "devo",
        "mi",
        "ha",
        "hanno"
    };

    private static readonly string[] SportsContextHints =
    {
        "giocher",
        "partita",
        "match",
        "campionato",
        "serie a",
        "champions",
        "derby",
        "gol",
        "allenatore",
        "squadra",
        "club",
        "vs",
        "contro"
    };

    private static readonly string[] VehicleContextHints =
    {
        "auto",
        "macchina",
        "automobile",
        "veicolo",
        "modello",
        "motore",
        "benzina",
        "diesel",
        "gpl",
        "elettrica",
        "elettrico",
        "ibrida",
        "ibrido",
        "chilometri",
        "km",
        "targa",
        "tagliando",
        "assicurazione",
        "garage",
        "immatricolazione"
    };

    private static readonly HashSet<string> KnownTeamTokens = new(StringComparer.OrdinalIgnoreCase)
    {
        "milan",
        "inter",
        "juventus",
        "roma",
        "lazio",
        "napoli",
        "atalanta",
        "fiorentina",
        "torino",
        "bologna",
        "genoa",
        "verona",
        "parma",
        "monza",
        "como",
        "udinese",
        "cagliari",
        "lecce",
        "empoli",
        "arsenal",
        "barcelona",
        "realmadrid"
    };

    private static readonly HashSet<string> VehicleWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "auto",
        "macchina",
        "automobile",
        "car",
        "veicolo",
        "suv",
        "berlina",
        "utilitaria",
        "furgone",
        "pickup"
    };

    private static readonly HashSet<string> ObjectWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "telefono",
        "smartphone",
        "laptop",
        "computer",
        "pc",
        "chiavi",
        "chiave",
        "zaino",
        "borsa"
    };

    private static readonly HashSet<string> KnownVehicleBrands = new(StringComparer.OrdinalIgnoreCase)
    {
        "citroen",
        "fiat",
        "alfa",
        "alfaromeo",
        "lancia",
        "ford",
        "bmw",
        "audi",
        "mercedes",
        "volkswagen",
        "vw",
        "renault",
        "peugeot",
        "opel",
        "toyota",
        "nissan",
        "honda",
        "kia",
        "hyundai",
        "mazda",
        "volvo",
        "tesla",
        "porsche",
        "ferrari",
        "lamborghini"
    };

    public static Task<AiAnalysisResult> SanitizeAsync(
        string content,
        AiAnalysisResult analysis,
        CancellationToken ct = default)
        => Task.FromResult(Sanitize(content, analysis));

    public static AiAnalysisResult Sanitize(
        string content,
        AiAnalysisResult analysis,
        IReadOnlyDictionary<string, string>? kindOverridesByName = null)
    {
        analysis ??= new AiAnalysisResult();
        analysis.Concepts ??= new List<ExtractedConcept>();
        analysis.GoalSignals ??= new List<GoalSignal>();

        var overrides = NormalizeOverrides(kindOverridesByName);
        var sourceConcepts = analysis.Concepts
            .Where(concept => !string.IsNullOrWhiteSpace(concept.Label))
            .Select(concept => new ExtractedConcept
            {
                Label = concept.Label.Trim(),
                Type = concept.Type?.Trim() ?? string.Empty
            })
            .ToList();

        sourceConcepts.AddRange(ExtractDeterministicConcepts(content));

        var classified = sourceConcepts
            .Select(concept => ClassifyCandidate(content, concept.Label, concept.Type))
            .Where(candidate => !string.IsNullOrWhiteSpace(candidate.NormalizedLabel))
            .ToList();

        var labelsWithStrongNonPerson = classified
            .Where(candidate => candidate.Type != "person" && candidate.Type != "not_entity")
            .Select(candidate => candidate.NormalizedLabel)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var sanitized = new List<ExtractedConcept>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var candidate in classified)
        {
            var finalType = candidate.Type;

            if (finalType == "person" && labelsWithStrongNonPerson.Contains(candidate.NormalizedLabel))
                finalType = "not_entity";

            if (overrides.TryGetValue(candidate.NormalizedLabel, out var overrideKind))
                finalType = ApplyOverride(finalType, overrideKind);

            if (string.IsNullOrWhiteSpace(finalType) || finalType == "not_entity")
                continue;

            var key = $"{candidate.NormalizedLabel}:{finalType}";
            if (!seen.Add(key))
                continue;

            sanitized.Add(new ExtractedConcept
            {
                Label = candidate.Label,
                Type = finalType
            });
        }

        analysis.Concepts = sanitized;
        analysis.GoalSignals = NormalizeGoalSignals(analysis.GoalSignals);
        return analysis;
    }

    private static Candidate ClassifyCandidate(string content, string label, string rawType)
    {
        var normalizedLabel = NormalizeToken(label);
        if (string.IsNullOrWhiteSpace(normalizedLabel))
            return new Candidate(label, normalizedLabel, "not_entity");

        var normalizedType = NormalizeConceptType(rawType);

        if (TryInferDeterministicType(label, normalizedLabel, content, out var inferredType))
            normalizedType = inferredType;

        if (normalizedType == "person")
        {
            if (NonPersonStopTokens.Contains(normalizedLabel))
                normalizedType = "not_entity";
            else if (TimeTokenRegex.IsMatch(label.Trim()) || DigitsOnlyRegex.IsMatch(normalizedLabel))
                normalizedType = "not_entity";
            else if (IsLikelyTeamMention(content, label, normalizedLabel))
                normalizedType = "team";
        }

        if (normalizedType == "organization" && IsVehicleBrandLabel(label, content))
            normalizedType = "brand";

        if ((normalizedType == "organization" || normalizedType == "brand") && IsVehicleModelLabel(label, content))
            normalizedType = "product_model";

        if (normalizedType == "problem" && IsYearLabel(label) && !HasCurrencyContext(content, label))
            normalizedType = "year";

        return new Candidate(label, normalizedLabel, normalizedType);
    }

    private static bool TryInferDeterministicType(string rawLabel, string normalizedLabel, string content, out string type)
    {
        if (IsAmountLabel(rawLabel, content))
        {
            type = "amount";
            return true;
        }

        if (IsTimeLabel(rawLabel))
        {
            type = "time";
            return true;
        }

        if (IsDateLabel(rawLabel))
        {
            type = "date";
            return true;
        }

        if (IsYearLabel(rawLabel) && !HasCurrencyContext(content, rawLabel))
        {
            type = "year";
            return true;
        }

        if (IsVehicleModelLabel(rawLabel, content))
        {
            type = "product_model";
            return true;
        }

        if (IsVehicleBrandLabel(rawLabel, content))
        {
            type = "brand";
            return true;
        }

        if (VehicleWords.Contains(normalizedLabel))
        {
            type = "vehicle";
            return true;
        }

        if (ObjectWords.Contains(normalizedLabel))
        {
            type = "object";
            return true;
        }

        if (DigitsOnlyRegex.IsMatch(normalizedLabel))
        {
            type = "not_entity";
            return true;
        }

        type = string.Empty;
        return false;
    }

    private static List<GoalSignal> NormalizeGoalSignals(List<GoalSignal> signals)
    {
        var normalizedSignals = new List<GoalSignal>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var signal in signals.Where(item => !string.IsNullOrWhiteSpace(item.Text)))
        {
            var text = signal.Text.Trim();
            if (text.Length == 0)
                continue;

            var type = NormalizeGoalSignalType(signal.Type);
            var key = $"{NormalizeToken(text)}:{type}";
            if (!seen.Add(key))
                continue;

            normalizedSignals.Add(new GoalSignal
            {
                Text = text,
                Type = type
            });
        }

        return normalizedSignals;
    }

    private static List<ExtractedConcept> ExtractDeterministicConcepts(string content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return new List<ExtractedConcept>();

        var extracted = new List<ExtractedConcept>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void Add(string label, string type)
        {
            var cleanLabel = label.Trim();
            if (string.IsNullOrWhiteSpace(cleanLabel))
                return;

            var key = $"{NormalizeToken(cleanLabel)}:{type}";
            if (!seen.Add(key))
                return;

            extracted.Add(new ExtractedConcept
            {
                Label = cleanLabel,
                Type = type
            });
        }

        foreach (Match match in TimeInTextRegex.Matches(content))
            Add(match.Value, "time");

        foreach (Match match in DateInTextRegex.Matches(content))
            Add(match.Value, "date");

        foreach (Match match in AmountInTextRegex.Matches(content))
            Add(match.Value, "amount");

        foreach (Match match in YearInTextRegex.Matches(content))
        {
            if (IsYearLabel(match.Value))
                Add(match.Value, "year");
        }

        foreach (Match match in VehicleWordRegex.Matches(content))
            Add(match.Value, "vehicle");

        foreach (Match match in BrandModelInTextRegex.Matches(content))
        {
            var brand = match.Groups["brand"].Value.Trim();
            var model = match.Groups["model"].Value.Trim();
            if (string.IsNullOrWhiteSpace(brand) || string.IsNullOrWhiteSpace(model))
                continue;

            var fullModel = $"{brand} {model}".Trim();
            if (IsVehicleModelLabel(fullModel, content))
                Add(fullModel, "product_model");

            if (IsVehicleBrandLabel(brand, content))
                Add(brand, "brand");
        }

        foreach (Match match in CompactModelInTextRegex.Matches(content))
        {
            var token = match.Value.Trim();
            if (IsVehicleModelLabel(token, content))
                Add(token, "product_model");
        }

        return extracted;
    }

    private static string ApplyOverride(string currentType, string overrideKind)
    {
        if (overrideKind == "not_entity")
            return "not_entity";

        if (overrideKind == "not_person")
            return currentType == "person" ? "not_entity" : currentType;

        return overrideKind;
    }

    private static Dictionary<string, string> NormalizeOverrides(IReadOnlyDictionary<string, string>? raw)
    {
        var normalized = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (raw == null)
            return normalized;

        foreach (var item in raw)
        {
            var key = NormalizeToken(item.Key);
            if (string.IsNullOrWhiteSpace(key))
                continue;

            var value = NormalizeOverrideKind(item.Value);
            if (string.IsNullOrWhiteSpace(value))
                continue;

            normalized[key] = value;
        }

        return normalized;
    }

    private static string NormalizeConceptType(string rawType)
    {
        var normalized = NormalizeToken(rawType);
        if (string.IsNullOrWhiteSpace(normalized))
            return "generic";

        return normalized switch
        {
            "person" or "persona" => "person",
            "place" or "luogo" or "location" or "city" or "country" or "citta" => "place",
            "team" or "squadra" or "club" => "team",
            "organization" or "organizzazione" or "company" or "azienda" or "societa" => "organization",
            "project" or "progetto" => "project",
            "activity" or "attivita" or "task" or "habit" or "abitudine" => "activity",
            "emotion" or "emozione" or "feeling" => "emotion",
            "idea" or "belief" or "philosophy" or "decision" or "decisione" => "idea",
            "problem" or "problema" or "blocker" => "problem",
            "finance" or "finanza" or "money" => "finance",
            "goal" or "obiettivo" or "objective" => "goal",
            "desire" or "desiderio" => "desire",
            "progress" or "progresso" => "progress",
            "object" or "oggetto" or "item" or "thing" => "object",
            "vehicle" or "veicolo" or "car" or "auto" or "macchina" or "automobile" => "vehicle",
            "brand" or "marca" => "brand",
            "productmodel" or "model" or "modello" => "product_model",
            "year" or "anno" => "year",
            "date" or "data" => "date",
            "time" or "ora" or "orario" => "time",
            "amount" or "importo" or "prezzo" or "costo" => "amount",
            "notentity" or "nonentity" => "not_entity",
            "notperson" or "nonperson" => "not_person",
            _ => normalized
        };
    }

    private static string NormalizeGoalSignalType(string rawType)
    {
        var normalized = NormalizeToken(rawType);
        return normalized switch
        {
            "desire" or "desiderio" => "desire",
            "progress" or "progresso" or "step" => "progress",
            "goal" or "obiettivo" or "objective" => "goal",
            _ => "goal"
        };
    }

    private static string NormalizeOverrideKind(string rawValue)
    {
        var normalized = NormalizeConceptType(rawValue);
        return normalized switch
        {
            "not_person" => "not_person",
            "not_entity" => "not_entity",
            _ => normalized
        };
    }

    private static bool IsLikelyTeamMention(string content, string rawLabel, string normalizedLabel)
    {
        var lower = content.ToLowerInvariant();
        var hasSportsContext = SportsContextHints.Any(lower.Contains);
        if (!hasSportsContext)
            return false;

        if (KnownTeamTokens.Contains(normalizedLabel))
            return true;

        var escaped = Regex.Escape(rawLabel.Trim());
        return Regex.IsMatch(content, $@"\b(?:il|lo|la|i|gli|le|l')\s*{escaped}\b", RegexOptions.IgnoreCase);
    }

    private static bool IsVehicleContext(string content)
    {
        var lower = content.ToLowerInvariant();
        return VehicleContextHints.Any(lower.Contains);
    }

    private static bool IsVehicleBrandLabel(string rawLabel, string content)
    {
        var normalizedLabel = NormalizeToken(rawLabel);
        if (!KnownVehicleBrands.Contains(normalizedLabel))
            return false;

        if (IsVehicleContext(content))
            return true;

        var escaped = Regex.Escape(rawLabel.Trim());
        return Regex.IsMatch(content, $@"\b{escaped}\s+[A-Za-z]{1,5}\s?\d{{1,3}}[A-Za-z]?\b", RegexOptions.IgnoreCase);
    }

    private static bool IsVehicleModelLabel(string rawLabel, string content)
    {
        var label = rawLabel.Trim();
        if (string.IsNullOrWhiteSpace(label))
            return false;

        var brandModelMatch = BrandModelLabelRegex.Match(label);
        if (brandModelMatch.Success)
        {
            var brand = NormalizeToken(brandModelMatch.Groups["brand"].Value);
            if (KnownVehicleBrands.Contains(brand) || IsVehicleContext(content))
                return true;
        }

        return CompactModelRegex.IsMatch(label) && IsVehicleContext(content);
    }

    private static bool IsAmountLabel(string rawLabel, string content)
    {
        var label = rawLabel.Trim();
        if (label.Contains('€', StringComparison.Ordinal))
            return true;

        if (!decimal.TryParse(label.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out _))
            return false;

        return HasCurrencyContext(content, label);
    }

    private static bool HasCurrencyContext(string content, string rawLabel)
    {
        var escaped = Regex.Escape(rawLabel.Trim());
        return Regex.IsMatch(
            content,
            $@"(?:€\s*{escaped}|{escaped}\s*(?:€|euro|eur)\b)",
            RegexOptions.IgnoreCase);
    }

    private static bool IsTimeLabel(string rawLabel) => TimeTokenRegex.IsMatch(rawLabel.Trim());
    private static bool IsDateLabel(string rawLabel) => DateTokenRegex.IsMatch(rawLabel.Trim());

    private static bool IsYearLabel(string rawLabel)
    {
        var value = rawLabel.Trim();
        if (!DigitsOnlyRegex.IsMatch(value))
            return false;
        if (!int.TryParse(value, out var year))
            return false;

        return year is >= 1900 and <= 2100;
    }

    private static string NormalizeToken(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            return string.Empty;

        var normalized = input.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder();
        foreach (var c in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(c);
            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            if (char.IsLetterOrDigit(c))
                builder.Append(char.ToLowerInvariant(c));
        }

        return builder.ToString();
    }

    private sealed record Candidate(string Label, string NormalizedLabel, string Type);
}
