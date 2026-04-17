using Microsoft.EntityFrameworkCore;
using VKFoodArea.Web.Data;
using VKFoodArea.Web.Models;
using VKFoodArea.Web.ViewModels;

namespace VKFoodArea.Web.Services;

public class AppDevicePresenceService : IAppDevicePresenceService
{
    private static readonly TimeSpan ActiveWindow = TimeSpan.FromSeconds(15);
    private readonly AppDbContext _context;

    public AppDevicePresenceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task UpsertHeartbeatAsync(AppDeviceHeartbeatViewModel vm)
    {
        var now = DateTime.UtcNow;
        var deviceKey = Normalize(vm.DeviceKey);

        if (string.IsNullOrWhiteSpace(deviceKey))
            throw new InvalidOperationException("Missing device key.");

        var session = await _context.AppDeviceSessions
            .FirstOrDefaultAsync(x => x.DeviceKey == deviceKey);

        if (session is null)
        {
            session = new AppDeviceSession
            {
                DeviceKey = deviceKey,
                CreatedAt = now
            };
            _context.AppDeviceSessions.Add(session);
        }

        session.UserKey = Normalize(vm.UserKey);
        session.Username = Normalize(vm.Username);
        session.FullName = Normalize(vm.FullName);
        session.Platform = Normalize(vm.Platform);
        session.DeviceName = Normalize(vm.DeviceName);
        session.AppVersion = Normalize(vm.AppVersion);
        session.IsOnline = vm.IsOnline;
        session.LastHeartbeatAt = now;

        if (!vm.IsOnline)
            session.LastOfflineAt = now;

        if (!string.IsNullOrWhiteSpace(session.UserKey))
        {
            var user = await _context.AppUserAccounts
                .FirstOrDefaultAsync(x => x.UserKey == session.UserKey);

            if (user is not null)
                user.LastSeenAt = now;
        }

        await _context.SaveChangesAsync();
    }

    public async Task<ActiveDeviceSummaryViewModel> GetSummaryAsync()
    {
        var threshold = DateTime.UtcNow - ActiveWindow;

        var activeSessions = await _context.AppDeviceSessions
            .AsNoTracking()
            .Where(x => x.IsOnline && x.LastHeartbeatAt >= threshold)
            .OrderByDescending(x => x.LastHeartbeatAt)
            .ToListAsync();

        var activeDeviceCount = activeSessions
            .Select(x => x.DeviceKey)
            .Distinct(StringComparer.Ordinal)
            .Count();

        var activeUserCount = activeSessions
            .Select(x => string.IsNullOrWhiteSpace(x.UserKey) ? x.DeviceKey : x.UserKey)
            .Distinct(StringComparer.Ordinal)
            .Count();

        return new ActiveDeviceSummaryViewModel
        {
            ActiveDeviceCount = activeDeviceCount,
            ActiveUserCount = activeUserCount,
            TimeoutSeconds = (int)ActiveWindow.TotalSeconds,
            Devices = activeSessions.Select(x => new ActiveDeviceItemViewModel
            {
                DeviceKey = x.DeviceKey,
                UserKey = x.UserKey,
                Username = x.Username,
                FullName = x.FullName,
                Platform = x.Platform,
                DeviceName = x.DeviceName,
                AppVersion = x.AppVersion,
                LastHeartbeatAt = x.LastHeartbeatAt,
                LastHeartbeatDisplay = WebDisplayTime.Format(x.LastHeartbeatAt, "dd/MM HH:mm:ss")
            }).ToList()
        };
    }

    private static string Normalize(string? value)
        => (value ?? string.Empty).Trim();
}
