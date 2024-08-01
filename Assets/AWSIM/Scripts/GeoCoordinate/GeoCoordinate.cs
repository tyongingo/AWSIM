namespace AWSIM.Geographic
{
[System.Serializable]
public class GeoCoordinate
{
  public double Latitude { get; }
  public double Longitude { get; }
  public double Altitude { get; }

  public GeoCoordinate()
  {
    Latitude = 0;
    Longitude = 0;
    Altitude = 0;
  }

  public GeoCoordinate(double latitude, double longitude, double altitude)
  {
    Latitude = latitude;
    Longitude = longitude;
    Altitude = altitude;
  }
}
}
