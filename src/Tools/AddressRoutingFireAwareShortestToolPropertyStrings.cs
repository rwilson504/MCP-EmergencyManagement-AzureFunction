namespace EmergencyManagementMCP.Tools
{
    public static class AddressRoutingFireAwareShortestToolPropertyStrings
    {
        public const string OriginAddressName = "OriginAddress";
        public const string OriginAddressType = "string";
        public const string OriginAddressDescription = "Origin address (e.g., '123 Main St, Los Angeles, CA').";

        public const string DestinationAddressName = "DestinationAddress";
        public const string DestinationAddressType = "string";
        public const string DestinationAddressDescription = "Destination address (e.g., '456 Oak Ave, Los Angeles, CA').";

        public const string AvoidBufferMetersName = "AvoidBufferMeters";
        public const string AvoidBufferMetersType = "double";
        public const string AvoidBufferMetersDescription = "Buffer distance in meters around fire perimeters to avoid. Default is 2000.";

        public const string DepartAtIsoUtcName = "DepartAtIsoUtc";
        public const string DepartAtIsoUtcType = "string";
        public const string DepartAtIsoUtcDescription = "Optional departure time in ISO 8601 UTC format (e.g., 2023-08-01T15:30:00Z).";

        public const string ProfileName = "Profile";
        public const string ProfileType = "string";
        public const string ProfileDescription = "Routing profile (e.g., driving, walking). Default is driving.";

        public const string PersistShareLinkName = "PersistShareLink";
        public const string PersistShareLinkType = "boolean";
        public const string PersistShareLinkDescription = "If true, create a shareable link artifact for this route (stored in blob).";

        public const string ShareLinkTtlMinutesName = "ShareLinkTtlMinutes";
        public const string ShareLinkTtlMinutesType = "int";
        public const string ShareLinkTtlMinutesDescription = "Optional TTL in minutes for the share link (default 1440 = 24h).";
    }
}