using System.ComponentModel;
using System.Text;
using System.Globalization;
using System.Text.RegularExpressions;
using AIAgentsBackend.Models.VectorStore;
using AIAgentsBackend.Services.VectorStore.Interfaces;
using Microsoft.Extensions.VectorData;

namespace AIAgentsBackend.Agents.Tools;

/// <summary>
/// Tools for the Message Formulator Agent to search seller requirements for message composition.
/// </summary>
public class MessageFormulatorTools
{
    private readonly IServiceProvider serviceProvider;
    private const string DisableSellerRequirementsToolKey = "DisableSellerRequirementsTool";

    public MessageFormulatorTools(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider;
    }

    #region Seller Requirements Tool

    /// <summary>
    /// Searches the seller requirements knowledge base to find what documents or information
    /// the seller will typically ask from the customer based on the type of problem.
    /// Use this when you understand the customer's problem and need to know what the seller will require.
    /// </summary>
    /// <param name="problemDescription">Description of the customer's problem (e.g., "broken screen", "product not working", "wrong item received")</param>
    /// <returns>List of requirements the seller will likely ask for</returns>
    [Description("Recherche les documents et informations que le vendeur demandera au client selon le type de problème. Utilise cet outil pour savoir ce que le vendeur va demander (photos, numéro de commande, etc.) afin de l'inclure dans les exigences.")]
    public async Task<string> SearchSellerRequirementsAsync(
        [Description("Description du problème du client (ex: 'écran cassé', 'produit ne fonctionne pas', 'mauvais produit reçu')")] string problemDescription)
    {
        using var scope = serviceProvider.CreateScope();
        // If the backend marks this as a follow-up message, do not provide seller knowledge.
        var http = scope.ServiceProvider.GetService<Microsoft.AspNetCore.Http.IHttpContextAccessor>();
        if (http?.HttpContext?.Items.TryGetValue(DisableSellerRequirementsToolKey, out var disabledObj) == true &&
            disabledObj is bool disabled && disabled)
        {
            return string.Empty;
        }
        var service = scope.ServiceProvider.GetRequiredService<ISellerRequirementsVectorStoreService>();
        
        // Fetch more candidates and then select the right typology.
        // This avoids cases where vector similarity returns the wrong section
        // (e.g., "dégâts" for a "mauvais produit reçu" scenario).
        var results = await service.SearchAsync(problemDescription, topK: 6);
        
        if (results.Count == 0)
        {
            return "Aucune exigence spécifique trouvée pour ce type de problème.";
        }

        var typology = ResolveTypology(problemDescription);
        var filtered = FilterByTypology(results, typology);

        return FormatRequirements(filtered);
    }

    private enum SellerProblemTypology
    {
        Unknown,
        WrongItem,
        Damaged,
        Malfunction,
        MissingParts,
        Delivery,
        Tracking,
        Quality,
        Warranty,
        Size,
        Refund
    }

