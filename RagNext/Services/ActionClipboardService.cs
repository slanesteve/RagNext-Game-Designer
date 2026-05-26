using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using RagsCore.Actions;

namespace RagNext.Services
{
    public static class ActionClipboardService
    {
        public static string? CopiedNodeJson { get; private set; }
        public static string? CopiedNodeType { get; private set; } // "Action" or "ActionStep"

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        public static void Copy(object model)
        {
            if (model is RagsCore.Models.Action action)
            {
                CopiedNodeJson = JsonSerializer.Serialize(action, JsonOptions);
                CopiedNodeType = "Action";
            }
            else if (model is ActionStep step)
            {
                CopiedNodeJson = JsonSerializer.Serialize(step, JsonOptions);
                CopiedNodeType = "ActionStep";
            }
        }

        public static object? Paste()
        {
            if (string.IsNullOrEmpty(CopiedNodeJson)) return null;

            try
            {
                if (CopiedNodeType == "Action")
                {
                    var action = JsonSerializer.Deserialize<RagsCore.Models.Action>(CopiedNodeJson, JsonOptions);
                    if (action != null)
                    {
                        // Generate a completely fresh ID for the duplicate action so references don't overlap
                        var prop = action.GetType().GetProperty("Id");
                        if (prop != null && prop.CanWrite)
                        {
                            prop.SetValue(action, Guid.NewGuid());
                        }
                        return action;
                    }
                }
                else if (CopiedNodeType == "ActionStep")
                {
                    var step = JsonSerializer.Deserialize<ActionStep>(CopiedNodeJson, JsonOptions);
                    return step;
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Clipboard] Paste failed: {ex.Message}");
            }
            return null;
        }

        public static bool CanPaste => !string.IsNullOrEmpty(CopiedNodeJson);
    }
}
