using System.Text.Json;
using Microsoft.Maui.Storage;
using VKFoodArea.Models;

namespace VKFoodArea.Services;

public sealed class TourSessionService
{
    private const string TourSessionKey = "active_tour_session";
    private readonly object _sync = new();
    private TourSession? _currentSession;

    public event EventHandler? StateChanged;

    public TourSessionService()
    {
        _currentSession = Load();
    }

    public TourSession? GetCurrentSession()
    {
        lock (_sync)
        {
            return Clone(_currentSession);
        }
    }

    public TourSession Start(Tour tour)
    {
        var orderedStops = tour.Stops
            .OrderBy(x => x.DisplayOrder)
            .ToList();

        var session = new TourSession
        {
            TourId = tour.Id,
            TourName = tour.Name,
            TourDescription = tour.Description,
            StartedAt = DateTimeOffset.UtcNow,
            Stops = orderedStops,
            CurrentStopIndex = orderedStops.Count == 0 ? -1 : 0,
            IsFinished = orderedStops.Count == 0
        };

        Save(session);
        return session;
    }

    public TourSession? CompleteCurrentStop()
    {
        lock (_sync)
        {
            if (_currentSession?.CurrentStop is not { } currentStop)
                return Clone(_currentSession);

            if (!_currentSession.CompletedStopIds.Contains(currentStop.Id))
                _currentSession.CompletedStopIds.Add(currentStop.Id);

            AdvanceCurrentStop(_currentSession);
            PersistLocked();
            NotifyChanged();
            return Clone(_currentSession);
        }
    }

    public void Cancel()
    {
        lock (_sync)
        {
            _currentSession = null;
            Preferences.Default.Remove(TourSessionKey);
        }

        NotifyChanged();
    }

    private void Save(TourSession session)
    {
        lock (_sync)
        {
            _currentSession = Clone(session);
            PersistLocked();
        }

        NotifyChanged();
    }

    private static void AdvanceCurrentStop(TourSession session)
    {
        var orderedStops = session.OrderedStops.ToList();
        var nextIndex = orderedStops.FindIndex(stop => !session.CompletedStopIds.Contains(stop.Id));

        if (nextIndex < 0)
        {
            session.CurrentStopIndex = -1;
            session.IsFinished = true;
            return;
        }

        session.CurrentStopIndex = nextIndex;
        session.IsFinished = false;
    }

    private void PersistLocked()
    {
        if (_currentSession is null)
        {
            Preferences.Default.Remove(TourSessionKey);
            return;
        }

        Preferences.Default.Set(
            TourSessionKey,
            JsonSerializer.Serialize(_currentSession));
    }

    private static TourSession? Load()
    {
        var raw = Preferences.Default.Get(TourSessionKey, string.Empty);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<TourSession>(raw);
        }
        catch
        {
            Preferences.Default.Remove(TourSessionKey);
            return null;
        }
    }

    private static TourSession? Clone(TourSession? session)
    {
        if (session is null)
            return null;

        return JsonSerializer.Deserialize<TourSession>(JsonSerializer.Serialize(session));
    }

    private void NotifyChanged()
        => StateChanged?.Invoke(this, EventArgs.Empty);
}
