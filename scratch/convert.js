const fs = require('fs');
const baseName = process.argv[2] || 'TheBet';
const inputPath = `C:\\Users\\steve\\source\\repos\\RagNext\\${baseName}_raw.json`;
const outputPath = `C:\\Users\\steve\\source\\repos\\RagNext\\${baseName}.json`;

console.log(`Converting raw JSON: ${inputPath} -> ${outputPath}`);

if (!fs.existsSync(inputPath)) {
    console.error(`Error: File not found at ${inputPath}`);
    process.exit(1);
}

const raw = JSON.parse(fs.readFileSync(inputPath, 'utf8'));

// Helper to generate GUIDs if missing
function generateGuid() {
    return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, function(c) {
        var r = Math.random() * 16 | 0, v = c == 'x' ? r : (r & 0x3 | 0x8);
        return v.toString(16);
    });
}

const converted = {
    Title: raw.Title || baseName,
    Author: raw.Author || "",
    Version: raw.Version || "1.0",
    Description: raw.Description || "",
    Player: {
        Id: generateGuid(),
        Name: "Player",
        Description: "Standard Player Character",
        Gender: "Male",
        PortraitImagePath: null,
        StartingRoomId: raw.roomdata?.[0]?.[8] || null, // First room is starting room
        Inventory: [],
        Actions: [],
        Attributes: []
    },
    Rooms: [],
    Objects: [],
    Characters: [],
    Variables: [],
    MediaAssets: [],
    Functions: [],
    Timers: [],
    SplashScreen: {
        Enabled: false
    },
    StatusBarElements: [],
    WearSlots: []
};

// Create GUID mapping for media files
const mediaGuidMap = {};
if (raw.imagedata) {
    raw.imagedata.forEach(img => {
        const filename = img[0];
        mediaGuidMap[filename] = generateGuid();
    });
}

// Create GUID mapping for timers
const timerGuidMap = {};
if (raw.timerdata) {
    raw.timerdata.forEach(t => {
        const name = t[0];
        timerGuidMap[name] = generateGuid();
    });
}

// Create GUID mapping for status bar elements
const statusBarGuidMap = {};
if (raw.statusbardata) {
    raw.statusbardata.forEach(sb => {
        statusBarGuidMap[sb[0]] = generateGuid();
    });
}

// ── Variables ──
if (raw.variabledata) {
    raw.variabledata.forEach(v => {
        converted.Variables.push({
            Name: v[4],
            Value: String(v[0] !== null ? v[0] : ""),
            Description: v[3] || ""
        });
    });
}

// ── Images/Media Assets ──
if (raw.imagedata) {
    raw.imagedata.forEach(img => {
        const filename = img[0];
        converted.MediaAssets.push({
            Id: mediaGuidMap[filename],
            Name: filename,
            Type: filename.endsWith(".mp3") || filename.endsWith(".wav") ? "Audio" : (filename.endsWith(".mp4") ? "Video" : "Image"),
            FilePath: filename
        });
    });
}

// ── Status Bar Elements ──
if (raw.statusbardata) {
    raw.statusbardata.forEach(sb => {
        const name = sb[0];
        converted.StatusBarElements.push({
            Id: statusBarGuidMap[name],
            Name: name,
            Template: sb[1],
            IsVisible: !sb[2]
        });
    });
}

