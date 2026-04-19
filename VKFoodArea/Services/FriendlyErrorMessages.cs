using System.Net.Http;

namespace VKFoodArea.Services;

public enum FriendlyErrorContext
{
    Startup,
    QrScan,
    History,
    Preview,
    TourCatalog
}

public static class FriendlyErrorMessages
{
    public static string Get(Exception exception, AppTextService text, FriendlyErrorContext context)
    {
        var language = text.CurrentLanguage;

        if (exception is RemoteEndpointUnavailableException)
            return GetRemoteEndpointUnavailableMessage(language, context);

        var isNetworkIssue = LooksLikeNetworkIssue(exception);

        return context switch
        {
            FriendlyErrorContext.Startup => isNetworkIssue
                ? Localize(
                    language,
                    "Không thể làm mới dữ liệu lúc này. Ứng dụng sẽ mở với dữ liệu đang có.",
                    "Couldn't refresh data right now. The app will open with the data already on this device.",
                    "目前无法更新数据。应用将使用设备上的现有数据继续打开。",
                    "現在はデータを更新できません。端末内のデータでそのまま利用できます。",
                    "Die Daten konnten gerade nicht aktualisiert werden. Die App wird mit den vorhandenen Gerätedaten geöffnet.")
                : Localize(
                    language,
                    "Ứng dụng vừa gặp trục trặc nhỏ. Bạn vẫn có thể tiếp tục và thử lại sau.",
                    "The app hit a small issue. You can keep going and try again later.",
                    "应用刚刚遇到一个小问题。你仍然可以继续使用，稍后再试。",
                    "アプリで軽い問題が発生しました。続けて使って、あとで再試行できます。",
                    "Die App hatte gerade ein kleines Problem. Sie können weitermachen und es später erneut versuchen."),

            FriendlyErrorContext.QrScan => isNetworkIssue
                ? Localize(
                    language,
                    "Không kết nối được để kiểm tra mã QR. Hãy kiểm tra mạng và quét lại.",
                    "Couldn't connect to verify this QR code. Check your connection and scan again.",
                    "无法连接来验证这个二维码。请检查网络后再扫描。",
                    "QRコードを確認するための接続ができません。通信を確認してもう一度スキャンしてください。",
                    "Die Verbindung zum Prüfen des QR-Codes konnte nicht hergestellt werden. Bitte Netzwerk prüfen und erneut scannen.")
                : Localize(
                    language,
                    "Mã QR này chưa sẵn sàng để mở lúc này. Hãy quét lại sau ít giây.",
                    "This QR code isn't ready to open right now. Please try scanning again in a moment.",
                    "这个二维码暂时还无法打开。请稍后再试。",
                    "このQRコードは現在まだ開けません。少し待ってからもう一度お試しください。",
                    "Dieser QR-Code kann im Moment nicht geöffnet werden. Bitte versuchen Sie es in Kürze erneut."),

            FriendlyErrorContext.History => isNetworkIssue
                ? Localize(
                    language,
                    "Không tải được lịch sử từ web lúc này. Ứng dụng sẽ dùng dữ liệu đang có.",
                    "Couldn't load history from the web right now. The app will use the data already available.",
                    "目前无法从网络加载历史记录。应用将使用现有数据。",
                    "現在はWebから履歴を読み込めません。端末内のデータを使います。",
                    "Der Verlauf konnte gerade nicht aus dem Web geladen werden. Die App verwendet die vorhandenen Daten.")
                : Localize(
                    language,
                    "Lịch sử chưa sẵn sàng ngay lúc này. Vui lòng thử lại sau.",
                    "History isn't ready just yet. Please try again later.",
                    "历史记录暂时还没准备好。请稍后再试。",
                    "履歴はまだ準備中です。しばらくしてからもう一度お試しください。",
                    "Der Verlauf ist gerade noch nicht bereit. Bitte versuchen Sie es später erneut."),

            FriendlyErrorContext.Preview => isNetworkIssue
                ? Localize(
                    language,
                    "Không thể nghe thử lúc này. Hãy kiểm tra mạng rồi thử lại.",
                    "Couldn't play the preview right now. Check your connection and try again.",
                    "目前无法播放试听。请检查网络后重试。",
                    "現在は試聴できません。通信を確認してからもう一度お試しください。",
                    "Die Vorschau kann gerade nicht abgespielt werden. Bitte Netzwerk prüfen und erneut versuchen.")
                : Localize(
                    language,
                    "Thiết bị chưa sẵn sàng để phát bản nghe thử này. Hãy thử lại sau ít giây.",
                    "This device isn't ready to play the preview yet. Please try again in a moment.",
                    "设备暂时还不能播放这个试听。请稍后再试。",
                    "この端末ではまだ試聴を再生できません。少し待ってからもう一度お試しください。",
                    "Dieses Gerät ist noch nicht bereit, die Vorschau abzuspielen. Bitte versuchen Sie es in Kürze erneut."),

            FriendlyErrorContext.TourCatalog => isNetworkIssue
                ? Localize(
                    language,
                    "Không tải được danh sách tour lúc này. Hãy thử lại khi mạng ổn định hơn.",
                    "Couldn't load the tour list right now. Try again when the connection is more stable.",
                    "目前无法加载导览列表。请在网络更稳定时再试。",
                    "現在はツアー一覧を読み込めません。通信が安定してから再試行してください。",
                    "Die Tourliste konnte gerade nicht geladen werden. Bitte versuchen Sie es erneut, wenn die Verbindung stabiler ist.")
                : Localize(
                    language,
                    "Danh sách tour chưa sẵn sàng ngay lúc này. Vui lòng thử lại sau.",
                    "The tour list isn't ready just yet. Please try again later.",
                    "导览列表暂时还没准备好。请稍后再试。",
                    "ツアー一覧はまだ準備中です。しばらくしてからもう一度お試しください。",
                    "Die Tourliste ist gerade noch nicht bereit. Bitte versuchen Sie es später erneut."),

            _ => Localize(
                language,
                "Đã có lỗi xảy ra. Vui lòng thử lại.",
                "Something went wrong. Please try again.",
                "发生了一些问题，请重试。",
                "問題が発生しました。もう一度お試しください。",
                "Es ist ein Problem aufgetreten. Bitte versuchen Sie es erneut.")
        };
    }

