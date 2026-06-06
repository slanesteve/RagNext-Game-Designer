$path = "c:\Users\steve\source\repos\RagNext\RagNext.Designer.Avalonia\Views\MainWindow.axaml"
$content = Get-Content $path -Raw

# Replace formatting buttons foreground/background
$content = $content -replace 'Background="#262635" Foreground="White"', 'Background="{DynamicResource ToolbarBtnBg}" Foreground="{DynamicResource ToolbarBtnFg}" BorderBrush="{DynamicResource ToolbarBtnBorder}" BorderThickness="1"'
$content = $content -replace 'Background="#262635"', 'Background="{DynamicResource ToolbarBtnBg}" Foreground="{DynamicResource ToolbarBtnFg}" BorderBrush="{DynamicResource ToolbarBtnBorder}" BorderThickness="1"'
$content = $content -replace 'Background="#2B2B3A"', 'Background="{DynamicResource ToolbarBtnBg}" Foreground="{DynamicResource TextNormal}" BorderBrush="{DynamicResource ToolbarBtnBorder}" BorderThickness="1"'

# Save back
Set-Content $path $content -NoNewline
Write-Host "Replacement completed successfully."