// ── Map Actions & Nodes (Recursive Converter) ──
function mapActionNode(node) {
    if (!node) return null;
    
    // Check if it's a Condition
    if (node[0] === "COND") {
        const condLabel = node[1];
        const condTests = node[2] || [];
        const trueBranch = (node[3] || []).map(mapActionNode).filter(x => x !== null);
        const falseBranch = (node[4] || []).map(mapActionNode).filter(x => x !== null);

        // Map condition check type
        let mappedCond = null;
        if (condTests.length === 1) {
            const t = condTests[0];
            if (t[0] === "CT_Variable_Comparison") {
                mappedCond = {
                    "$type": "var.compare",
                    Name: t[2],
                    Comparison: mapOp(t[3]),
                    Value: t[4],
                    TrueBranch: trueBranch,
                    FalseBranch: falseBranch,
                    Label: condLabel
                };
            } else if (t[0] === "CT_Character_CustomPropertyCheck" || t[0] === "CT_Item_CustomPropertyCheck" || t[0] === "CT_Player_CustomPropertyCheck") {
                mappedCond = {
                    "$type": t[0].includes("Character") ? "char.attributeCheck" : (t[0].includes("Item") ? "item.attributeCheck" : "player.attributeCheck"),
                    AttributeName: t[4] || "",
                    ExpectedValue: t[5] || "",
                    TrueBranch: trueBranch,
                    FalseBranch: falseBranch,
                    Label: condLabel
                };
            } else if (t[0] === "CT_Item_State_Check") {
                mappedCond = {
                    "$type": t[3] === "Worn" ? "item.isWorn" : "item.heldByPlayer",
                    ItemId: t[2],
                    TrueBranch: trueBranch,
                    FalseBranch: falseBranch,
                    Label: condLabel
                };
            } else if (t[0] === "CT_Timer_Active") {
                mappedCond = {
                    "$type": "timer.isActive",
                    TimerId: timerGuidMap[t[2]] || t[2] || "",
                    TrueBranch: trueBranch,
                    FalseBranch: falseBranch,
                    Label: condLabel
                };
            }
        }

        if (!mappedCond) {
            // Fallback for unknown condition: wrap inside a text command displaying info
            const fallbackText = `[Condition Check: "${condLabel}" | Tests: ${JSON.stringify(condTests)}]`;
            mappedCond = {
                "$type": "general.displayText",
                Text: fallbackText,
                Label: condLabel
            };
            if (trueBranch.length > 0) {
                return {
                    "$type": "var.compare",
                    Name: "true",
                    Comparison: "=",
                    Value: "true",
                    TrueBranch: [mappedCond, ...trueBranch],
                    FalseBranch: falseBranch,
                    Label: condLabel
                };
            }
        }
        return mappedCond;
    }

    // Check if it's a Command
    if (node[0] === "CMD") {
        const cmdType = node[1];
        switch (cmdType) {
            case "CT_DISPLAYTEXT":
            case "CT_DISPLAYROOMDESCRIPTION":
                return {
                    "$type": "general.displayText",
                    Text: node[3] || node[4] || ""
                };
            case "CT_SETVARIABLE":
                return {
                    "$type": "var.set",
                    Name: node[4] || "",
                    Value: node[6] || ""
                };
            case "CT_MOVEITEMTOINV":
                return {
                    "$type": "object.moveToInventory",
                    ObjectId: node[4] || ""
                };
            case "CT_MOVECHAR":
                return {
                    "$type": "char.moveToRoom",
                    CharacterId: node[4] || "",
                    RoomId: node[5] || ""
                };
            case "CT_PAUSEGAME":
                return {
                    "$type": "general.displayText",
                    Text: "<i>(Press Enter to continue)</i>"
                };
            case "CT_SETVARIABLEBYINPUT":
                return {
                    "$type": "general.promptInput",
                    Name: node[5] || "Input",
                    Label: node[6] || "Choose an option"
                };
            case "CT_DISPLAYPICTURE":
            case "CT_DISPLAYROOMPICTURE":
                return {
                    "$type": "media.displayMultimedia",
                    MediaId: mediaGuidMap[node[4]] || node[4] || ""
                };
            case "CT_MM_PLAY_SOUNDEFFECT":
                return {
                    "$type": "media.playSound",
                    SoundId: mediaGuidMap[node[4]] || node[4] || ""
                };
            case "CT_CHAR_SET_CUSTOM_PROPERTY":
            case "CT_ITEM_SET_CUSTOM_PROPERTY":
            case "CT_PLAYER_SET_CUSTOM_PROPERTY":
                return {
                    "$type": cmdType.includes("CHAR") ? "char.setAttribute" : (cmdType.includes("ITEM") ? "item.setAttribute" : "player.setAttribute"),
                    AttributeName: node[4] || "",
                    Value: node[6] || ""
                };
            case "CT_SHOWSTATUSBARITEM":
            case "CT_HIDESTATUSBARITEM":
                return {
                    "$type": cmdType === "CT_SHOWSTATUSBARITEM" ? "status.show" : "status.hide",
                    ElementId: statusBarGuidMap[node[4]] || node[4] || ""
                };
            case "CT_EXECUTETIMER":
                return {
                    "$type": "general.displayText",
                    Text: `[Execute Timer: ${node[4] || ""}]`
                };
            case "CT_SETTIMERACTIVE":
            case "CT_SETTIMERINACTIVE":
                return {
                    "$type": "timer.setTimerActive",
                    TimerId: timerGuidMap[node[4]] || node[4] || "",
                    Active: cmdType === "CT_SETTIMERACTIVE"
                };
            default:
                // Inject unknown command into a display text command as requested
                return {
                    "$type": "general.displayText",
                    Text: `[Command Fallback: "${cmdType}" | Args: ${JSON.stringify(node.slice(2, 7))}]`
                };
        }
    }
    return null;
}