    private static bool LooksLikeNetworkIssue(Exception exception)
    {
        if (exception is HttpRequestException or TimeoutException)
            return true;

        if (exception is TaskCanceledException taskCanceledException &&
            !taskCanceledException.CancellationToken.IsCancellationRequested)
        {
            return true;
        }

        var message = exception.ToString();
        return message.Contains("http", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("socket", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("network", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("timeout", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("timed out", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("host", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("connection", StringComparison.OrdinalIgnoreCase) ||
               message.Contains("ngrok", StringComparison.OrdinalIgnoreCase);
    }

    private static string Localize(
        string language,
        string vietnamese,
        string english,
        string chinese,
        string japanese,
        string german)
    {
        return language switch
        {
            "en" => english,
            "zh" => chinese,
            "ja" => japanese,
            "de" => german,
            _ => vietnamese
        };
    }

    private static string GetRemoteEndpointUnavailableMessage(string language, FriendlyErrorContext context)
    {
        if (!string.Equals(language, "vi", StringComparison.OrdinalIgnoreCase))
        {
            return context switch
            {
                FriendlyErrorContext.Startup => "Web data is not connected yet. The app will open with data already on this device.",
                FriendlyErrorContext.QrScan => "This device is not connected to the web data source for this QR code yet.",
                FriendlyErrorContext.History => "Web history is not connected yet. The app will use history already on this device.",
                FriendlyErrorContext.Preview => "The online preview source is not connected on this device yet.",
                FriendlyErrorContext.TourCatalog => "The online tour list is not connected on this device yet.",
                _ => "This device is not connected to the online data source yet."
            };
        }

        return context switch
        {
            FriendlyErrorContext.Startup => "Chưa kết nối dữ liệu web. Ứng dụng sẽ mở với dữ liệu đang có trên thiết bị.",
            FriendlyErrorContext.QrScan => "Thiết bị này chưa kết nối nguồn dữ liệu web cho mã QR này.",
            FriendlyErrorContext.History => "Chưa kết nối lịch sử web. Ứng dụng sẽ dùng dữ liệu đang có trên thiết bị.",
            FriendlyErrorContext.Preview => "Thiết bị này chưa kết nối nguồn nghe thử trực tuyến.",
            FriendlyErrorContext.TourCatalog => "Danh sách tour trực tuyến chưa được kết nối trên thiết bị này.",
            _ => "Thiết bị này chưa kết nối dữ liệu trực tuyến."
        };
    }
}
