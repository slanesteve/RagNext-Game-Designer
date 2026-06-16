# RagNext Release, Packaging, & Architectural Guide

This guide documents the critical release workflows, packaging scripts, compiler flags, and design contracts for the **RagNext Designer** (Avalonia) and **RagNext Player** (Unity). 

---

## 🛠️ 1. Windows NativeAOT Publishing & Packaging Workflow

### Standalone High-Performance NativeAOT Release
The new Avalonia designer is built to support **NativeAOT (Ahead-of-Time)** compilation. NativeAOT compiles the C# codebase directly into a platform-native, highly-optimized standalone machine executable (`RagNext.Designer.Avalonia.exe`). 

This completely eliminates IL assembly shipping, provides extreme startup performance, guarantees code protection, and runs directly as a portable "green" executable without requiring a .NET runtime installed on the user's system.

To compile a fully standalone, **AOT-compiled Windows distribution build**:

1. **Clean prior builds** (to prevent cached assets or intermediate IL assemblies from contaminating the packaging):
   ```powershell
   Remove-Item -Recurse -Force Publish\Designer
   ```

2. **Publish with NativeAOT Enabled (`-p:PublishAot=true`)**:
   Provide the platform runtime identifier (`-r win-x64`), optimize for release (`-c Release`), and compile into the distribution directory:
   ```powershell
   dotnet publish RagNext.Designer.Avalonia/RagNext.Designer.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=true -o Publish/Designer
   ```

3. **Deploy the Web Assets & Templates Folders**:
   Ensure the HTML5 graph editors and Unity standalone player builds are correctly copied adjacent to the standalone binary:
   ```powershell
   Remove-Item -Recurse -Force Publish\Designer\WebAssets
   Copy-Item -Recurse -Force RagNext.Designer.Avalonia\WebAssets Publish\Designer\WebAssets

   Remove-Item -Recurse -Force Publish\Designer\Templates
   Copy-Item -Recurse -Force Templates Publish\Designer\Templates
   ```

### ⚠️ NuGet Multi-Targeting Restore Workaround
Because the core library `RagsCore` is multi-targeted (Android, iOS, macOS, Windows), standard `dotnet publish` commands will attempt to restore mobile platform workloads and SDK runtimes. If the development machine is missing these specific mobile SDK configurations or matches a different .NET patch version, the NuGet restore phase will fail.

To resolve this seamlessly, temporarily toggle `RagsCore.csproj` to standard `net9.0` desktop targeting before publishing, and restore it immediately afterward.

#### 🚀 Automated PowerShell Packaging Command:
Run this single combined command chain in PowerShell to automate the entire switch, NativeAOT publish, resource copy, and restore process cleanly:
```powershell
# 1. Temporarily target net9.0 only to bypass mobile Mono restores
(Get-Content RagsCore\RagsCore.csproj) -replace '<TargetFrameworks>.*</TargetFrameworks>', '<TargetFramework>net9.0</TargetFramework>' | Set-Content RagsCore\RagsCore.csproj

# 2. Run publish and asset copying
Remove-Item -Recurse -Force Publish\Designer
dotnet publish RagNext.Designer.Avalonia/RagNext.Designer.Avalonia.csproj -c Release -r win-x64 --self-contained true -p:PublishAot=true -o Publish/Designer
New-Item -ItemType Directory -Force Publish\Designer\WebAssets
Copy-Item -Recurse -Force RagNext.Designer.Avalonia\WebAssets\* Publish\Designer\WebAssets\
if (Test-Path Templates) { New-Item -ItemType Directory -Force Publish\Designer\Templates; Copy-Item -Recurse -Force Templates\* Publish\Designer\Templates\ }
Compress-Archive -Path Publish\Designer\* -DestinationPath Publish\Designer.zip -Force

# 3. Restore RagsCore back to multi-targeting
(Get-Content RagsCore\RagsCore.csproj) -replace '<TargetFramework>net9.0</TargetFramework>', '<TargetFrameworks>net9.0;net9.0-android;net9.0-ios;net9.0-maccatalyst;net9.0-windows10.0.19041.0</TargetFrameworks>' | Set-Content RagsCore\RagsCore.csproj
```

