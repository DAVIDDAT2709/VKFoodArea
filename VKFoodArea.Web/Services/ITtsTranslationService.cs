namespace VKFoodArea.Web.Services;

public interface ITtsTranslationService
{
    Task<TtsTranslationBundle> GenerateFromVietnameseAsync(string vietnameseText, CancellationToken ct = default);
}

public sealed record TtsTranslationBundle(
    string Vi,
    string En,
    string Zh,
    string Ja,
    string De);