    private static SellerProblemTypology ResolveTypology(string input)
    {
        var t = NormalizeKey(input);
        if (string.IsNullOrWhiteSpace(t)) return SellerProblemTypology.Unknown;

        // Wrong item / wrong model (priority)
        if (t.Contains("mauvais", StringComparison.Ordinal) ||
            t.Contains("pas le bon", StringComparison.Ordinal) ||
            t.Contains("pas la bonne", StringComparison.Ordinal) ||
            t.Contains("a la place", StringComparison.Ordinal) ||
            t.Contains("erreur de modele", StringComparison.Ordinal) ||
            t.Contains("produit different", StringComparison.Ordinal) ||
            t.Contains("recu le model", StringComparison.Ordinal) ||
            t.Contains("recu le modele", StringComparison.Ordinal))
        {
            return SellerProblemTypology.WrongItem;
        }

        // Damage
        if (t.Contains("casse", StringComparison.Ordinal) ||
            t.Contains("endommag", StringComparison.Ordinal) ||
            t.Contains("degat", StringComparison.Ordinal) ||
            t.Contains("fissur", StringComparison.Ordinal) ||
            t.Contains("abime", StringComparison.Ordinal) ||
            t.Contains("brise", StringComparison.Ordinal))
        {
            return SellerProblemTypology.Damaged;
        }

        // Malfunction
        if (t.Contains("ne s allume", StringComparison.Ordinal) ||
            t.Contains("ne s allume pas", StringComparison.Ordinal) ||
            t.Contains("ne fonctionne", StringComparison.Ordinal) ||
            t.Contains("ne marche", StringComparison.Ordinal) ||
            t.Contains("panne", StringComparison.Ordinal) ||
            t.Contains("defectu", StringComparison.Ordinal))
        {
            return SellerProblemTypology.Malfunction;
        }

        // Missing parts
        if (t.Contains("manqu", StringComparison.Ordinal) ||
            t.Contains("incomplet", StringComparison.Ordinal) ||
            t.Contains("accessoire", StringComparison.Ordinal) ||
            t.Contains("piece", StringComparison.Ordinal))
        {
            return SellerProblemTypology.MissingParts;
        }

        // Delivery / tracking
        if (t.Contains("suivi", StringComparison.Ordinal) || t.Contains("tracking", StringComparison.Ordinal))
        {
            return SellerProblemTypology.Tracking;
        }
        if (t.Contains("pas recu", StringComparison.Ordinal) ||
            t.Contains("non recu", StringComparison.Ordinal) ||
            t.Contains("livraison", StringComparison.Ordinal) ||
            t.Contains("colis", StringComparison.Ordinal) ||
            t.Contains("perdu", StringComparison.Ordinal))
        {
            return SellerProblemTypology.Delivery;
        }

        if (t.Contains("garantie", StringComparison.Ordinal))
        {
            return SellerProblemTypology.Warranty;
        }

        if (t.Contains("taille", StringComparison.Ordinal) ||
            t.Contains("trop petit", StringComparison.Ordinal) ||
            t.Contains("trop grand", StringComparison.Ordinal))
        {
            return SellerProblemTypology.Size;
        }

        if (t.Contains("rembours", StringComparison.Ordinal))
        {
            return SellerProblemTypology.Refund;
        }

        if (t.Contains("qualite", StringComparison.Ordinal) ||
            t.Contains("non conforme", StringComparison.Ordinal) ||
            t.Contains("description", StringComparison.Ordinal))
        {
            return SellerProblemTypology.Quality;
        }

        return SellerProblemTypology.Unknown;
    }

    private static IReadOnlyList<VectorSearchResult<PolicySectionRecord>> FilterByTypology(
        IReadOnlyList<VectorSearchResult<PolicySectionRecord>> results,
        SellerProblemTypology typology)
    {
        if (typology == SellerProblemTypology.Unknown) return results;

        bool Match(VectorSearchResult<PolicySectionRecord> r, params string[] hints)
        {
            var sid = NormalizeKey(r.Record.SectionId);
            var title = NormalizeKey(r.Record.Title);
            return hints.Any(h =>
                sid.Contains(NormalizeKey(h), StringComparison.Ordinal) ||
                title.Contains(NormalizeKey(h), StringComparison.Ordinal));
        }

        IEnumerable<VectorSearchResult<PolicySectionRecord>> preferred = results;

        switch (typology)
        {
            case SellerProblemTypology.WrongItem:
                preferred = results.Where(r => Match(r, "wrong", "different", "produit recu different"));
                // If no direct match, exclude damage sections (common false positive)
                if (!preferred.Any())
                {
                    preferred = results.Where(r => !Match(r, "damaged", "endommag", "casse", "degat"));
                }
                break;
            case SellerProblemTypology.Damaged:
                preferred = results.Where(r => Match(r, "damaged", "endommag", "casse", "degat"));
                break;
            case SellerProblemTypology.Malfunction:
                preferred = results.Where(r => Match(r, "defective", "malfunction", "defectu", "ne fonctionne"));
                break;
            case SellerProblemTypology.MissingParts:
                preferred = results.Where(r => Match(r, "missing", "incomplet", "manqu"));
                break;
            case SellerProblemTypology.Tracking:
                preferred = results.Where(r => Match(r, "tracking", "suivi"));
                break;
            case SellerProblemTypology.Delivery:
                preferred = results.Where(r => Match(r, "delivery", "livraison", "colis"));
                break;
            case SellerProblemTypology.Quality:
                preferred = results.Where(r => Match(r, "quality", "qualite", "conforme", "description"));
                break;
            case SellerProblemTypology.Warranty:
                preferred = results.Where(r => Match(r, "warranty", "garantie"));
                break;
            case SellerProblemTypology.Size:
                preferred = results.Where(r => Match(r, "size", "taille"));
                break;
            case SellerProblemTypology.Refund:
                preferred = results.Where(r => Match(r, "refund", "rembours"));
                break;
        }

        var preferredList = preferred.ToList();
        if (preferredList.Count == 0) return results;

        // Keep preferred first, then the rest (formatter will stop early anyway)
        var rest = results.Where(r => preferredList.All(p => p.Record.Id != r.Record.Id));
        preferredList.AddRange(rest);
        return preferredList;
    }

