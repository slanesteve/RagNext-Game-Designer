using System;
using System.IO;
using System.Reflection;
using System.Windows.Input;

class Program
{
    static void Main()
    {
        string dllPath = @"c:\Users\steve\source\repos\RagNext\RagNext.Designer.Avalonia\bin\Debug\net9.0\RagNext.dll";
        var assembly = Assembly.LoadFrom(dllPath);
        var viewModelType = assembly.GetType("RagNext.Designer.Avalonia.ViewModels.MainWindowViewModel")!;
        
        // Instantiate
        object viewModel = Activator.CreateInstance(viewModelType)!;
        Console.WriteLine("Instantiated MainWindowViewModel successfully.");

        // Helper to check command
        CheckCommand(viewModel, "CloseAttributeDialogCommand");
        CheckCommand(viewModel, "SaveAttributeCommand");
        CheckCommand(viewModel, "CloseActionSelectorCommand");
        CheckCommand(viewModel, "SelectActionTemplateCommand");
    }

    static void CheckCommand(object viewModel, string propertyName)
    {
        var prop = viewModel.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        if (prop == null)
        {
            Console.WriteLine($"Property {propertyName} not found!");
            return;
        }

        var command = prop.GetValue(viewModel) as ICommand;
        if (command == null)
        {
            Console.WriteLine($"Property {propertyName} value is null or not ICommand!");
            return;
        }

        Console.WriteLine($"\nCommand: {propertyName}");
        Console.WriteLine($"  Type: {command.GetType().FullName}");
        try
        {
            bool canExec = command.CanExecute(null);
            Console.WriteLine($"  CanExecute(null): {canExec}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  CanExecute(null) threw: {ex.Message}");
        }
    }
}
