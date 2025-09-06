namespace EmergencyManagementMCP.Common
{
    public class McpToolProperty
    {
        [Required]
        public string Name { get; set; }
        [Required]
        public string Type { get; set; }
        [Required]
        public string Description { get; set; }
        public bool Required { get; set; } = false;
    }
}
