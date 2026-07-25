using System;
using System.Windows.Input;

namespace RagNext.Designer.Avalonia.Models
{
    public class ValidationErrorItem
    {
        public string Message { get; set; } = string.Empty;
        public string Severity { get; set; } = "Error"; // "Error", "Warning", "Info"
        public string Category { get; set; } = "General"; // "Room", "Object", "Character", "Function", "Timer", "Variable", "Media", "General"
        public Guid? TargetId { get; set; }
        public string? TargetName { get; set; }
        public ICommand? JumpToCommand { get; set; }

        public string SeverityIcon => Severity switch
        {
            "Error" => "❌",
            "Warning" => "⚠️",
            "Info" => "ℹ️",
            _ => "📌"
        };

        public string SeverityColor => Severity switch
        {
            "Error" => "#FF5252",
            "Warning" => "#FFB74D",
            "Info" => "#64B5F6",
            _ => "#E0E0E0"
        };

        public bool HasTarget => TargetId.HasValue || (Category != "General" && Category != "System");
    }
}
