using Mapsui;
using Mapsui.Extensions;
using Mapsui.Layers;
using Mapsui.Nts;
using Mapsui.Projections;
using Mapsui.Styles;
using Microsoft.Maui.Devices.Sensors;
using NetTopologySuite.Geometries;
using VKFoodArea.Models;
using MauiLocation = Microsoft.Maui.Devices.Sensors.Location;
using MapBrush = Mapsui.Styles.Brush;
using MapColor = Mapsui.Styles.Color;
using MapPen = Mapsui.Styles.Pen;

namespace VKFoodArea.Features.Home;

public static class OfflineMapLayerFactory
{
    private static readonly GeometryFactory GeometryFactory = new(new PrecisionModel(), 3857);

    public static IReadOnlyList<ILayer> CreateVinhKhanhLayers()
    {
        return
        [
            CreateRiverLayer(),
            CreateAreaLayer(),
            CreateMinorRoadLayer(),
            CreateDiningStreetLayer(),
            CreateMainRoadLayer(),
            CreateVietnameseLabelLayer()
        ];
    }

    public static MauiLocation GetContentCenter(IReadOnlyList<Poi> pois)
    {
        if (pois.Count == 0)
            return new MauiLocation(10.7613135, 106.702667);

        return new MauiLocation(
            pois.Average(x => x.Latitude),
            pois.Average(x => x.Longitude));
    }

    public static bool IsNearAnyPoi(MauiLocation? location, IReadOnlyList<Poi> pois, double maxDistanceMeters)
    {
        if (location is null || pois.Count == 0)
            return false;

        return pois.Any(poi =>
            MauiLocation.CalculateDistance(
                location.Latitude,
                location.Longitude,
                poi.Latitude,
                poi.Longitude,
                DistanceUnits.Kilometers) * 1000 <= maxDistanceMeters);
    }

    private static MemoryLayer CreateAreaLayer()
    {
        var features = new List<IFeature>
        {
            CreatePolygonFeature(
                [
                    (106.70155, 10.76305),
                    (106.70725, 10.76282),
                    (106.70705, 10.76222),
                    (106.70135, 10.76242),
                    (106.70155, 10.76305)
                ],
                "#DDEFF7",
                "#C7DEE8"),
            CreatePolygonFeature(
                [
                    (106.70175, 10.76205),
                    (106.70705, 10.76182),
                    (106.70692, 10.76002),
                    (106.70185, 10.76005),
                    (106.70175, 10.76205)
                ],
                "#EEF6F2",
                "#D4E4DD"),
            CreatePolygonFeature(
                [
                    (106.70195, 10.76156),
                    (106.70702, 10.76128),
                    (106.70688, 10.76052),
                    (106.70212, 10.76062),
                    (106.70195, 10.76156)
                ],
                "#F9F1DD",
                "#E7DAB7")
        };

        return new MemoryLayer("Offline map areas")
        {
            Features = features
        };
    }

    private static MemoryLayer CreateRiverLayer()
    {
        var features = new List<IFeature>
        {
            CreatePolygonFeature(
                [
                    (106.70125, 10.76334),
                    (106.70762, 10.76312),
                    (106.70754, 10.76278),
                    (106.70135, 10.76298),
                    (106.70125, 10.76334)
                ],
                "#CFEAF5",
                "#B7D8E5")
        };

        return new MemoryLayer("Offline river")
        {
            Features = features
        };
    }

    private static MemoryLayer CreateDiningStreetLayer()
    {
        var features = new List<IFeature>
        {
            CreateRoadFeature(
                [
                    (106.70182, 10.76174),
                    (106.70255, 10.76139),
                    (106.70324, 10.76083),
                    (106.70418, 10.76064),
                    (106.70568, 10.76120),
                    (106.70696, 10.76082)
                ],
                20,
                "#FFF3C4",
                "#F3D46B")
        };

        return new MemoryLayer("Offline dining corridor")
        {
            Features = features
        };
    }

    private static MemoryLayer CreateMinorRoadLayer()
    {
        var features = new List<IFeature>
        {
            CreateRoadFeature([(106.70205, 10.76225), (106.70225, 10.76010)], 7, "#FDFDFB", "#D9E4DF"),
            CreateRoadFeature([(106.70335, 10.76220), (106.70352, 10.76008)], 7, "#FDFDFB", "#D9E4DF"),
            CreateRoadFeature([(106.70455, 10.76208), (106.70468, 10.76002)], 7, "#FDFDFB", "#D9E4DF"),
            CreateRoadFeature([(106.70578, 10.76202), (106.70590, 10.76000)], 7, "#FDFDFB", "#D9E4DF"),
            CreateRoadFeature([(106.70178, 10.76008), (106.70695, 10.76002)], 8, "#FDFDFB", "#D9E4DF"),
            CreateRoadFeature([(106.70170, 10.76238), (106.70710, 10.76220)], 8, "#FDFDFB", "#D9E4DF")
        };

        return new MemoryLayer("Offline minor roads")
        {
            Features = features
        };
    }

