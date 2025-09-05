using System.Collections.Generic;
using System.Reflection;

namespace EmergencyManagementMCP.Common
{
    public static class McpToolParameterHelper
    {
        public static Dictionary<string, object> BuildParametersFromRequest(object request, IReadOnlyList<McpToolProperty> properties)
        {
            var dict = new Dictionary<string, object>();
            var type = request.GetType();
            foreach (var prop in properties)
            {
                var pi = type.GetProperty(prop.Name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);
                if (pi != null)
                {
                    dict[prop.Name] = pi.GetValue(request);
                }
            }
            return dict;
        }
    }
}
