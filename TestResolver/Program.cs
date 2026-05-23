using System;
using RagsCore.Models;
using RagsCore.Actions;
using RagsCore.Services;

class Program
{
    static void Main()
    {
        var game = new Game();
        game.Player.Name = "Joe Bilbo";
        game.Player.Description = "A fluffy rabbit protagonist.";
        
        var ctx = new ActionContext(game, currentRoom: null, focusEntity: game.Player);
        
        string template = "Your name is {this.Name} and your description is: {this.Description}";
        string resolved = TemplateResolver.Resolve(template, ctx);
        
        Console.WriteLine($"Template: {template}");
        Console.WriteLine($"Resolved: {resolved}");
    }
}
