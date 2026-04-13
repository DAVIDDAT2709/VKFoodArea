using Microsoft.Maui.ApplicationModel;
using Microsoft.Maui.Controls;
using Microsoft.Extensions.DependencyInjection;
using VKFoodArea.Features.Home;
using VKFoodArea.Helpers;
using VKFoodArea.Models;
using VKFoodArea.Repositories;

namespace VKFoodArea.Services;

public class AppLinkService
{
    private readonly object _pendingSync = new();
    private readonly QrLookupService _qrLookupService;
    private readonly PoiRepository _poiRepository;
    private readonly NarrationService _narrationService;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;
    private readonly AuthService _authService;
    private readonly AppRootNavigationService _rootNavigationService;
    private readonly TourSessionService _tourSessionService;
    private readonly IServiceProvider _serviceProvider;
    private readonly SemaphoreSlim _handleLock = new(1, 1);

    private Uri? _pendingUri;

    public AppLinkService(
        QrLookupService qrLookupService,
        PoiRepository poiRepository,
        NarrationService narrationService,
        AppTextService text,
        NarrationUiStateService narrationUiState,
        AuthService authService,
        AppRootNavigationService rootNavigationService,
        TourSessionService tourSessionService,
        IServiceProvider serviceProvider)
    {
        _qrLookupService = qrLookupService;
        _poiRepository = poiRepository;
        _narrationService = narrationService;
        _text = text;
        _narrationUiState = narrationUiState;
        _authService = authService;
        _rootNavigationService = rootNavigationService;
        _tourSessionService = tourSessionService;
        _serviceProvider = serviceProvider;
    }

    public void Enqueue(Uri? uri)
    {
        if (uri is null)
            return;

        lock (_pendingSync)
        {
            _pendingUri = uri;
        }
    }

    public async Task<bool> TryHandlePendingAsync(CancellationToken ct = default)
    {
        if (_authService.CurrentUser is null)
            return false;

        await _handleLock.WaitAsync(ct);
        try
        {
            if (_authService.CurrentUser is null)
                return false;

            Uri? pendingUri;

            lock (_pendingSync)
            {
                pendingUri = _pendingUri;
                _pendingUri = null;
            }

            if (pendingUri is null)
                return false;

            try
            {
                await HandleUriCoreAsync(pendingUri, ct);
                return true;
            }
            catch (OperationCanceledException)
            {
                Enqueue(pendingUri);
                return false;
            }
            catch (InvalidOperationException)
            {
                Enqueue(pendingUri);
                return false;
            }
            catch (AppLinkTargetNotFoundException)
            {
                await ShowAlertAsync(
                    _text["Qr.NotFoundTitle"],
                    _text.Format("Qr.NotFoundMessage", QrCodePayload.Normalize(pendingUri.ToString())));
                return false;
            }
            catch (Exception ex)
            {
                await ShowAlertAsync(_text["Qr.ConnectionErrorTitle"], ex.Message);
                return false;
            }
        }
        finally
        {
            _handleLock.Release();
        }
    }

    private async Task HandleUriCoreAsync(Uri uri, CancellationToken ct)
    {
        var code = QrCodePayload.Normalize(uri.ToString());
        if (string.IsNullOrWhiteSpace(code))
            throw new AppLinkTargetNotFoundException();

        var localPoi = await _poiRepository.GetByQrCodeAsync(code);
        QrResolveResult? resolved = null;

        try
        {
            resolved = await _qrLookupService.ResolveAsync(code, ct);
        }
        catch when (localPoi is not null)
        {
            resolved = null;
        }

        resolved ??= BuildLocalPoiFallback(code, localPoi);
        if (resolved is null)
            throw new AppLinkTargetNotFoundException();

        switch (QrTargetTypes.Normalize(resolved.TargetType))
        {
            case QrTargetTypes.Tour:
                if (resolved.Tour is null)
                    throw new AppLinkTargetNotFoundException();

                _tourSessionService.Start(resolved.Tour);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var navigationPage = await EnsureHomeNavigationAsync();
                    await navigationPage.Navigation.PushAsync(_serviceProvider.GetRequiredService<TourSessionPage>());
                });
                return;

            default:
                var poi = MergePoiForDisplay(localPoi, resolved.Poi);
                if (poi is null)
                    throw new AppLinkTargetNotFoundException();

                _narrationUiState.SetContext(poi);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    var navigationPage = await EnsureHomeNavigationAsync();

