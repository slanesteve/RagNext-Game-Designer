$path = "c:\Users\steve\source\repos\RagNext\RagNext.Designer.Avalonia\Views\MainWindow.axaml"
$content = Get-Content $path -Raw

# Replace any occurrence of Foreground="{DynamicResource TextNormal}" followed by Foreground="Gray" (or vice versa) with just Foreground="{DynamicResource TextMuted}"
$content = $content -replace 'Foreground="\{DynamicResource TextNormal\}"[^>]*Foreground="Gray"', 'Foreground="{DynamicResource TextMuted}"'
$content = $content -replace 'Foreground="Gray"[^>]*Foreground="\{DynamicResource TextNormal\}"', 'Foreground="{DynamicResource TextMuted}"'
$content = $content -replace 'Foreground="\{DynamicResource ToolbarBtnFg\}"[^>]*Foreground="Gray"', 'Foreground="{DynamicResource TextMuted}"'
$content = $content -replace 'Foreground="Gray"[^>]*Foreground="\{DynamicResource ToolbarBtnFg\}"', 'Foreground="{DynamicResource TextMuted}"'

# Save back
Set-Content $path $content -NoNewline
Write-Host "Cleanup completed."
