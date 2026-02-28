using DiarioIntelligente.Core.DTOs;
using DiarioIntelligente.Core.Interfaces;
using DiarioIntelligente.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace DiarioIntelligente.Infrastructure.Services;

public sealed class PersonalModelService : IPersonalModelService
{
    private static readonly (string Trait, string[] Keywords, string Rationale)[] TraitRules =
    {
        ("family_oriented", new[] { "famiglia", "figlia", "figlie", "madre", "padre", "fratello", "sorella", "mamma", "papa" }, "Forte attenzione alle relazioni familiari."),
        ("builder_execution", new[] { "progetto", "prodotto", "cliente", "business", "deploy", "roadmap", "lancio", "feature", "vendita" }, "Orientamento a costruzione e risultati concreti."),
        ("growth_learning", new[] { "imparo", "studio", "leggo", "crescere", "migliorare", "alleno", "disciplina" }, "Ricerca attiva di miglioramento personale."),
        ("analytical_reflection", new[] { "analisi", "pattern", "metrica", "dato", "debug", "cause", "correlazione" }, "Approccio analitico ai problemi e ai pattern."),
        ("resilient_adaptation", new[] { "problema", "difficile", "blocco", "riparto", "adatto", "risolto", "stress" }, "Capacita di adattamento sotto vincoli reali.")
    };

    private static readonly (string Theme, string[] Keywords)[] PhilosophyRules =
    {
        ("responsabilita_personale", new[] { "decido", "scelgo", "responsabilita", "mi assumo", "devo" }),
        ("famiglia_al_centro", new[] { "famiglia", "figlie", "madre", "fratello", "casa" }),
        ("pragmatismo_operativo", new[] { "azione", "passo", "micro", "priorita", "focus", "concreto" }),
        ("crescita_continua", new[] { "migliorare", "imparo", "crescere", "evolvere", "alleno" })
    };

    private readonly AppDbContext _db;

    public PersonalModelService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<PersonalModelResponse> BuildAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var entries = await _db.Entries
            .Where(x => x.UserId == userId)
            .OrderByDescending(x => x.CreatedAt)
            .Take(240)
            .Select(x => new { x.Id, x.Content, x.CreatedAt })
            .ToListAsync(cancellationToken);

        var canonicalEntities = await _db.CanonicalEntities
            .Where(x => x.UserId == userId)
            .Include(x => x.Aliases)
            .ToListAsync(cancellationToken);

        var activeGoals = await _db.GoalItems
            .Where(x => x.UserId == userId && x.Status == "active")
            .Include(x => x.SubGoals)
            .ToListAsync(cancellationToken);

        var openSettlements = await _db.Settlements
            .Where(x => x.UserId == userId && x.Status != "settled")
            .Include(x => x.CounterpartyEntity)
            .ToListAsync(cancellationToken);

        var aggregateText = string.Join(" ", entries.Select(x => x.Content)).ToLowerInvariant();

        var personalitySignals = TraitRules
            .Select(rule => new ProfileSignalResponse(
                rule.Trait,
                CountKeywordHits(aggregateText, rule.Keywords),
                rule.Rationale))
            .Where(signal => signal.Score > 0)
            .OrderByDescending(signal => signal.Score)
            .Take(5)
            .ToList();

        var philosophicalThemes = PhilosophyRules
            .Select(rule => new
            {
                rule.Theme,
                Score = CountKeywordHits(aggregateText, rule.Keywords)
            })
            .Where(x => x.Score > 0)
            .OrderByDescending(x => x.Score)
            .Select(x => x.Theme)
            .Take(4)
            .ToList();

        var topPeople = canonicalEntities
            .Where(x => x.Kind == "person")
            .OrderByDescending(x => x.UpdatedAt)
            .Take(6)
            .Select(x => x.CanonicalName)
            .ToList();

        var currentFocus = new List<string>();
        currentFocus.AddRange(activeGoals.Take(3).Select(goal => $"Goal attivo: {goal.Title}"));
        currentFocus.AddRange(openSettlements
            .Take(3)
            .Select(settlement =>
                settlement.Direction == "user_owes"
                    ? $"Debito aperto verso {settlement.CounterpartyEntity.CanonicalName}: {settlement.RemainingAmount} {settlement.Currency}"
                    : $"Credito aperto verso {settlement.CounterpartyEntity.CanonicalName}: {settlement.RemainingAmount} {settlement.Currency}"));