function mapOp(oldOp) {
    if (oldOp === "Equals" || oldOp === "Equal") return "=";
    if (oldOp === "GreaterThan") return ">";
    if (oldOp === "LessThan") return "<";
    if (oldOp === "GreaterThanOrEqual") return ">=";
    if (oldOp === "LessThanOrEqual") return "<=";
    return oldOp || "=";
}

function mapTrigger(act) {
    const oldTrigger = act[3];
    const name = act[0];
    
    if (oldTrigger === "UserClicked" || oldTrigger === "0") return "UserClicked";
    if (oldTrigger === "OnGameStart" || oldTrigger === "1") return "OnGameStart";
    if (oldTrigger === "OnGameLoad" || oldTrigger === "2") return "OnGameLoad";
    if (oldTrigger === "OnTurnTick" || oldTrigger === "3") return "OnTurnTick";
    
    // Map by special names used in rooms, timers, and objects
    if (name === "<<On Game Start>>" || name === "On Game Start") return "OnGameStart";
    if (name === "<<On Player Enter First Time>>" || name === "<<On Player Enter>>") return "OnPlayerEnter";
    if (name === "<<On Player Leave First Time>>" || name === "<<On Player Leave>>") return "OnPlayerExit";
    if (name === "<<On Each Turn>>" || name === "On Each Turn") return "OnTurnTick";
    if (name === "Examine" || name === "<<Examine>>") return "OnObjectExamined";
    if (name === "Take" || name === "<<Take>>") return "OnObjectTaken";
    if (name === "Drop" || name === "<<Drop>>") return "OnObjectDropped";
    
    return "UserClicked";
}

function convertActionBlock(oldActions) {
    const list = [];
    if (!oldActions || !Array.isArray(oldActions)) return list;
    oldActions.forEach(act => {
        const rawNodes = [
            ...(act[9] || []),
            ...(act[8] || []),
            ...(act[7] || [])
        ];
        const nodes = rawNodes.map(mapActionNode).filter(x => x !== null);
        list.push({
            Id: act[2] || generateGuid(),
            Name: act[0],
            InitallyActive: act[1],
            Trigger: mapTrigger(act),
            DirectionFilter: (typeof act[4] === 'string' && act[4] !== "") ? act[4] : "All",
            Nodes: nodes
        });
    });
    return list;
}

// ── Rooms ──
if (raw.roomdata) {
    raw.roomdata.forEach(r => {
        const exits = {};
        const lockedExits = {};
        if (r[9]) {
            r[9].forEach(ex => {
                if (ex[2]) {
                    exits[ex[0]] = ex[2];
                    if (ex[1]) lockedExits[ex[0]] = true;
                }
            });
        }
        
        const attributes = [];
        if (r[10]) {
            r[10].forEach(attr => {
                attributes.push({ Name: attr[0], Value: String(attr[1] !== null ? attr[1] : "") });
            });
        }

        const roomActions = convertActionBlock(r[11]);

        converted.Rooms.push({
            Id: r[8],
            Name: r[2],
            Description: r[1],
            PortraitImagePath: r[4] || null,
            Exits: exits,
            LockedExits: lockedExits,
            ObjectIds: [],
            Actions: roomActions,
            Attributes: attributes
        });
    });
}

