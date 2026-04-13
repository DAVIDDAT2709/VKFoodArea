using Microsoft.Maui.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using ZXing.Net.Maui;
using VKFoodArea.Helpers;
using VKFoodArea.Models;
using VKFoodArea.Repositories;
using VKFoodArea.Services;

namespace VKFoodArea.Features.Home;

public partial class QrScannerPage : ContentPage
{
    private readonly QrLookupService _qrLookupService;
    private readonly PoiRepository _poiRepository;
    private readonly NarrationService _narrationService;
    private readonly AppTextService _text;
    private readonly NarrationUiStateService _narrationUiState;
    private readonly ApiBaseUrlService _apiBaseUrlService;
    private readonly TourSessionService _tourSessionService;
    private readonly IServiceProvider _serviceProvider;

    private bool _isHandlingResult;
    private bool _isTorchOn;

    public QrScannerPage(
        QrLookupService qrLookupService,
        PoiRepository poiRepository,
        NarrationService narrationService,
        AppTextService text,
        NarrationUiStateService narrationUiState,
        ApiBaseUrlService apiBaseUrlService,
        TourSessionService tourSessionService,
        IServiceProvider serviceProvider)
    {
        InitializeComponent();

        _qrLookupService = qrLookupService;
        _poiRepository = poiRepository;
        _narrationService = narrationService;
        _text = text;
        _narrationUiState = narrationUiState;
        _apiBaseUrlService = apiBaseUrlService;
        _tourSessionService = tourSessionService;
        _serviceProvider = serviceProvider;

        QrReader.Options = new BarcodeReaderOptions
        {
            Formats = BarcodeFormats.TwoDimensional,
            AutoRotate = true,
            Multiple = false
        };
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        ApplyLocalizedText();

        if (!BarcodeScanning.IsSupported)
        {
            await DisplayAlertAsync(_text["Qr.NotSupportedTitle"], _text["Qr.NotSupportedMessage"], _text["Common.Ok"]);
            await Navigation.PopAsync();
            return;
        }

        var status = await Permissions.CheckStatusAsync<Permissions.Camera>();
        if (status != PermissionStatus.Granted)
            status = await Permissions.RequestAsync<Permissions.Camera>();

        if (status != PermissionStatus.Granted)
        {
            await DisplayAlertAsync(_text["Qr.PermissionTitle"], _text["Qr.PermissionMessage"], _text["Common.Ok"]);
            await Navigation.PopAsync();
            return;
        }

        _isHandlingResult = false;
        QrReader.IsDetecting = true;
        TorchButton.Text = _isTorchOn ? _text["Qr.TorchOn"] : _text["Qr.TorchOff"];
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        QrReader.IsDetecting = false;
    }

    private async void OnBarcodesDetected(object sender, BarcodeDetectionEventArgs e)
    {
        if (_isHandlingResult)
            return;

        var rawValue = e.Results?.FirstOrDefault()?.Value?.Trim();
        var value = QrCodePayload.Normalize(rawValue);

        if (string.IsNullOrWhiteSpace(value))
            return;

        _isHandlingResult = true;
        QrReader.IsDetecting = false;

        try
        {
            var localPoi = await _poiRepository.GetByQrCodeAsync(value);
            QrResolveResult? resolved = null;
            Exception? resolveException = null;

            try
            {
                resolved = await _qrLookupService.ResolveAsync(value);
            }
            catch (Exception ex)
            {
                resolveException = ex;
            }

            resolved ??= BuildLocalPoiFallback(value, localPoi);

            if (resolved is null)
            {
                if (resolveException is not null)
                    throw resolveException;

                await HandleNotFoundAsync(value);
                return;
            }

            var handled = await HandleResolvedTargetAsync(localPoi, resolved);
            if (!handled)
            {
                await HandleNotFoundAsync(value);
                return;
            }

            _isHandlingResult = false;
        }
        catch (Exception ex)
        {
            var retry = await MainThread.InvokeOnMainThreadAsync(() =>
                DisplayAlertAsync(
                    _text["Qr.ConnectionErrorTitle"],
                    ex.Message,
                    _text["Common.Again"],
                    _text["Common.Close"]));

            if (retry)
            {
                _isHandlingResult = false;
                QrReader.IsDetecting = true;
            }
            else
            {
                await MainThread.InvokeOnMainThreadAsync(() => Navigation.PopAsync());
            }
        }
    }

    private async Task<bool> HandleResolvedTargetAsync(Poi? localPoi, QrResolveResult resolved)
    {
        switch (QrTargetTypes.Normalize(resolved.TargetType))
        {
            case QrTargetTypes.Tour:
                if (resolved.Tour is null)
                    return false;

                _tourSessionService.Start(resolved.Tour);

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    await Navigation.PushAsync(_serviceProvider.GetRequiredService<TourSessionPage>());
                });

                return true;

            default:
                var poi = MergePoiForDisplay(localPoi, resolved.Poi);
                if (poi is null)
                    return false;

                await MainThread.InvokeOnMainThreadAsync(async () =>
                {
                    _narrationUiState.SetContext(poi);

                    await Navigation.PushAsync(new PoiDetailPage(
                        poi,
                        _narrationService,
                        _text,
                        _narrationUiState));

                    await _narrationService.PlayPoiAsync(poi, triggerSource: "qr");
                });

                return true;
        }
    }

    private async Task HandleNotFoundAsync(string value)
    {
        var retry = await MainThread.InvokeOnMainThreadAsync(() =>
            DisplayAlertAsync(
                _text["Qr.NotFoundTitle"],
                _text.Format("Qr.NotFoundMessage", value),
                _text["Common.Again"],
                _text["Common.Close"]));

        if (retry)
        {
            _isHandlingResult = false;
            QrReader.IsDetecting = true;
            return;
        }

        await MainThread.InvokeOnMainThreadAsync(() => Navigation.PopAsync());
    }

    private void OnTorchClicked(object sender, EventArgs e)
    {
        _isTorchOn = !_isTorchOn;
        QrReader.IsTorchOn = _isTorchOn;
        TorchButton.Text = _isTorchOn ? _text["Qr.TorchOn"] : _text["Qr.TorchOff"];
    }

    private async void OnApiClicked(object sender, EventArgs e)
    {
        var value = await DisplayPromptAsync(
            "API demo",
            "Nhap URL ngrok cua Web, bo trong de ve mac dinh.",
            "Luu",
            _text["Common.Close"],
            initialValue: _apiBaseUrlService.BaseUrl,
            keyboard: Keyboard.Url);

        if (value is null)
            return;

        var result = _apiBaseUrlService.SaveDemoBaseUrl(value);
        ApplyLocalizedText();
        await DisplayAlertAsync(
            result.Success ? "API demo" : _text["Common.Error"],
            result.Message,
            _text["Common.Ok"]);
    }

    private async void OnCloseClicked(object sender, EventArgs e)
    {
        await Navigation.PopAsync();
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

    private void ApplyLocalizedText()
    {
        Title = _text["Qr.PageTitle"];
        ScannerTitleLabel.Text = _text["Qr.HeaderTitle"];
        ScannerSupportLabel.Text =
    $"Quet QR tai diem dung de nghe thuyet minh ngay.\nAPI: {_apiBaseUrlService.BaseUrl}";
        ScannerHintLabel.Text = _text["Qr.Hint"];
        TorchButton.Text = _isTorchOn ? _text["Qr.TorchOn"] : _text["Qr.TorchOff"];
        CloseButton.Text = _text["Common.Close"];
    }
}
