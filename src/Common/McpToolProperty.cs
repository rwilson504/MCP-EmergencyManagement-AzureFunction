namespace EmergencyManagementMCP.Common
{
    public class McpToolProperty
    {
        public required string Name { get; set; }
        public required string Type { get; set; }
        public required string Description { get; set; }
        public bool Required { get; set; } = false;
    }
}
