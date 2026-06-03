using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using RagsCore.Actions;
using RagsCore;

namespace RagNext.Designer.Avalonia.Services
{
    public static class ActionClipboardService
    {
        public static string? CopiedNodeJson { get; private set; }
        public static string? CopiedNodeType { get; private set; } // "Action" or "ActionStep"

        public static void Copy(object model)
        {
            if (model is RagsCore.Models.Action action)
            {
                CopiedNodeJson = JsonSerializer.Serialize(action, RagsJsonContext.CustomDefault.Action);
                CopiedNodeType = "Action";
            }
            else if (model is ActionStep step)
            {
                CopiedNodeJson = JsonSerializer.Serialize(step, RagsJsonContext.CustomDefault.ActionStep);
                CopiedNodeType = "ActionStep";
            }
        }

        public static object? Paste()
        {
            if (string.IsNullOrEmpty(CopiedNodeJson)) return null;

            try
            {
                var normalizedJson = ActionStep.NormalizeLegacyDiscriminators(CopiedNodeJson);
                if (CopiedNodeType == "Action")
                {
                    var action = JsonSerializer.Deserialize(normalizedJson, RagsJsonContext.CustomDefault.Action);
                    if (action != null)
                    {
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
                    var step = JsonSerializer.Deserialize(normalizedJson, RagsJsonContext.CustomDefault.ActionStep);
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