        if (currentFocus.Count == 0 && topPeople.Count > 0)
            currentFocus.Add($"Relazioni recenti centrali: {string.Join(", ", topPeople.Take(3))}");

        var recentEntriesCount = entries.Count(item => item.CreatedAt >= DateTime.UtcNow.AddDays(-2));
        var suggestedMicroSteps = BuildMicroSteps(activeGoals, openSettlements, recentEntriesCount, topPeople);

        var adaptationRules = BuildAdaptationRules(personalitySignals, philosophicalThemes);

        var contextSummary = BuildCompactContext(entries.Count, canonicalEntities.Count, activeGoals.Count, openSettlements.Count, topPeople);

        return new PersonalModelResponse(
            DateTime.UtcNow,
            entries.Count,
            canonicalEntities.Count,
            activeGoals.Count,
            contextSummary,
            personalitySignals,
            philosophicalThemes,
            currentFocus,
            suggestedMicroSteps,
            adaptationRules);
    }

    private static List<string> BuildMicroSteps(
        List<Core.Models.GoalItem> activeGoals,
        List<Core.Models.Settlement> openSettlements,
        int recentEntriesCount,
        List<string> topPeople)
    {
        var steps = new List<string>();

        var goalsWithoutSubSteps = activeGoals.Where(goal => goal.SubGoals.Count == 0).Take(2).ToList();
        foreach (var goal in goalsWithoutSubSteps)
            steps.Add($"Scomponi '{goal.Title}' in 2 micro-step da 15 minuti.");

        foreach (var settlement in openSettlements.Take(2))
            steps.Add($"Registra il prossimo pagamento per chiudere il saldo con {settlement.CounterpartyEntity.CanonicalName}.");

        if (recentEntriesCount < 2)
            steps.Add("Scrivi un recap rapido di oggi (3 righe: fatto, energia, prossimo passo).");

        if (topPeople.Count > 0)
            steps.Add($"Conferma/correggi un fatto chiave sul nodo '{topPeople[0]}' per rafforzare il grafo.");

        if (steps.Count == 0)
            steps.Add("Scegli il prossimo step piu piccolo e verificabile che sblocca un obiettivo attivo.");

        return steps.Take(5).ToList();
    }

    private static List<string> BuildAdaptationRules(
        List<ProfileSignalResponse> personalitySignals,
        List<string> philosophicalThemes)
    {
        var rules = new List<string>
        {
            "Prioritizza sempre pochi step ad alto impatto invece di liste lunghe.",
            "Usa evidenze esplicite (entry + snippet) prima di inferenze."
        };

        if (personalitySignals.Any(signal => signal.Trait == "family_oriented"))
            rules.Add("Quando c'e conflitto tra lavoro e famiglia, proponi step compatti e realistici.");

        if (personalitySignals.Any(signal => signal.Trait == "analytical_reflection"))
            rules.Add("Mostra motivazione delle scelte con dati e pattern osservati.");

        if (philosophicalThemes.Contains("pragmatismo_operativo"))
            rules.Add("Evita consigli astratti: fornisci sempre un'azione verificabile.");

        return rules.Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList();
    }

    private static int CountKeywordHits(string content, IEnumerable<string> keywords)
    {
        var total = 0;
        foreach (var keyword in keywords)
        {
            var search = keyword.ToLowerInvariant();
            var index = 0;
            while (index >= 0)
            {
                index = content.IndexOf(search, index, StringComparison.Ordinal);
                if (index < 0)
                    break;

                total++;
                index += search.Length;
            }
        }

        return total;
    }

    private static string BuildCompactContext(
        int entriesCount,
        int entityCount,
        int activeGoalsCount,
        int openSettlementsCount,
        List<string> topPeople)
    {
        var peopleSummary = topPeople.Count > 0
            ? $"Persone centrali recenti: {string.Join(", ", topPeople.Take(3))}."
            : "Nessuna persona centrale ancora consolidata.";

        return
            $"Memoria utente: {entriesCount} entry analizzate, {entityCount} nodi canonici, {activeGoalsCount} goal attivi, {openSettlementsCount} settlement aperti. " +
            peopleSummary;
    }
}