---

## 🎨 2. Avalonia XAML Theme Bindings & Best Practices

### Dynamic Resourcing & Styling Consistency
Unlike MAUI, Avalonia utilizes advanced CSS-like Styles and dynamic Resource Dictionaries. When adding custom-drawn subviews, controls, or panel splitters (e.g. inside `MainWindow.axaml`):

> [!IMPORTANT]
> **Always** bind styling brush values to global theme keys (like `ThemeBackgroundBrush`, `ThemeBorderLowBrush`, `ThemeForegroundBrush`) using `DynamicResource` rather than `StaticResource`.

* **Why?** `StaticResource` performs a compile-time resolution. If resources are overridden at runtime (e.g., dynamically switching between Dracula, Dark, or High-Contrast palettes), static resources will fail to update. If resources are evaluated during UI assembly initialization before the dictionaries have merged, the designer will crash instantly on startup.
* **The Fix:** Utilize runtime-evaluated `DynamicResource` to support seamless runtime theme switching:
  ```xml
  <!-- Correct responsive implementation in Avalonia -->
  <Border Background="{DynamicResource ThemeBackgroundBrush}"
          BorderBrush="{DynamicResource ThemeBorderLowBrush}"
          BorderThickness="1" />
  ```

---

## ⚙️ 3. Global Functions & Timers Architecture

`GlobalFunction` and `GameTimer` inherit directly from `RagsCore.Models.Action`. This allows them to bind natively to the Designer's action tree structures.

* **Single Root Design:** Because a function or timer *is* itself an Action, it contains a list of steps directly.
* **UI Controls & Hiding "+ Action":** When a user is editing a Function or Timer, the `+ Action` button must be hidden (`IsVisible="{Binding CanAddAction}"` inside `MainWindow.axaml`). This prevents invalid secondary top-level actions from being added.
* **Name Synchronization:** The root node's `Name` in the tree view listens to `PropertyChanged` of the `Action` model so that modifying the name in the **Properties** entry dynamically syncs with the **Actions & Events** tree root in real-time.
* **Root Deletion & Paste Protection:** Root action nodes inside functions and timers are protected inside `ActionLibraryViewModel.cs` to prevent deletion or pasting overrides.

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

---

## 🔒 5. NativeAOT & WebView Airspace Best Practices

When adding views, buttons, or native browser components (`NativeWebView`) to the Designer, follow these strict rules to ensure compatibility with macOS NativeAOT compilation and prevent UI hit-testing/click-blocking bugs:

### 1. Programmatic Delegate Click Subscriptions (Anti-Trimming)
Avoid defining click event handlers directly in XAML (e.g. `Click="OnSaveClicked"`) for critical overlay controls, dialogs, or settings buttons.
* **Problem**: Under NativeAOT, the compiler trims methods that are only referenced in XAML by string, which silences click events completely.
* **Solution**: Assign an `x:Name` in XAML and register click handlers programmatically in the C# code-behind constructor:
  ```csharp
  var saveBtn = this.FindControl<Button>("SaveButton");
  if (saveBtn != null) saveBtn.Click += OnSaveClicked;
  ```

### 2. Dynamic WebView Mounting (Anti-Airspace Blocking)
Native web browser views (`NativeWebView` hosted via `WKWebView`/`NativeControlHost` on macOS) do not honor standard visual properties like `IsVisible="False"`, parent opacity `Opacity="0.0"`, or parent bounds sizing like `Width="1" Height="1"` inside the native OS window manager. They remain active and intercept all pointer click events in their target regions.
* **Problem**: An inactive webview will invisibly cover overlay panels and block clicks.
* **Solution**: Physically mount and unmount WebViews to and from the visual tree dynamically. Detach them on startup and when hidden, and only insert them into their parent grid/border container when they are actively displayed.
  ```csharp
  // Detach / Unmount
  if (CanvasWebView.Parent is Border parent) parent.Child = null;

  // Attach / Mount
  if (CanvasWebView.Parent == null) container.Child = CanvasWebView;
  ```
