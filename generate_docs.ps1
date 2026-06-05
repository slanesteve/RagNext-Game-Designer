$cmds = Get-Content -Raw -Path "c:\Users\steve\source\repos\RagNext\RagNext.Designer.Avalonia\WebAssets\Commands.json" | ConvertFrom-Json
$sb = New-Object System.Text.StringBuilder
[void]$sb.AppendLine("# Command Reference")
[void]$sb.AppendLine()
[void]$sb.AppendLine("Below is the complete reference of script commands supported by the RagNext Game Engine.")
[void]$sb.AppendLine()

$groups = $cmds.commands | Group-Object category
foreach ($g in $groups) {
    [void]$sb.AppendLine("## $($g.Name)")
    [void]$sb.AppendLine()
    foreach ($cmd in $g.Group) {
        [void]$sb.AppendLine("### $($cmd.name)")
        [void]$sb.AppendLine()
        if ($cmd.inputs) {
            [void]$sb.AppendLine("| Parameter | Type | UI Input |")
            [void]$sb.AppendLine("| :--- | :--- | :--- |")
            foreach ($inp in $cmd.inputs) {
                [void]$sb.AppendLine("| $($inp.label) | ``$($inp.dataType)`` | $($inp.controlType) |")
            }
            [void]$sb.AppendLine()
        } else {
            [void]$sb.AppendLine("No parameters.")
            [void]$sb.AppendLine()
        }
    }
}
$sb.ToString() | Out-File -FilePath "c:\Users\steve\source\repos\RagNext-Game-Designer\docs\guide\commands.md" -Encoding utf8


$conds = Get-Content -Raw -Path "c:\Users\steve\source\repos\RagNext\RagNext.Designer.Avalonia\WebAssets\Conditions.json" | ConvertFrom-Json
$sb2 = New-Object System.Text.StringBuilder
[void]$sb2.AppendLine("# Condition Reference")
[void]$sb2.AppendLine()
[void]$sb2.AppendLine("Below is the complete reference of conditional branch checks supported by the RagNext Game Engine.")
[void]$sb2.AppendLine()

$groups2 = $conds.conditions | Group-Object category
foreach ($g in $groups2) {
    [void]$sb2.AppendLine("## $($g.Name)")
    [void]$sb2.AppendLine()
    foreach ($cond in $g.Group) {
        [void]$sb2.AppendLine("### $($cond.name)")
        [void]$sb2.AppendLine()
        if ($cond.inputs) {
            [void]$sb2.AppendLine("| Parameter | Type | UI Input |")
            [void]$sb2.AppendLine("| :--- | :--- | :--- |")
            foreach ($inp in $cond.inputs) {
                [void]$sb2.AppendLine("| $($inp.label) | ``$($inp.dataType)`` | $($inp.controlType) |")
            }
            [void]$sb2.AppendLine()
        } else {
            [void]$sb2.AppendLine("No parameters.")
            [void]$sb2.AppendLine()
        }
    }
}
$sb2.ToString() | Out-File -FilePath "c:\Users\steve\source\repos\RagNext-Game-Designer\docs\guide\conditions.md" -Encoding utf8