    private static MemoryLayer CreateMainRoadLayer()
    {
        var features = new List<IFeature>
        {
            CreateRoadFeature(
                [
                    (106.70195, 10.76178),
                    (106.70270, 10.76140),
                    (106.70330, 10.76072),
                    (106.70413, 10.76050),
                    (106.70570, 10.76117),
                    (106.70692, 10.76072)
                ],
                14,
                "#FFFFFF",
                "#BFD3CB"),
            CreateRoadFeature(
                [
                    (106.70145, 10.76302),
                    (106.70325, 10.76286),
                    (106.70555, 10.76270),
                    (106.70718, 10.76258)
                ],
                12,
                "#FFFFFF",
                "#C1D6E0"),
            CreateRoadFeature(
                [
                    (106.70155, 10.75955),
                    (106.70360, 10.75940),
                    (106.70560, 10.75936),
                    (106.70710, 10.75920)
                ],
                11,
                "#FFFFFF",
                "#D5DED9")
        };

        return new MemoryLayer("Offline main roads")
        {
            Features = features
        };
    }

    private static MemoryLayer CreateVietnameseLabelLayer()
    {
        var labels = new List<IFeature>
        {
            CreateLabelFeature("Ph\u1ed1 \u1ea9m th\u1ef1c V\u0129nh Kh\u00e1nh", 106.70440, 10.76105, "#173330", "#FDF8E8"),
            CreateLabelFeature("B\u1ebfn V\u00e2n \u0110\u1ed3n", 106.70465, 10.76282, "#467082", "#EAF6FB"),
            CreateLabelFeature("Qu\u1eadn 4", 106.70235, 10.76018, "#5C706B", "#F5FAF7")
        };

        return new MemoryLayer("Offline map labels")
        {
            Features = labels
        };
    }

    private static MemoryLayer CreateLabelLayer()
    {
        var labels = new List<IFeature>
        {
            CreateLabelFeature("Phố ẩm thực Vĩnh Khánh", 106.70440, 10.76105, "#173330", "#FDF8E8"),
            CreateLabelFeature("Bến Vân Đồn", 106.70465, 10.76282, "#467082", "#EAF6FB"),
            CreateLabelFeature("Quận 4", 106.70235, 10.76018, "#5C706B", "#F5FAF7")
        };

        return new MemoryLayer("Offline map labels")
        {
            Features = labels
        };
    }

    private static GeometryFeature CreatePolygonFeature(
        IReadOnlyList<(double Longitude, double Latitude)> points,
        string fillColor,
        string outlineColor)
    {
        var coordinates = points.Select(ToCoordinate).ToArray();
        var feature = new GeometryFeature
        {
            Geometry = GeometryFactory.CreatePolygon(coordinates)
        };

        feature.Styles.Add(new VectorStyle
        {
            Fill = new MapBrush(MapColor.FromString(fillColor)),
            Outline = new MapPen(MapColor.FromString(outlineColor), 1)
        });

        return feature;
    }

    private static GeometryFeature CreateRoadFeature(
        IReadOnlyList<(double Longitude, double Latitude)> points,
        double width,
        string lineColor,
        string outlineColor)
    {
        var feature = new GeometryFeature
        {
            Geometry = GeometryFactory.CreateLineString(points.Select(ToCoordinate).ToArray())
        };

        feature.Styles.Add(new VectorStyle
        {
            Line = new MapPen(MapColor.FromString(lineColor), width),
            Outline = new MapPen(MapColor.FromString(outlineColor), width + 3)
        });

        return feature;
    }

    private static PointFeature CreateLabelFeature(
        string text,
        double longitude,
        double latitude,
        string textColor,
        string backgroundColor)
    {
        var point = SphericalMercator
            .FromLonLat(longitude, latitude)
            .ToMPoint();

        var feature = new PointFeature(point);
        feature.Styles.Add(new LabelStyle
        {
            Text = text,
            ForeColor = MapColor.FromString(textColor),
            BackColor = new MapBrush(MapColor.FromString(backgroundColor)),
            Halo = new MapPen(MapColor.White, 2),
            HorizontalAlignment = LabelStyle.HorizontalAlignmentEnum.Center,
            VerticalAlignment = LabelStyle.VerticalAlignmentEnum.Center,
            CollisionDetection = true
        });

        return feature;
    }

    private static Coordinate ToCoordinate((double Longitude, double Latitude) point)
    {
        var mercatorPoint = SphericalMercator
            .FromLonLat(point.Longitude, point.Latitude)
            .ToMPoint();

        return new Coordinate(mercatorPoint.X, mercatorPoint.Y);
    }
}
