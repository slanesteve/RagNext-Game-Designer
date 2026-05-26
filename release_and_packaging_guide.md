# RagNext Release, Packaging, & Architectural Guide

This guide documents the critical release workflows, packaging scripts, compiler flags, and design contracts for the **RagNext Designer** (MAUI) and **RagNext Player** (Unity). 

---

## 🛠️ 1. Windows Publishing & Packaging Workflow

### The Portable "Unpackaged" Release
By default, `.NET MAUI` publishes Windows apps as **packaged (MSIX)** applications. Running a packaged app's `.exe` directly from the folder causes a silent startup crash. 

To compile a fully standalone, **portable "green" build** that testers can double-click and run instantly:

1. **Clean prior builds** (to prevent cached assets/assemblies from contaminating the output):
   ```powershell
   Remove-Item -Recurse -Force Publish\Designer
   ```

2. **Publish as Unpackaged (`WindowsPackageType=None`)**:
   ```powershell
   dotnet publish RagNext/RagsNextDesigner.csproj -f net9.0-windows10.0.19041.0 -c Release -o Publish/Designer -p:WindowsPackageType=None
   ```

3. **Deploy the Unity Templates Folder**:
   Copy the fresh Unity standalone player builds into the published templates folder:
   ```powershell
   Remove-Item -Recurse -Force Publish\Designer\Templates
   Copy-Item -Recurse -Force Templates Publish\Designer\Templates
   ```

4. **Compress for Distribution**:
   ```powershell
   Compress-Archive -Path Publish\Designer\* -DestinationPath Publish\Designer.zip -Force
   ```

---

## 🎨 2. XAML Theme Bindings & Best Practices

### Prevent Startup Crashes (`DynamicResource` vs. `StaticResource`)
When adding custom custom-drawn subviews, controls, or splitters (e.g., `RightPaneLayout.xaml` or `ActionTreeView.xaml`):

> [!IMPORTANT]
> **Never** use `StaticResource` for global theme color keys (like `Gray200`, `Gray800`, `Gray100`, etc.) inside custom controls. 

* **Why?** Static resources force a compiler-time search. If a view inflates before `App.xaml` finishes merging Dracula, Gruvbox, or high-contrast theme dictionaries, the app will crash instantly on launch with a `XamlParseException`.
* **The Fix:** Natively utilize `DynamicResource` which defers resolution to runtime:
  ```xml
  <!-- Correct implementation -->
  <BoxView Color="{AppThemeBinding Light={DynamicResource Gray200}, Dark={DynamicResource Gray800}}" />
  ```

---

## ⚙️ 3. Global Functions & Timers Architecture

`GlobalFunction` and `GameTimer` inherit directly from `RagsCore.Models.Action`. This allows them to bind natively to the Designer's action tree structures.

* **Single Root Design:** Because a function or timer *is* itself an Action, it contains a list of steps directly.
* **UI Controls & Hiding "+ Action":** When a user is editing a Function or Timer, the `+ Action` button must be hidden (`IsVisible="{Binding CanAddAction}"` inside `ActionTreeView.xaml`). This prevents invalid secondary top-level actions from being added.
* **Name Synchronization:** The root node's `Name` in the tree view must listen to `PropertyChanged` of the `Action` model so that modifying the name in the **Properties** entry dynamically syncs with the **Actions & Events** tree root in real-time.
* **Root Deletion & Paste Protection:** Root action nodes inside functions and timers must be protected inside `ActionLibraryViewModel.cs` to prevent deletion or pasting overrides.

---

## 📂 4. The Unity Player Templates Directory Structure

The Unity Player shell relies on platform-neutral bundle packages (`Game.rags`). The `Templates` directory contains compiled builds of the Unity Player:

```
Templates/
 ├── Windows/         <-- Windows Unity Player (RagNextPlayer.exe, UnityPlayer.dll)
 ├── MacOS/           <-- MacOS Catalyst Unity Bundle (MyGame.app)
 ├── Linux/           <-- Linux Player build
 └── WebGL/           <-- WebGL Host Player build
```

When building or publishing, the Designer automatically expects to locate `Templates/{Platform}/` right next to its executing path to seamlessly bundle the exported project save payload.
