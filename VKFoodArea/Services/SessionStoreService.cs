using Microsoft.Maui.Storage;

namespace VKFoodArea.Services;

public sealed class SessionStoreService
{
    private const string CurrentUserIdKey = "session_user_id";

    public void Save(int userId)
    {
        Preferences.Default.Set(CurrentUserIdKey, userId);
    }

    public int? GetCurrentUserId()
    {
        var userId = Preferences.Default.Get(CurrentUserIdKey, 0);
        return userId > 0 ? userId : null;
    }

    public void Clear()
    {
        Preferences.Default.Remove(CurrentUserIdKey);
    }
}
