import json
import re

def main():
    commands_path = r"c:\Users\steve\source\repos\RagNext\RagNext\Resources\Raw\Commands.json"
    conditions_path = r"c:\Users\steve\source\repos\RagNext\RagNext\Resources\Raw\Conditions.json"
    action_steps_path = r"c:\Users\steve\source\repos\RagNext\RagsCore\Actions\ActionSteps.cs"

    with open(commands_path, "r", encoding="utf-8") as f:
        cmds = json.load(f)["commands"]
    
    with open(conditions_path, "r", encoding="utf-8") as f:
        conds = json.load(f)["conditions"]

    with open(action_steps_path, "r", encoding="utf-8") as f:
        cs_content = f.read()

    # Find all JsonDerivedType attributes
    derived_types = re.findall(r'\[JsonDerivedType\(typeof\((.*?)\),\s*"(.*?)"\)\]', cs_content)
    
    registered_discriminators = {d for _, d in derived_types}
    registered_classes = {c for c, _ in derived_types}

    print(f"Total derived types registered in C#: {len(derived_types)}")

    # We need a normalizer to match command names to discriminator strings.
    # In JS, the discriminator is built by mapping name to C# type map, or fallback rules.
    # Let's check how js maps them:
    # commands use fallbackDiscriminators or typeToInputsMap.
    # Let's extract fallbackDiscriminators mapping from graph_editor.js
    
    fallback_mappings = {}
    with open(r"c:\Users\steve\source\repos\RagNext\RagNext\Resources\Raw\graph_editor.js", "r", encoding="utf-8") as f:
        js_content = f.read()
    
    # Try to find fallbackDiscriminators block
    match = re.search(r'const fallbackDiscriminators\s*=\s*(\{.*?\});', js_content, re.DOTALL)
    if match:
        block = match.group(1)
        # Parse simple keys and values
        for line in block.splitlines():
            m = re.search(r'"(.*?)":\s*"(.*?)"', line)
            if m:
                fallback_mappings[m.group(1).lower()] = m.group(2)

    def normalize(name):
        return re.sub(r'[^a-zA-Z0-9]', '', name).lower()

    # Let's map Command names to C# Class names or custom discriminators
    # In C#, class names map to names like PlaySoundEffectCommand, DisplayTextCommand, VariableEqualsCondition etc.
    # Let's see what is missing.
    missing_commands = []
    for cmd in cmds:
        name = cmd["name"]
        cat = cmd["category"]
        norm_name = normalize(name)
        norm_combined = normalize(f"{cat}: {name}")
        
        # Check standard name matching
        matched_class = None
        for cls in registered_classes:
            norm_cls = normalize(cls.replace("Command", "").replace("Condition", ""))
            if norm_cls == norm_name or norm_cls == normalize(name.replace("Action:", "").replace("Character:", "").replace("Media:", "").replace("Item:", "").replace("Player:", "").replace("Room:", "").replace("StatusBar:", "").replace("Timer:", "").replace("Variable:", "")):
                matched_class = cls
                break
        
        # Check discriminator matching
        matched_disc = None
        for d in registered_discriminators:
            norm_d = normalize(d.split('.')[-1])
            if norm_d == norm_name or norm_d == normalize(name.replace("Action:", "").replace("Character:", "").replace("Media:", "").replace("Item:", "").replace("Player:", "").replace("Room:", "").replace("StatusBar:", "").replace("Timer:", "").replace("Variable:", "")):
                matched_disc = d
                break
            # Also check exact matching on lower of d
            if d.lower() == f"{cat.lower()}.{normalize(name.split(':')[-1])}" or d.lower().endswith(normalize(name.split(':')[-1])):
                matched_disc = d
                break

        # Check explicit fallback mapping from graph_editor.js
        fallback_disc = fallback_mappings.get(norm_name) or fallback_mappings.get(norm_combined)
        if fallback_disc and fallback_disc in registered_discriminators:
            matched_disc = fallback_disc

        if not matched_class and not matched_disc:
            missing_commands.append((name, cat))

    missing_conditions = []
    for cond in conds:
        name = cond["name"]
        cat = cond["category"]
        norm_name = normalize(name)
        norm_combined = normalize(f"{cat}: {name}")

        matched_class = None
        for cls in registered_classes:
            norm_cls = normalize(cls.replace("Command", "").replace("Condition", ""))
            if norm_cls == norm_name or norm_cls == normalize(name.replace("Action:", "").replace("Character:", "").replace("Media:", "").replace("Item:", "").replace("Player:", "").replace("Room:", "").replace("StatusBar:", "").replace("Timer:", "").replace("Variable:", "")):
                matched_class = cls
                break

        matched_disc = None
        for d in registered_discriminators:
            norm_d = normalize(d.split('.')[-1])
            if norm_d == norm_name or norm_d == normalize(name.replace("Action:", "").replace("Character:", "").replace("Media:", "").replace("Item:", "").replace("Player:", "").replace("Room:", "").replace("StatusBar:", "").replace("Timer:", "").replace("Variable:", "")):
                matched_disc = d
                break
            if d.lower() == f"{cat.lower()}.{normalize(name.split(':')[-1])}" or d.lower().endswith(normalize(name.split(':')[-1])):
                matched_disc = d
                break

        fallback_disc = fallback_mappings.get(norm_name) or fallback_mappings.get(norm_combined)
        if fallback_disc and fallback_disc in registered_discriminators:
            matched_disc = fallback_disc

        if not matched_class and not matched_disc:
            missing_conditions.append((name, cat))

    print("\n--- MISSING COMMANDS ---")
    if missing_commands:
        for name, cat in missing_commands:
            print(f"- [{cat}] {name}")
    else:
        print("None! All commands successfully mapped!")

    print("\n--- MISSING CONDITIONS ---")
    if missing_conditions:
        for name, cat in missing_conditions:
            print(f"- [{cat}] {name}")
    else:
        print("None! All conditions successfully mapped!")

if __name__ == "__main__":
    main()
