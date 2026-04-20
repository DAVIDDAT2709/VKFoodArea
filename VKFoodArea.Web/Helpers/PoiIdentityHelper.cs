using System.Globalization;
using System.Text;

namespace VKFoodArea.Web.Helpers;

public static class PoiIdentityHelper
{
    public const int CoordinatePrecision = 6;

    public static string NormalizeIdentityText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var builder = new StringBuilder(value.Length);
        var previousWasSpace = false;

        foreach (var character in value
                     .Trim()
                     .ToLowerInvariant()
                     .Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(character) == UnicodeCategory.NonSpacingMark)
                continue;

            var normalizedCharacter = character switch
            {
                '\u0111' => 'd',
                '\u0110' => 'd',
                _ => character
            };

            if (char.IsWhiteSpace(normalizedCharacter))
            {
                if (previousWasSpace)
                    continue;

                builder.Append(' ');
                previousWasSpace = true;
                continue;
            }

            builder.Append(normalizedCharacter);
            previousWasSpace = false;
        }

        return builder.ToString().Trim();
    }

    public static string BuildIdentityKey(string? name, string? address)
        => $"{NormalizeIdentityText(name)}|{NormalizeIdentityText(address)}";

    public static string BuildCoordinateKey(double latitude, double longitude)
    {
        var normalizedLatitude = Math.Round(latitude, CoordinatePrecision, MidpointRounding.AwayFromZero);
        var normalizedLongitude = Math.Round(longitude, CoordinatePrecision, MidpointRounding.AwayFromZero);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0:F6}|{1:F6}",
            normalizedLatitude,
            normalizedLongitude);
    }
}
