namespace Geogrid.Generation;

/// <summary>Working coordinate system: a local equirectangular projection in meters around a centroid.</summary>
public sealed class LocalProjection
{
    public double CenterLat { get; }
    public double CenterLon { get; }
    private readonly double _cosLat;
    private const double R = 6_378_137.0;

    public LocalProjection(double centerLat, double centerLon)
    {
        CenterLat = centerLat;
        CenterLon = centerLon;
        _cosLat = Math.Cos(centerLat * Math.PI / 180.0);
    }

    public (double X, double Y) ToLocal(double lon, double lat)
    {
        var x = R * (lon - CenterLon) * Math.PI / 180.0 * _cosLat;
        var y = R * (lat - CenterLat) * Math.PI / 180.0;
        return (x, y);
    }

    public (double Lon, double Lat) ToWgs(double x, double y)
    {
        var lon = CenterLon + (x / (R * _cosLat)) * 180.0 / Math.PI;
        var lat = CenterLat + (y / R) * 180.0 / Math.PI;
        return (lon, lat);
    }
}
