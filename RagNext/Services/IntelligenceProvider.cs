using System;
using System.Collections.Generic;
using System.Linq;
using RagsCore.Models;

namespace RagNext.Services
{
    public static class IntelligenceProvider
    {
        public static List<SuggestionItem> GetSuggestions(Game? game)
        {
            var list = new List<SuggestionItem>();

            // 0. This (Intuitive self-referencing keyword)
            list.Add(new SuggestionItem
            {
                Token = "this.Name",
                DisplayText = "this.Name",
                TypeName = "Current Object Property",
                Description = "Name of this object."
            });
            list.Add(new SuggestionItem
            {
                Token = "this.Description",
                DisplayText = "this.Description",
                TypeName = "Current Object Property",
                Description = "Description of this object."
            });
            list.Add(new SuggestionItem
            {
                Token = "this.portrait",
                DisplayText = "this.portrait",
                TypeName = "Current Object Property",
                Description = "Portrait or image path of this object."
            });

            if (game != null)
            {
                var objAttrNames = game.Objects
                    .SelectMany(o => o.Attributes)
                    .Select(a => a.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct();

                foreach (var attrName in objAttrNames)
                {
                    list.Add(new SuggestionItem
                    {
                        Token = $"this.attributes.{attrName}",
                        DisplayText = $"this.attributes.{attrName}",
                        TypeName = "Current Object Attribute",
                        Description = $"Custom attribute of this object."
                    });
                }
            }

            // 1. Player
            list.Add(new SuggestionItem
            {
                Token = "player.Name",
                DisplayText = "player.Name",
                TypeName = "Player Property",
                Description = "Name of the protagonist."
            });
            list.Add(new SuggestionItem
            {
                Token = "player.Description",
                DisplayText = "player.Description",
                TypeName = "Player Property",
                Description = "Full biography or description of the protagonist."
            });
            list.Add(new SuggestionItem
            {
                Token = "player.Gender",
                DisplayText = "player.Gender",
                TypeName = "Player Property",
                Description = "Gender of the protagonist."
            });
            list.Add(new SuggestionItem
            {
                Token = "player.portrait",
                DisplayText = "player.portrait",
                TypeName = "Player Property",
                Description = "Portrait or image path of the protagonist."
            });

            if (game?.Player != null)
            {
                foreach (var attr in game.Player.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
                {
                    list.Add(new SuggestionItem
                    {
                        Token = $"player.attributes.{attr.Name}",
                        DisplayText = $"player.attributes.{attr.Name}",
                        TypeName = "Player Attribute",
                        Description = $"Custom player attribute. Current: {attr.Value}"
                    });
                }
            }

            // 2. Room
            list.Add(new SuggestionItem
            {
                Token = "room.Name",
                DisplayText = "room.Name",
                TypeName = "Room Property",
                Description = "Name of the current room."
            });
            list.Add(new SuggestionItem
            {
                Token = "room.Description",
                DisplayText = "room.Description",
                TypeName = "Room Property",
                Description = "Description of the current room."
            });
            list.Add(new SuggestionItem
            {
                Token = "room.portrait",
                DisplayText = "room.portrait",
                TypeName = "Room Property",
                Description = "Portrait or image path of the current room."
            });
            
            if (game != null)
            {
                var roomAttrNames = game.Rooms
                    .SelectMany(r => r.Attributes)
                    .Select(a => a.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct();

                foreach (var attrName in roomAttrNames)
                {
                    list.Add(new SuggestionItem
                    {
                        Token = $"room.attributes.{attrName}",
                        DisplayText = $"room.attributes.{attrName}",
                        TypeName = "Room Attribute",
                        Description = $"Room attribute defined in your rooms."
                    });
                }
            }

            // 3. Focus / Object
            list.Add(new SuggestionItem
            {
                Token = "focus.Name",
                DisplayText = "focus.Name",
                TypeName = "Focus Object Property",
                Description = "Name of the current focus object."
            });
            list.Add(new SuggestionItem
            {
                Token = "focus.Description",
                DisplayText = "focus.Description",
                TypeName = "Focus Object Property",
                Description = "Description of the current focus object."
            });
            list.Add(new SuggestionItem
            {
                Token = "focus.portrait",
                DisplayText = "focus.portrait",
                TypeName = "Focus Object Property",
                Description = "Portrait or image path of the current focus object."
            });
            if (game != null)
            {
                var objAttrNames = game.Objects
                    .SelectMany(o => o.Attributes)
                    .Select(a => a.Name)
                    .Where(n => !string.IsNullOrWhiteSpace(n))
                    .Distinct();

                foreach (var attrName in objAttrNames)
                {
                    list.Add(new SuggestionItem
                    {
                        Token = $"focus.attributes.{attrName}",
                        DisplayText = $"focus.attributes.{attrName}",
                        TypeName = "Object Attribute",
                        Description = $"Focus object attribute."
                    });
                }
            }

            // 4. Variables
            if (game != null)
            {
                foreach (var variable in game.Variables.Where(v => !string.IsNullOrWhiteSpace(v.Name)))
                {
                    list.Add(new SuggestionItem
                    {
                        Token = $"variables.{variable.Name}",
                        DisplayText = $"variables.{variable.Name}",
                        TypeName = "Global Variable",
                        Description = $"State variable ({variable.Type}). Current: {variable.Value}"
                    });
                }
            }

            // 5. Characters
            if (game != null)
            {
                foreach (var character in game.Characters.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
                {
                    var nameClean = character.Name.Replace(" ", "");
                    list.Add(new SuggestionItem
                    {
                        Token = $"characters.{nameClean}.Name",
                        DisplayText = $"characters.{nameClean}.Name",
                        TypeName = "Character Property",
                        Description = $"Name of character '{character.Name}'."
                    });
                    list.Add(new SuggestionItem
                    {
                        Token = $"characters.{nameClean}.Description",
                        DisplayText = $"characters.{nameClean}.Description",
                        TypeName = "Character Property",
                        Description = $"Description of character '{character.Name}'."
                    });
                    list.Add(new SuggestionItem
                    {
                        Token = $"characters.{nameClean}.Health",
                        DisplayText = $"characters.{nameClean}.Health",
                        TypeName = "Character Property",
                        Description = $"Health of character '{character.Name}'."
                    });
                    list.Add(new SuggestionItem
                    {
                        Token = $"characters.{nameClean}.portrait",
                        DisplayText = $"characters.{nameClean}.portrait",
                        TypeName = "Character Property",
                        Description = $"Portrait or image path of character '{character.Name}'."
                    });

                    foreach (var attr in character.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
                    {
                        list.Add(new SuggestionItem
                        {
                            Token = $"characters.{nameClean}.attributes.{attr.Name}",
                            DisplayText = $"characters.{nameClean}.attributes.{attr.Name}",
                            TypeName = "Character Attribute",
                            Description = $"Attribute of character '{character.Name}'."
                        });
                    }
                }
            }

            // 6. Objects
            if (game != null)
            {
                foreach (var obj in game.Objects.Where(o => !string.IsNullOrWhiteSpace(o.Name)))
                {
                    var nameClean = obj.Name.Replace(" ", "");
                    list.Add(new SuggestionItem
                    {
                        Token = $"objects.{nameClean}.Name",
                        DisplayText = $"objects.{nameClean}.Name",
                        TypeName = "Object Property",
                        Description = $"Name of object '{obj.Name}'."
                    });
                    list.Add(new SuggestionItem
                    {
                        Token = $"objects.{nameClean}.Description",
                        DisplayText = $"objects.{nameClean}.Description",
                        TypeName = "Object Property",
                        Description = $"Description of object '{obj.Name}'."
                    });
                    list.Add(new SuggestionItem
                    {
                        Token = $"objects.{nameClean}.portrait",
                        DisplayText = $"objects.{nameClean}.portrait",
                        TypeName = "Object Property",
                        Description = $"Portrait or image path of object '{obj.Name}'."
                    });

                    foreach (var attr in obj.Attributes.Where(a => !string.IsNullOrWhiteSpace(a.Name)))
                    {
                        list.Add(new SuggestionItem
                        {
                            Token = $"objects.{nameClean}.attributes.{attr.Name}",
                            DisplayText = $"objects.{nameClean}.attributes.{attr.Name}",
                            TypeName = "Object Attribute",
                            Description = $"Attribute of object '{obj.Name}'."
                        });
                    }
                }
            }

            return list;
        }

        public static List<SuggestionItem> GetEntitySuggestions(Game? game)
        {
            var list = new List<SuggestionItem>();

            // 1. Directions / Exits
            var directions = new[] { "North", "South", "East", "West", "Up", "Down", "In", "Out" };
            foreach (var dir in directions)
            {
                list.Add(new SuggestionItem
                {
                    Token = dir,
                    DisplayText = dir,
                    TypeName = "Exit Direction",
                    Description = $"Clickable exit shortcut in player navigation."
                });
            }

            if (game != null)
            {
                // 2. Objects
                foreach (var obj in game.Objects.Where(o => !string.IsNullOrWhiteSpace(o.Name)))
                {
                    list.Add(new SuggestionItem
                    {
                        Token = obj.Name,
                        DisplayText = obj.Name,
                        TypeName = "Game Object",
                        Description = $"Interactive inline link to object '{obj.Name}'."
                    });
                }

                // 3. Characters
                foreach (var ch in game.Characters.Where(c => !string.IsNullOrWhiteSpace(c.Name)))
                {
                    list.Add(new SuggestionItem
                    {
                        Token = ch.Name,
                        DisplayText = ch.Name,
                        TypeName = "Character",
                        Description = $"Interactive inline link to character '{ch.Name}'."
                    });
                }

                // 4. Rooms
                foreach (var room in game.Rooms.Where(r => !string.IsNullOrWhiteSpace(r.Name)))
                {
                    list.Add(new SuggestionItem
                    {
                        Token = room.Name,
                        DisplayText = room.Name,
                        TypeName = "Room",
                        Description = $"Navigation/travel link to room '{room.Name}'."
                    });
                }
            }

            return list;
        }
    }

    public class SuggestionItem
    {
        public string Token { get; set; } = string.Empty;
        public string DisplayText { get; set; } = string.Empty;
        public string TypeName { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
    }
}