    /// <summary>
    /// Formats search results as a clear list of requirements.
    /// </summary>
    private static string FormatRequirements(IReadOnlyList<VectorSearchResult<PolicySectionRecord>> results)
    {
        // Goal:
        // - Return a SHORT list (max 3) for "💡 Le vendeur pourrait aussi demander"
        // - Avoid duplicates
        // - Prefer "documents/preuves" over generic tips if we have enough items
        // - Avoid mixing unrelated typologies: use best match first, then fallback

        var candidates = new List<(int ResultRank, string Text, bool IsDocument)>();

        for (var i = 0; i < results.Count; i++)
        {
            var record = results[i].Record;
            if (string.IsNullOrWhiteSpace(record.Content)) continue;

            var lines = record.Content
                .Split(new[] { "\r\n", "\n" }, StringSplitOptions.RemoveEmptyEntries)
                .Select(l => l.Trim())
                .Where(l => !string.IsNullOrWhiteSpace(l));

            foreach (var line in lines)
            {
                if (!line.StartsWith("- ")) continue;

                var cleaned = line.Trim();
                candidates.Add((i, cleaned, IsDocumentLike(cleaned)));
            }

            // Prefer not to blend too many sections:
            // if the best record already has enough bullets, stop early.
            if (i == 0 && candidates.Count(c => c.ResultRank == 0) >= 3)
            {
                break;
            }
        }

        if (candidates.Count == 0)
        {
            return "Aucune information vendeur pertinente trouvée pour ce type de problème.";
        }

        // De-duplicate (accent/case/punctuation insensitive)
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var selected = new List<string>();

        // Order:
        // - Best matching record first
        // - Prefer document-like items
        // - Then shorter (usually clearer)
        var ordered = candidates
            .OrderBy(c => c.ResultRank)
            .ThenByDescending(c => c.IsDocument)
            .ThenBy(c => c.Text.Length);

        foreach (var c in ordered)
        {
            var key = NormalizeKey(c.Text);
            if (!seen.Add(key)) continue;

            // Ensure bullet prefix
            var text = c.Text.StartsWith("- ") ? c.Text : $"- {c.Text}";
            selected.Add(text);

            if (selected.Count >= 3) break;
        }

        return string.Join("\n", selected).Trim();
    }

    #endregion

    #region Private Helpers

    private static bool IsDocumentLike(string bulletLine)
    {
        // bulletLine usually starts with "- "
        var t = NormalizeKey(bulletLine);
        // Document/proof keywords
        var docKeywords = new[]
        {
            "photo", "video", "facture", "preuve", "bon de livraison", "etiquette",
            "numero de suivi", "numero de serie", "capture", "suivi", "reference"
        };

        if (docKeywords.Any(k => t.Contains(NormalizeKey(k), StringComparison.Ordinal)))
        {
            return true;
        }

        // Common "tip" verbs → treat as non-document
        var tipStarts = new[]
        {
            "verifier", "tester", "charger", "tenter", "patienter", "redemarrer", "reinitialiser", "comparer"
        };
        return !tipStarts.Any(v => t.Contains(v, StringComparison.Ordinal));
    }

    private static string NormalizeKey(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Lowercase + remove diacritics
        var normalized = input.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var ch in normalized)
        {
            var cat = CharUnicodeInfo.GetUnicodeCategory(ch);
            if (cat != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(ch);
            }
        }

        var noDiacritics = sb.ToString().Normalize(NormalizationForm.FormC);

        // Remove punctuation except spaces
        noDiacritics = Regex.Replace(noDiacritics, @"[^\p{L}\p{Nd}\s]", " ");
        // Collapse whitespace
        noDiacritics = Regex.Replace(noDiacritics, @"\s+", " ").Trim();

        // Remove leading dash if present
        if (noDiacritics.StartsWith("- "))
        {
            noDiacritics = noDiacritics[2..].Trim();
        }

        return noDiacritics;
    }

    #endregion
}
