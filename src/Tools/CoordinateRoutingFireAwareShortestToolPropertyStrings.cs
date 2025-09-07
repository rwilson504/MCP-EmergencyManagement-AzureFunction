namespace EmergencyManagementMCP.Tools
{
    public static class CoordinateRoutingFireAwareShortestToolPropertyStrings
    {
        public const string OriginLatName = "OriginLat";
        public const string OriginLatType = "double";
        public const string OriginLatDescription = "Origin latitude.";

        public const string OriginLonName = "OriginLon";
        public const string OriginLonType = "double";
        public const string OriginLonDescription = "Origin longitude.";

        public const string DestinationLatName = "DestinationLat";
        public const string DestinationLatType = "double";
        public const string DestinationLatDescription = "Destination latitude.";

        public const string DestinationLonName = "DestinationLon";
        public const string DestinationLonType = "double";
        public const string DestinationLonDescription = "Destination longitude.";

        public const string AvoidBufferMetersName = "AvoidBufferMeters";
        public const string AvoidBufferMetersType = "double";
        public const string AvoidBufferMetersDescription = "Buffer distance in meters around fire perimeters to avoid. Default is 2000.";

        public const string DepartAtIsoUtcName = "DepartAtIsoUtc";
        public const string DepartAtIsoUtcType = "string";
        public const string DepartAtIsoUtcDescription = "Optional departure time in ISO 8601 UTC format (e.g., 2023-08-01T15:30:00Z).";

        public const string ProfileName = "Profile";
        public const string ProfileType = "string";
        public const string ProfileDescription = "Routing profile (e.g., driving, walking). Default is driving.";
    }
}
