using Microsoft.Maui.Devices;

namespace VKFoodArea.Services;

public class ApiBaseUrlService
{
    public string BaseUrl =>
        DeviceInfo.Platform == DevicePlatform.Android
            ? "http://10.0.2.2:5216/"
            : "http://localhost:5216/";
}