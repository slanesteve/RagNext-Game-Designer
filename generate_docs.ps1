# Script to generate documentation reference pages with descriptive summaries for all commands and conditions.

$descriptions = @{
    # Commands
    "Action: Add Custom Choice" = "Adds a dynamic choice button to a player prompt and binds it to a target variable."
    "Action: Clear Custom Choice" = "Clears all custom choices from a player prompt."
    "Character: Display Description" = "Appends the character's description text to the story feed."
    "Character: Move To Room" = "Moves a character to a target room."
    "Character: Move Inventory To Player" = "Transfers all items from a character's inventory to the player."
    "Character: Move To Object" = "Moves a character inside a container object."
    "Character: Set Portrait Media" = "Changes the active portrait image of a character."
    "Character: Set Action To Active/Inactive" = "Activates or deactivates a specific character script action."
    "Character: Set Attribute" = "Sets a custom string attribute value on a character."
    "Character: Set Description" = "Changes the story description text of a character."
    "Character: Set Gender" = "Sets the gender of a character."
    "Character: Set Display Name" = "Sets the visible display name of a character."
    "General: Call Function" = "Invokes a global scripting function by ID."
    "Add A Comment" = "Inserts a designer note or comment inside the action graph (ignored at runtime)."
    "Debug Text" = "Outputs a test message to the developer console log."
    "Display Text" = "Appends standard text narrative to the gameplay story feed."
    "Media: Display Layered Picture" = "Draws a layered composite picture on the multimedia panel."
    "Media: Display Multimedia" = "Draws an image or plays audio on the gameplay screen."
    "Media: Set Background Music" = "Plays a music asset on loop as the active background track."
    "Media: Stop Background Music" = "Fades out and stops the active background music."
    "Media: Play Sound Effect" = "Plays a one-shot sound effect at a designated volume."
    "Item: Display Description" = "Appends an item's description text to the story feed."
    "Item: Move To Character" = "Moves an item to a character's inventory."
    "Item: Move To Inventory" = "Moves an item to the player's inventory."
    "Item: Move Inside Object" = "Places an item inside a container object."
    "Item: Move To Room" = "Places an item in a specific room location."
    "Item: Set Attribute" = "Sets a custom string attribute value on an item."
    "Player: Display Description" = "Appends the player's description text to the story feed."
    "Player: Move Inventory To Character" = "Transfers the player's entire inventory to a character."
    "Player: Move Inventory To Room" = "Drops the player's entire inventory into the current room."
    "Player: Move To Room" = "Teleports the player to a target room."
    "Player: Move To Character" = "Teleports the player to the room where a specific character is located."
    "Player: Move To Object" = "Teleports the player to the room where a specific object is located."
    "Player: Set Attribute" = "Sets a custom string attribute value on the player."
    "Player: Set Description" = "Changes the story description text of the player."
    "Player: Set Name" = "Changes the player's character name."
    "Player: Set Gender" = "Sets the player's gender."
    "Player: Set Portrait Media" = "Changes the active portrait image of the player."
    "Room: Display Description" = "Appends the current room's description text to the story feed."
    "Room: Display Picture" = "Displays the current room's assigned background image."
    "Room: Move Items To Player" = "Moves all loose items in the current room to the player's inventory."
    "Room: Set Description" = "Changes the story description text of a room."
    "Room: Set Picture" = "Changes the background picture asset of a room."
    "Room: Lock Exit" = "Locks a room exit direction, preventing traversal."
    "Room: Unlock Exit" = "Unlocks a room exit direction, allowing traversal."
    "StatusBar: Set Visible/Invisible" = "Shows or hides the engine's gameplay status bar."
    "Timer: Execute Timer" = "Forces a background timer to trigger immediately."
    "Timer: Reset Timer" = "Resets a timer's tick count back to zero."
    "Timer: Set Attribute" = "Sets a custom string attribute value on a timer."
    "Timer: Set Timer To Active/Inactive" = "Starts or stops a background interval timer."
    "Variable: Display Data" = "Outputs the formatted value of a variable to the story feed."
    "Variable: Set" = "Assigns a value to a global variable (supports numbers, booleans, and date/times)."
    "Variable: Increment" = "Adds a numeric value or time duration offset to a variable."
    "Variable: Decrement" = "Subtracts a numeric value or time duration offset from a variable."
    "Prompt Player Input" = "Displays an input popup dialog to prompt user text/option entry."
    "Variable: Set Numeric Randomly" = "Assigns a random integer within a range to a variable."
    "End The Game" = "Ends gameplay and displays a final summary message to the player."
    "Item: Open Container" = "Opens an item container so its contents can be accessed."
    "Item: Close Container" = "Closes an item container."

    # Conditions
    "Character: Attribute Check" = "Checks if a character's custom attribute matches an expected value."
    "Character: Gender" = "Checks if a character is a specific gender."
    "Character: In Room" = "Checks if a character is currently in a specific room."
    "Item: Attribute Check" = "Checks if an item's custom attribute matches an expected value."
    "Item: Held By Character" = "Checks if a character has a specific item in their inventory."
    "Item: Held By Player" = "Checks if the player has a specific item in their inventory."
    "Item: In Object" = "Checks if an item is inside a specific container object."
    "Item: In Room" = "Checks if a loose item is placed in a specific room."
    "Item: Not Held By Player" = "Checks if the player does not have a specific item."
    "Item: Not In Object" = "Checks if an item is not inside a specific container object."
    "Player: Attribute Check" = "Checks if the player's custom attribute matches an expected value."
    "Player: Gender" = "Checks if the player is a specific gender."
    "Player: In Room" = "Checks if the player is currently in a specific room."
    "Player: In Same Room As" = "Checks if the player is in the same room location as a specific character."
    "Room: Attribute Check" = "Checks if a room's custom attribute matches an expected value."
    "Room: Is Exit Locked" = "Checks if a specific exit direction in a room is locked."
    "Timer: Is Active" = "Checks if a background timer is currently active."
    "Variable: Comparison" = "Compares a global variable's value against a static value."
    "Variable: Comparison To Variable" = "Compares the values of two global variables."
    "Variable: DateTime Part Comparison" = "Compares a single component (minute, second, hour, day, month, year) of a datetime variable against a number."
    "DateTime: Is Past" = "Checks if a datetime variable's value represents a time in the past."
    "DateTime: Is Future" = "Checks if a datetime variable's value represents a time in the future."
    "DateTime: Compare Two Variables" = "Compares two datetime variables against each other."
    "DateTime: Compare Difference" = "Compares the timespan difference between two datetime variables against a duration (e.g. 5 minutes)."
    "DateTime: Compare Constant" = "Compares a datetime variable against a static timestamp constant."
    "DateTime: Is Valid" = "Checks if a variable contains a valid, parseable datetime string."
}

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
        $desc = $descriptions[$cmd.name]
        [void]$sb.AppendLine("### $($cmd.name)")
        [void]$sb.AppendLine()
        if ($desc) {
            [void]$sb.AppendLine("*$desc*")
            [void]$sb.AppendLine()
        }
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
        $desc = $descriptions[$cond.name]
        [void]$sb2.AppendLine("### $($cond.name)")
        [void]$sb2.AppendLine()
        if ($desc) {
            [void]$sb2.AppendLine("*$desc*")
            [void]$sb2.AppendLine()
        }
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
