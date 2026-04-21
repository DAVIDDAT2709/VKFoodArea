using VKFoodArea.Helpers;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

internal static class PoiReferenceMatcher
{
    public static Poi? FindMatch(IEnumerable<Poi> candidates, Poi? referencePoi, int? preferredPoiId = null)
    {
        var poiList = candidates as IList<Poi> ?? candidates.ToList();

        if (preferredPoiId.HasValue && preferredPoiId.Value > 0)
        {
            var byId = poiList.FirstOrDefault(x => x.Id == preferredPoiId.Value);
            if (byId is not null)
                return byId;
        }

        if (referencePoi is null)
            return null;

        var normalizedQr = NormalizeQrCode(referencePoi.QrCode);
        if (!string.IsNullOrWhiteSpace(normalizedQr))
        {
            var byQr = poiList.FirstOrDefault(x => NormalizeQrCode(x.QrCode) == normalizedQr);
            if (byQr is not null)
                return byQr;
        }

        var identityKey = BuildIdentityKey(referencePoi.Name, referencePoi.Address);
        if (!string.IsNullOrWhiteSpace(identityKey))
        {
            var byIdentity = poiList.FirstOrDefault(x =>
                BuildIdentityKey(x.Name, x.Address) == identityKey);
            if (byIdentity is not null)
                return byIdentity;
        }

        return null;
    }

    public static string NormalizeQrCode(string? qrCode)
        => QrCodePayload.Normalize(qrCode);

    public static string BuildIdentityKey(string? name, string? address)
    {
        var normalizedName = (name ?? string.Empty).Trim().ToLowerInvariant();
        var normalizedAddress = (address ?? string.Empty).Trim().ToLowerInvariant();

        if (string.IsNullOrWhiteSpace(normalizedName) &&
            string.IsNullOrWhiteSpace(normalizedAddress))
        {
            return string.Empty;
        }

        return $"{normalizedName}|{normalizedAddress}";
    }
}