                    if (navigationPage.Navigation.NavigationStack.LastOrDefault() is not PoiDetailPage existingDetailPage ||
                        existingDetailPage.Poi.Id != poi.Id)
                    {
                        await navigationPage.Navigation.PushAsync(new PoiDetailPage(
                            poi,
                            _narrationService,
                            _text,
                            _narrationUiState));
                    }
                });

                await _narrationService.PlayPoiAsync(poi, triggerSource: "app-link", ct: ct);
                return;
        }
    }

    private async Task ShowAlertAsync(string title, string message)
    {
        await MainThread.InvokeOnMainThreadAsync(async () =>
        {
            var page = GetCurrentPage();
            if (page is not null)
                await page.DisplayAlertAsync(title, message, _text["Common.Ok"]);
        });
    }

    private static Page? GetCurrentPage()
    {
        var application = Application.Current;
        var window = application?.Windows.FirstOrDefault();
        var page = window?.Page;

        if (page is NavigationPage navigationPage)
            return navigationPage.CurrentPage;

        return page;
    }

    private static Poi? MergePoiForDisplay(Poi? localPoi, Poi? webPoi)
    {
        if (localPoi is null)
            return ClonePoi(webPoi);

        if (webPoi is null)
            return ClonePoi(localPoi);

        var merged = ClonePoi(localPoi);
        if (merged is null)
            return ClonePoi(webPoi);

        merged.Name = webPoi.Name;
        merged.Address = webPoi.Address;
        merged.PhoneNumber = webPoi.PhoneNumber;
        merged.ImageUrl = webPoi.ImageUrl;
        merged.Description = webPoi.Description;
        merged.Latitude = webPoi.Latitude;
        merged.Longitude = webPoi.Longitude;
        merged.RadiusMeters = webPoi.RadiusMeters;
        merged.Priority = webPoi.Priority;
        merged.QrCode = webPoi.QrCode;
        merged.IsActive = webPoi.IsActive;
        merged.MapUrl = webPoi.MapUrl;
        merged.TtsScriptVi = webPoi.TtsScriptVi;
        merged.TtsScriptEn = webPoi.TtsScriptEn;
        merged.TtsScriptZh = webPoi.TtsScriptZh;
        merged.TtsScriptJa = webPoi.TtsScriptJa;
        merged.TtsScriptDe = webPoi.TtsScriptDe;
        merged.AudioFileVi = webPoi.AudioFileVi;
        merged.AudioFileEn = webPoi.AudioFileEn;
        merged.AudioFileJa = webPoi.AudioFileJa;
        return merged;
    }

    private static Poi? ClonePoi(Poi? poi)
    {
        if (poi is null)
            return null;

        return new Poi
        {
            Id = poi.Id,
            Name = poi.Name,
            Address = poi.Address,
            PhoneNumber = poi.PhoneNumber,
            ImageUrl = poi.ImageUrl,
            Description = poi.Description,
            Latitude = poi.Latitude,
            Longitude = poi.Longitude,
            RadiusMeters = poi.RadiusMeters,
            Priority = poi.Priority,
            QrCode = poi.QrCode,
            IsActive = poi.IsActive,
            MapUrl = poi.MapUrl,
            TtsScriptVi = poi.TtsScriptVi,
            TtsScriptEn = poi.TtsScriptEn,
            TtsScriptZh = poi.TtsScriptZh,
            TtsScriptJa = poi.TtsScriptJa,
            TtsScriptDe = poi.TtsScriptDe,
            AudioFileVi = poi.AudioFileVi,
            AudioFileEn = poi.AudioFileEn,
            AudioFileJa = poi.AudioFileJa
        };
    }

    private async Task<NavigationPage> EnsureHomeNavigationAsync()
    {
        await _rootNavigationService.SetRootAsync<HomeDesignPage>();

        var application = Application.Current
            ?? throw new InvalidOperationException("Application.Current is unavailable.");

        var window = application.Windows.FirstOrDefault()
            ?? throw new InvalidOperationException("No active MAUI window is available.");

        if (window.Page is not NavigationPage navigationPage)
            throw new InvalidOperationException("Root page is not a navigation page.");

        if (navigationPage.Navigation.NavigationStack.Count > 1)
            await navigationPage.Navigation.PopToRootAsync(false);

        return navigationPage;
    }

    private static QrResolveResult? BuildLocalPoiFallback(string qrCode, Poi? localPoi)
    {
        if (localPoi is null)
            return null;

        return new QrResolveResult
        {
            TargetType = QrTargetTypes.Poi,
            TargetId = localPoi.Id,
            MatchedCode = qrCode,
            Source = "local",
            Poi = ClonePoi(localPoi)
        };
    }

    private sealed class AppLinkTargetNotFoundException : Exception;
}
