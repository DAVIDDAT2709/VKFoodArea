using System.Globalization;
using System.Text;
using VKFoodArea.Models;
using VKFoodArea.Repositories;

namespace VKFoodArea.Services;

public class PoiService
{
    private readonly PoiRepository _poiRepository;

    public PoiService(PoiRepository poiRepository)
    {
        _poiRepository = poiRepository;
    }

    public Task<List<Poi>> GetAllPoisAsync(CancellationToken ct = default)
        => _poiRepository.GetActiveAsync(ct);

    public Task<Poi?> GetPoiByIdAsync(int id, CancellationToken ct = default)
        => _poiRepository.GetByIdAsync(id, ct);

    public async Task<PoiSearchResponse> SearchPoisAsync(string? keyword, CancellationToken ct = default)
    {
        var pois = await _poiRepository.GetActiveAsync(ct);
        var normalizedKeyword = NormalizeSearchText(keyword);

        if (string.IsNullOrWhiteSpace(normalizedKeyword))
            return new PoiSearchResponse(pois, []);

        var ranked = pois
            .Select(poi => new
            {
                Poi = poi,
                Score = GetSearchScore(poi, normalizedKeyword)
            })
            .Where(x => x.Score > int.MinValue)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Poi.Name.Length)
            .ToList();

        return new PoiSearchResponse(
            ranked.Select(x => x.Poi).ToList(),
            ranked.Take(6).Select(x => x.Poi).ToList());
    }

    public static string NormalizeSearchText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var normalized = text.Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(normalized.Length);

        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);

            if (category == UnicodeCategory.NonSpacingMark)
                continue;

            builder.Append(character switch
            {
                '\u0111' => 'd',
                '\u0110' => 'd',
                _ => character
            });
        }

        return builder
            .ToString()
            .Normalize(NormalizationForm.FormC);
    }

    private static int GetSearchScore(Poi poi, string normalizedKeyword)
    {
        var nameKey = NormalizeSearchText(poi.Name);
        var addressKey = NormalizeSearchText(poi.Address);

        var tokens = normalizedKeyword
            .Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        if (tokens.Length == 0)
            return int.MinValue;

        var combined = $"{nameKey} {addressKey}";

        if (tokens.Any(token => !combined.Contains(token, StringComparison.Ordinal)))
            return int.MinValue;

        var score = 0;

        if (nameKey.StartsWith(normalizedKeyword, StringComparison.Ordinal))
            score += 500;
        else if (nameKey.Contains(normalizedKeyword, StringComparison.Ordinal))
            score += 360;
        else if (addressKey.StartsWith(normalizedKeyword, StringComparison.Ordinal))
            score += 260;
        else if (addressKey.Contains(normalizedKeyword, StringComparison.Ordinal))
            score += 180;

        foreach (var token in tokens)
        {
            if (nameKey.Contains(token, StringComparison.Ordinal))
                score += 30;
            else if (addressKey.Contains(token, StringComparison.Ordinal))
                score += 16;
        }

        return score;
    }
}

public sealed record PoiSearchResponse(
    IReadOnlyList<Poi> Results,
    IReadOnlyList<Poi> Suggestions);
