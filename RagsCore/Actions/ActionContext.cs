using System;
using System.Linq;
using RagsCore.Models;

namespace RagsCore.Actions
{
    // Execution context passed to conditions and commands.
    public sealed class ActionContext
    {
        public Game Game { get; }
        public Room? CurrentRoom { get; }
        public Player Player => Game.Player;
        public GameObject? FocusObject { get; }
        public object? FocusEntity { get; }

        public ActionContext(Game game, Room? currentRoom = null, GameObject? focusObject = null, object? focusEntity = null)
        {
            Game = game ?? throw new ArgumentNullException(nameof(game));
            CurrentRoom = currentRoom;
            FocusObject = focusObject;
            FocusEntity = focusEntity ?? focusObject;
        }

        public GameVariable? GetVariable(string name)
        {
            if (name.Contains(':'))
            {
                var index = name.IndexOf(':');
                var realName = name.Substring(0, index);
                var modifier = name.Substring(index + 1).ToLowerInvariant();
                var baseVar = Game.Variables.FirstOrDefault(v => string.Equals(v.Name, realName, StringComparison.Ordinal));
                if (baseVar != null && DateTime.TryParse(baseVar.Value, out var dt))
                {
                    string? val = modifier switch
                    {
                        "year" => dt.Year.ToString(),
                        "month" => dt.Month.ToString(),
                        "day" => dt.Day.ToString(),
                        "hour" => dt.Hour.ToString(),
                        "minute" => dt.Minute.ToString(),
                        "second" => dt.Second.ToString(),
                        "dayofweek" => ((int)dt.DayOfWeek).ToString(),
                        "date" => dt.ToString("yyyy-MM-dd"),
                        "time" => dt.ToString("HH:mm:ss"),
                        "datetime" => dt.ToString("yyyy-MM-ddTHH:mm:ss"),
                        _ => null
                    };
                    if (val != null)
                    {
                        return new GameVariable { Name = name, Value = val, Type = "int" };
                    }
                }
            }
            return Game.Variables.FirstOrDefault(v => string.Equals(v.Name, name, StringComparison.Ordinal));
        }

        public void SetVariable(string name, string? value)
        {
            if (string.IsNullOrEmpty(name)) return;

            var cleanName = name;
            if (cleanName.StartsWith("{") && cleanName.EndsWith("}"))
            {
                cleanName = cleanName.Substring(1, cleanName.Length - 2);
            }

            if (cleanName.StartsWith("variables.", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = cleanName.Substring(10);
            }
            else if (cleanName.StartsWith("variable.", StringComparison.OrdinalIgnoreCase))
            {
                cleanName = cleanName.Substring(9);
            }

            var parts = cleanName.Split('.');
            if (parts.Length >= 3)
            {
                var baseVar = Game.Variables.FirstOrDefault(v => string.Equals(v.Name, parts[0], StringComparison.OrdinalIgnoreCase));
                if (baseVar != null && (string.Equals(baseVar.Type, "array", StringComparison.OrdinalIgnoreCase) || baseVar.Columns.Count > 0))
                {
                    int rowIndex = -1;
                    string colName = "";
                    if (int.TryParse(parts[1], out var idx1))
                    {
                        rowIndex = idx1;
                        colName = parts[2];
                    }
                    else if (int.TryParse(parts[2], out var idx2))
                    {
                        rowIndex = idx2;
                        colName = parts[1];
                    }

                    if (rowIndex >= 0 && rowIndex < baseVar.Rows.Count)
                    {
                        int colIdx = baseVar.Columns.IndexOf(baseVar.Columns.FirstOrDefault(c => string.Equals(c, colName, StringComparison.OrdinalIgnoreCase)));
                        if (colIdx >= 0)
                        {
                            var row = baseVar.Rows[rowIndex];
                            while (row.Count <= colIdx) row.Add(string.Empty);
                            row[colIdx] = value ?? string.Empty;
                            return;
                        }
                    }
                }
            }

            var v = GetVariable(cleanName);
            if (v is null)
            {
                v = new GameVariable { Name = cleanName, Value = value };
                Game.Variables.Add(v);
            }
            else
            {
                v.Value = value;
            }
        }
    }
}