// ── Characters & Objects ──
if (raw.chardata) {
    raw.chardata.forEach(c => {
        const charActions = convertActionBlock(c[8]);
        const attributes = [];
        if (c[7]) {
            // Check if array or object
            if (Array.isArray(c[7])) {
                c[7].forEach(attr => {
                    attributes.push({ Name: attr[0], Value: String(attr[1] !== null ? attr[1] : "") });
                });
            } else if (typeof c[7] === 'object') {
                for (const [key, val] of Object.entries(c[7])) {
                    attributes.push({ Name: key, Value: String(val !== null ? val : "") });
                }
            }
        }
        
        converted.Characters.push({
            Id: c[4] || generateGuid(), // idx 4 has starting room / ID info?
            Name: c[0],
            Description: c[2],
            PortraitImagePath: null,
            IsCollectible: false,
            IsCharacter: true,
            Actions: charActions,
            Inventory: [],
            Properties: {},
            IsContainer: false,
            ContainerOpen: false,
            ContainedObjectIds: [],
            StartingRoomId: c[4] || null,
            IsWearable: false,
            IsWorn: false,
            WearSlot: "",
            Attributes: attributes
        });
    });
}

if (raw.objectdata) {
    raw.objectdata.forEach(o => {
        const objActions = convertActionBlock(o[21]);
        const attributes = [];
        if (o[20]) {
            if (Array.isArray(o[20])) {
                o[20].forEach(attr => {
                    attributes.push({ Name: attr[0], Value: String(attr[1] !== null ? attr[1] : "") });
                });
            } else if (typeof o[20] === 'object') {
                for (const [key, val] of Object.entries(o[20])) {
                    attributes.push({ Name: key, Value: String(val !== null ? val : "") });
                }
            }
        }

        converted.Objects.push({
            Id: o[1] || generateGuid(),
            Name: o[0],
            Description: o[4],
            PortraitImagePath: o[3] || null,
            IsCollectible: o[7] || false,
            IsCharacter: false,
            Actions: objActions,
            Inventory: [],
            Properties: {},
            IsContainer: false,
            ContainerOpen: false,
            ContainedObjectIds: [],
            StartingRoomId: null,
            IsWearable: o[22] ? true : false,
            IsWorn: o[9] || false,
            WearSlot: (o[22] && typeof o[22] === 'object') ? (o[22].wearSlot || "") : "",
            Attributes: attributes
        });
    });
}

// ── Timers ──
if (raw.timerdata) {
    raw.timerdata.forEach(t => {
        const name = t[0];
        const timerActions = convertActionBlock(t[9]);
        converted.Timers.push({
            Id: timerGuidMap[name],
            Name: name,
            IntervalSeconds: Number(t[7] || 1),
            IsActive: t[2] || false,
            IsRepeating: t[3] || false,
            Nodes: timerActions[0]?.Nodes || []
        });
    });
}

// ── Player Details & Actions ──
const playerObj = raw.playerdata;
if (playerObj) {
    converted.Player.Name = playerObj[0] || "Player";
    converted.Player.Description = playerObj[1] || "";
    converted.Player.StartingRoomId = playerObj[2] || converted.Player.StartingRoomId;
    converted.Player.Gender = playerObj[3] || "Male";
    converted.Player.PortraitImagePath = playerObj[4] || null;
    converted.Player.Actions = playerObj[11] ? convertActionBlock(playerObj[11]) : [];
    if (playerObj[10]) {
        playerObj[10].forEach(attr => {
            converted.Player.Attributes.push({ Name: attr[0], Value: String(attr[1] !== null ? attr[1] : "") });
        });
    }
}

fs.writeFileSync(outputPath, JSON.stringify(converted, null, 2), 'utf8');
console.log(`Successfully converted to new JSON format! Saved to ${outputPath}`);

