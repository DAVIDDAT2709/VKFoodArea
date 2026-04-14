namespace VKFoodArea.Web.ViewModels;

public class AdminMapViewModel
{
    public List<AdminMapPoiViewModel> ActivePois { get; set; } = new();
    public AdminMapAnalyticsViewModel Analytics { get; set; } = new();
    public bool HasMapData => ActivePois.Count > 0 || Analytics.HeatmapPoints.Count > 0 || Analytics.Routes.Count > 0;
}

public class AdminMapPoiViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public double RadiusMeters { get; set; }
    public int Priority { get; set; }
}

public class AdminMapAnalyticsViewModel
{
    public int TotalMovementLogs { get; set; }
    public int AnalyzedMovementLogCount { get; set; }
    public int AnonymousVisitorCount { get; set; }
    public int AnonymousRouteCount { get; set; }
    public int UnassignedMovementLogCount { get; set; }
    public DateTime? LatestMovementAt { get; set; }
    public double AverageListenSeconds { get; set; }
    public int AverageListenSampleCount { get; set; }
    public string AnalyticsWindowLabel { get; set; } = string.Empty;
    public List<TopPoiPerformanceViewModel> TopPois { get; set; } = new();
    public List<AdminMapRouteViewModel> Routes { get; set; } = new();
    public List<AdminMapHeatPointViewModel> HeatmapPoints { get; set; } = new();
}

public class AdminMapRouteViewModel
{
    public string RouteId { get; set; } = string.Empty;
    public string RouteLabel { get; set; } = string.Empty;
    public string SourceSummary { get; set; } = string.Empty;
    public int PointCount { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime EndedAt { get; set; }
    public double DurationMinutes { get; set; }
    public double ApproxDistanceMeters { get; set; }
    public List<AdminMapGeoPointViewModel> Points { get; set; } = new();
}

public class AdminMapGeoPointViewModel
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public DateTime RecordedAt { get; set; }
    public double? AccuracyMeters { get; set; }
}

public class AdminMapHeatPointViewModel
{
    public double Latitude { get; set; }
    public double Longitude { get; set; }
    public int Count { get; set; }
    public double Weight { get; set; }
}
