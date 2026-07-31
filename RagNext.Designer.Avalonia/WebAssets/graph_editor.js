/**
 * Rags Node Visual Graph Editor Engine
 * Handles dragging, panning, drawing connections, dynamic catalog parsing, parameter inputs, and C# serialization.
 */

// Global C# Bridge Helper using WebView2 postMessage or URL navigation fallback
window.invokeCSharpAction = function(msg) {
    if (window.chrome && window.chrome.webview && typeof window.chrome.webview.postMessage === 'function') {
        window.chrome.webview.postMessage(msg);
    } else {
        window.location.href = "rags-action://" + msg;
    }
};

let nodes = [];
let connections = [];
let selectedNode = null;
let selectedNodes = [];
let activeActionName = "Visual Action Node";
let activeActionTrigger = "UserClicked";
let activeActionInitallyActive = true;
let activeActionDirectionFilter = "All";
let isLoadingGraph = false;

function getArray(val) {
    if (!val) return [];
    if (Array.isArray(val)) return val;
    if (val.$values && Array.isArray(val.$values)) return val.$values;
    return [];
}

// Infinite Canvas Transform State
let panX = 0;
let panY = 0;
let zoom = 1.0;
let isPanning = false;
let startPanX = 0;
let startPanY = 0;

// Dynamic link drawing state
let activeDrawingPin = null;
let cursorX = 0;
let cursorY = 0;

// Right-click cursor positioning
let contextCursorX = 100;
let contextCursorY = 100;

// Dom Elements references
const container = document.getElementById('canvas-container');
const nodesLayer = document.getElementById('nodes-layer');
const svgLayer = document.getElementById('svg-layer');
const contextMenu = document.getElementById('context-menu');

// Disable spellchecking globally across all inputs and textareas in the visual script editor
document.addEventListener('DOMContentLoaded', () => {
    const disableSpellcheck = (node) => {
        if (!node) return;
        if (node.tagName === 'TEXTAREA' || node.tagName === 'INPUT') {
            node.setAttribute('spellcheck', 'false');
            node.spellcheck = false;
        }
        if (node.querySelectorAll) {
            node.querySelectorAll('textarea, input').forEach(el => {
                el.setAttribute('spellcheck', 'false');
                el.spellcheck = false;
            });
        }
    };
    const observer = new MutationObserver((mutations) => {
        mutations.forEach(m => m.addedNodes.forEach(disableSpellcheck));
    });
    if (document.body) {
        observer.observe(document.body, { childList: true, subtree: true });
        document.querySelectorAll('textarea, input').forEach(el => {
            el.setAttribute('spellcheck', 'false');
            el.spellcheck = false;
        });
    }
});

// Dynamic Database Catalogs and reflection lookup maps
let catalogs = {};
let nameToTypeMap = {};
let typeToNameMap = {};
let typeToInputsMap = {};

let AVAILABLE_COMMANDS = [];
let AVAILABLE_CONDITIONS = [];

let lastActionJson = null;
let lastCommandsDb = null;
let lastConditionsDb = null;
let lastCatalogsDb = null;
let lastTypesMap = null;
let previousGraphState = null;

// Debounced auto-saving on the fly
let autoSaveTimeout = null;
function triggerAutoSave() {
    if (autoSaveTimeout) clearTimeout(autoSaveTimeout);
    autoSaveTimeout = setTimeout(() => {
        saveAndSyncCsharp(true);
    }, 400); // 400ms debounce
}

// Comprehensive fallback map of friendly names to C# polymorphic type discriminators
const fallbackDiscriminators = {
    "statusshowstatuselement": "status.show",
    "statushidestatuselement": "status.hide",
    "statussetstatuselementtext": "status.setText",
    "statussetstatuselementimage": "status.setImage",
    "statusisstatuselementvisible": "status.isVisible",
    "actionaddcustomchoice": "general.addCustomChoice",
    "actionclearcustomchoice": "general.clearCustomChoice",
    "characterdisplaydescription": "char.displayDescription",
    "charactermovetoroom": "char.moveToRoom",
    "charactermovetorandomadjacentroom": "char.moveToRandomAdjacent",
    "charactermovealongpatrolpath": "char.moveAlongPatrolPath",
    "charactermoveinventorytoplayer": "char.moveInventoryToPlayer",
    "charactermovetoobject": "char.moveToObject",
    "charactersetportraitmedia": "char.setPortraitMedia",
    "charactersetactiontoactiveinactive": "char.setActionActive",
    "playersetactiontoactiveinactive": "player.setActionActive",
    "roomsetactiontoactiveinactive": "room.setActionActive",
    "itemsetactiontoactiveinactive": "item.setActionActive",
    "charactersetattribute": "char.setAttribute",
    "charactersetdescription": "char.setDescription",
    "charactersetgender": "char.setGender",
    "charactersetdisplayname": "char.setDisplayName",
    "addacomment": "general.addComment",
    "generalcallfunction": "general.callFunction",
    "debugtext": "general.debugText",
    "displaytext": "general.displayText",
    "mediadisplaymultimedia": "media.displayMultimedia",
    "mediasetbackgroundmusic": "media.setBackgroundMusic",
    "mediastopbackgroundmusic": "media.stopBackgroundMusic",
    "mediaplaysoundeffect": "media.playSound",
    "mediastopsoundeffect": "media.stopSound",
    "mediaplayvideo": "media.playVideo",
    "itemdisplaydescription": "object.displayDescription",
    "itemmovetocharacter": "object.moveToCharacter",
    "itemmovetoinventory": "object.moveToInventory",
    "itemmoveinsideobject": "object.moveInsideObject",
    "itemmovetoroom": "room.addObject",
    "itemsetattribute": "item.setAttribute",
    "itemshowinteractivescreen": "item.showInteractiveScreen",
    "itemcloseinteractivescreen": "item.closeInteractiveScreen",
    "playerdisplaydescription": "player.displayDescription",
    "playermoveinventorytocharacter": "player.moveInventoryToChar",
    "playermoveinventorytoroom": "player.moveInventoryToRoom",
    "playermovetoroom": "player.moveTo",
    "generalscreenshake": "player.screenShake",
    "playermovetocharacter": "player.moveToChar",
    "playermovetoobject": "player.moveToObject",
    "playerswapcharacter": "player.swapCharacter",
    "uishowsplashscreen": "ui.showSplashScreen",
    "playersetattribute": "player.setAttribute",
    "playersetdescription": "player.setDescription",
    "playersetname": "player.setName",
    "playersetgender": "player.setGender",
    "playersetportraitmedia": "player.setPortraitMedia",
    "roomdisplaydescription": "room.displayDescription",
    "roomdisplaypicture": "room.displayPicture",
    "roommoveitemstoplayer": "room.moveItemsToPlayer",
    "roomsetdescription": "room.setDescription",
    "roomsetpicture": "room.setPicture",
    "roomsetattribute": "room.setAttribute",
    "roomlockexit": "room.lockExit",
    "roomunlockexit": "room.unlockExit",
    "statusbarsetvisibleinvisible": "ui.setStatusBarVisible",
    "uisethotspotactivestate": "ui.setHotspotActive",
    "uisetclosebuttonvisible": "ui.setCloseButtonVisible",
    "timersetattribute": "timer.setAttribute",
    "timersettimertoactiveinactive": "timer.setTimerActive",
    "variableset": "var.set",
    "variableevaluateformula": "var.evaluate",
    "variableincrement": "var.inc",
    "variabledecrement": "var.dec",
    "variableforeachloop": "variable.forEachLoop",
    "foreachloop": "variable.forEachLoop",
    "variablebreakloop": "variable.breakLoop",
    "variablesetarrayelement": "variable.setArrayElement",
    "variableaddarrayrow": "variable.addArrayRow",
    "variableremovearrayrow": "variable.removeArrayRow",
    "variableappendtext": "variable.appendText",
    "variableappendline": "variable.appendLine",
    "promptplayerinput": "general.promptInput",
    "waitforcontinue": "general.waitForContinue",
    "generalshowmap": "general.showMap",
    "variablesetnumericrandomly": "var.setRandom",
    "endthegame": "general.endGame",
    "itemopencontainer": "general.openContainer",
    "itemclosecontainer": "general.closeContainer",
    "itemwearitem": "item.wear",
    "itemremoveitem": "item.remove",
    "characterattributecheck": "char.attributeCheck",
    "charactergender": "char.gender",
    "characterinroom": "char.inRoom",
    "itemattributecheck": "item.attributeCheck",
    "itemheldbycharacter": "item.heldByChar",
    "itemheldbyplayer": "item.heldByPlayer",
    "iteminobject": "item.inObject",
    "iteminroom": "item.inRoom",
    "itemnotheldbyplayer": "item.notHeldByPlayer",
    "itemnotinobject": "item.notInObject",
    "itemisitemworn": "item.isWorn",
    "itemcanitembeworn": "item.canWear",
    "playerattributecheck": "player.attributeCheck",
    "playergender": "player.gender",
    "playerinroom": "player.inRoom",
    "playerinsameroomas": "player.sameRoom",
    "roomattributecheck": "room.attributeCheck",
    "roomisexitlocked": "room.isExitLocked",
    "timerisactive": "timer.isActive",
    "variablecomparison": "var.compare",
    "variabledatetimepartcomparison": "date.partCompare",
    "datetimeispast": "date.isPast",
    "datetimeisfuture": "date.isFuture",
    "datetimecomparetwovariables": "date.compareVars",
    "datetimecomparedifference": "date.diffCompare",
    "datetimecompareconstant": "date.compareConst",
    "datetimeisvalid": "date.isValid"
};

const propertyMappings = {
    "Character": ["CharacterId", "characterId", "Character"],
    "Destination Room": ["RoomId", "roomId", "DestinationRoom", "destinationRoom"],
    "Room": ["RoomId", "roomId", "Room"],
    "Media File": ["MediaId", "mediaId", "MediaFile", "mediaFile"],
    "Media": ["MediaId", "mediaId", "Media"],
    "Portrait Media": ["PortraitId", "portraitId", "PortraitMedia", "portraitMedia", "MediaId"],
    "Object": ["ObjectId", "objectId", "Object"],
    "Item": ["ItemId", "itemId", "Item", "ObjectId", "objectId"],
    "Container Object": ["ObjectId", "objectId", "ContainerObjectId", "containerObjectId", "ContainerObject", "containerObject"],
    "Choice Text": ["ChoiceText", "choiceText", "Text", "text"],
    "Target Variable": ["VariableName", "variableName", "Name", "name", "TargetVariable", "targetVariable"],
    "Variable": ["Name", "name", "VariableName", "variableName", "Variable", "variable"],
    "Variable A": ["VariableNameA", "NameA", "nameA", "VariableA", "variableA", "variableNameA"],
    "Variable B": ["VariableNameB", "NameB", "nameB", "VariableB", "variableB", "variableNameB"],
    "Text": ["Text", "text"],
    "Final Message": ["FinalMessage", "finalMessage"],
    "Amount": ["Amount", "amount"],
    "Direction": ["Direction", "direction"],
    "Prompt Text": ["PromptText", "promptText"],
    "Input Type": ["InputType", "inputType"],
    "Splash Screen Name": ["SplashScreenName", "splashScreenName"],
    "Custom Options": ["CustomOptions", "customOptions"],
    "Store Variable": ["StoreVariableName", "storeVariableName"],
    "Prompt Name": ["PromptName", "promptName"],
    "Attribute Name": ["AttributeName", "attributeName"],
    "Map Title": ["MapTitle", "mapTitle"],
    "Map Style": ["MapStyle", "mapStyle"],
    "Custom Background": ["CustomBackground", "customBackground"],
    "Timer": ["TimerId", "timerId", "Timer"],
    "Function": ["FunctionId", "functionId", "Function"],
    "Expected Value": ["ExpectedValue", "expectedValue"],
    "DateTime Component": ["DateTimeComponent", "dateTimeComponent"],
    "Duration": ["Duration", "duration"],
    "Constant Value": ["ConstantValue", "constantValue"],
    "Transition Style": ["TransitionStyle", "transitionStyle"],
    "Transition Duration": ["Duration", "duration", "TransitionDuration", "transitionDuration"],
    "Transition Intensity": ["Intensity", "intensity"],
    "Button Text": ["ButtonText", "buttonText"],
    "Sound File": ["SoundId", "soundId"],
    "Hotspot Id or Name": ["HotspotIdOrName", "hotspotIdOrName"],
    "Video File": ["VideoId", "videoId"],
    "Array Variable": ["ArrayVariableName", "arrayVariableName"],
    "Row Index": ["RowIndex", "rowIndex"],
    "Column Name": ["ColumnName", "columnName"],
    "Comma-separated Values": ["ValuesCommaSeparated", "valuesCommaSeparated"],
    "Volume": ["Volume", "volume"],
    "Loop": ["Loop", "loop"],
    "Start Time": ["StartTime", "startTime"],
    "End Time": ["EndTime", "endTime"],
    // Bug #5: ActionName maps to the C# ActionName property on Set Action Active commands.
    "Action Name": ["ActionName", "actionName"],
    "Patrol Path": ["PatrolPath", "patrolPath"],
    "Index Variable": ["IndexVariable", "indexVariable"],
    "Ping Pong": ["PingPong", "pingPong"],
    "Loop Source": ["LoopSource", "loopSource"],
    "Filter Type": ["FilterType", "filterType"],
    "Comment Text": ["CommentText", "commentText"],
    "Target Character": ["CharacterId", "characterId"],
    "Music File": ["MusicFile", "musicFile"]
};

function getPropertyValue(nodeData, label) {
    if (nodeData[label] !== undefined) return nodeData[label];
    
    const aliases = propertyMappings[label] || [];
    for (let alias of aliases) {
        if (nodeData[alias] !== undefined) {
            return nodeData[alias];
        }
    }
    
    const normalizedLabel = label.toLowerCase().replace(/[^a-z]/g, '');
    for (let key of Object.keys(nodeData)) {
        const normalizedKey = key.toLowerCase().replace(/[^a-z]/g, '');
        if (normalizedKey === normalizedLabel || 
            normalizedKey === normalizedLabel + 'id' || 
            normalizedLabel === normalizedKey + 'id') {
            return nodeData[key];
        }
    }
    return "";
}

function getSelectedVariableType(node) {
    const varName = node.data["Name"] || node.data["name"] || node.data["VariableName"] || node.data["variableName"] || node.data["Variable"] || node.data["variable"] || "";
    if (varName && catalogs.Variables) {
        const matchingVar = catalogs.Variables.find(v => v.Name === varName || v.id === varName || v.Id === varName);
        if (matchingVar) {
            return (matchingVar.varType || matchingVar.VarType || matchingVar.Type || matchingVar.type || "").toLowerCase();
        }
    }
    return "";
}

function getNodeIdFromPinId(pinId) {
    if (!pinId) return null;
    if (pinId.startsWith('choice_')) {
        const choiceId = parseInt(pinId.split('_')[1]);
        const parentNode = nodes.find(n => n.choices && n.choices.some(ch => ch.id === choiceId));
        if (parentNode) return parentNode.id;
    }
    const lastUnderscore = pinId.lastIndexOf('_');
    return lastUnderscore !== -1 ? pinId.substring(0, lastUnderscore) : pinId;
}

function normalize(str) {
    if (!str) return "";
    return str.toLowerCase().replace(/[^a-z0-9]/g, '');
}

// Initialize Canvas Panning, Zooming, and Input Listeners
function initGraph() {
    container.addEventListener('mousedown', (e) => {
        if (e.target === container || e.target === svgLayer) {
            isPanning = true;
            startPanX = e.clientX - panX;
            startPanY = e.clientY - panY;
            hideContextMenu();
            deselectAllNodes();
        }
    });

    container.addEventListener('mousemove', (e) => {
        const bounds = container.getBoundingClientRect();
        cursorX = (e.clientX - bounds.left - panX) / zoom;
        cursorY = (e.clientY - bounds.top - panY) / zoom;

        if (isPanning) {
            panX = e.clientX - startPanX;
            panY = e.clientY - startPanY;
            updateTransform();
        } else if (activeDrawingPin) {
            drawTemporaryConnection();
        }
    });

    container.addEventListener('mouseup', () => {
        isPanning = false;
        if (activeDrawingPin) {
            activeDrawingPin = null;
            redrawConnections();
        }
    });

    container.addEventListener('wheel', (e) => {
        if (e.target.tagName === 'TEXTAREA' || e.target.closest('textarea')) {
            return; // Let the textarea scroll naturally
        }
        e.preventDefault();
        const rect = container.getBoundingClientRect();
        const mouseX = e.clientX - rect.left;
        const mouseY = e.clientY - rect.top;

        // Zoom centered on mouse
        const wheel = e.deltaY < 0 ? 1.05 : 0.95;
        const newZoom = Math.min(Math.max(zoom * wheel, 0.4), 2.0);

        panX = mouseX - (mouseX - panX) * (newZoom / zoom);
        panY = mouseY - (mouseY - panY) * (newZoom / zoom);
        zoom = newZoom;

        updateTransform();
    });
}

let jsActionClipboard = null;

function getViewportCenterCoordinates() {
    const editorEl = document.getElementById('canvas-container') || document.body;
    const width = editorEl.clientWidth || 800;
    const height = editorEl.clientHeight || 600;
    const x = (width / 2 - panX) / zoom;
    const y = (height / 2 - panY) / zoom;
    return { x, y };
}

function getDescendantNodeIds(node, visited = new Set()) {
    if (!node || visited.has(node.id)) return [];
    visited.add(node.id);

    let descendants = [];

    if (node.type === 'dialogue') {
        node.choices.forEach(c => {
            const destPin = connections.find(conn => conn.fromPinId === `${c.rowId}_out`);
            const destNode = destPin ? nodes.find(n => n.id === getNodeIdFromPinId(destPin.toPinId)) : null;
            if (destNode) {
                descendants.push(destNode.id);
                descendants = descendants.concat(getDescendantNodeIds(destNode, visited));
            }
        });
    } else if (node.type === 'switch') {
        node.cases.forEach(c => {
            const destPin = connections.find(conn => conn.fromPinId === `${c.rowId}_out`);
            const destNode = destPin ? nodes.find(n => n.id === getNodeIdFromPinId(destPin.toPinId)) : null;
            if (destNode) {
                descendants.push(destNode.id);
                descendants = descendants.concat(getDescendantNodeIds(destNode, visited));
            }
        });
        const defaultPin = connections.find(c => c.fromPinId === `${node.id}_default`);
        const defaultNode = defaultPin ? nodes.find(n => n.id === getNodeIdFromPinId(defaultPin.toPinId)) : null;
        if (defaultNode) {
            descendants.push(defaultNode.id);
            descendants = descendants.concat(getDescendantNodeIds(defaultNode, visited));
        }
    } else if (node.type === 'condition') {
        const truePin = connections.find(c => c.fromPinId === `${node.id}_true`);
        const falsePin = connections.find(c => c.fromPinId === `${node.id}_false`);
        const trueNode = truePin ? nodes.find(n => n.id === getNodeIdFromPinId(truePin.toPinId)) : null;
        const falseNode = falsePin ? nodes.find(n => n.id === getNodeIdFromPinId(falsePin.toPinId)) : null;
        if (trueNode) {
            descendants.push(trueNode.id);
            descendants = descendants.concat(getDescendantNodeIds(trueNode, visited));
        }
        if (falseNode) {
            descendants.push(falseNode.id);
            descendants = descendants.concat(getDescendantNodeIds(falseNode, visited));
        }
    } else if (node.type === 'command') {
        const nextPin = connections.find(c => c.fromPinId === `${node.id}_out`);
        const nextNode = nextPin ? nodes.find(n => n.id === getNodeIdFromPinId(nextPin.toPinId)) : null;
        if (nextNode) {
            descendants.push(nextNode.id);
            descendants = descendants.concat(getDescendantNodeIds(nextNode, visited));
        }
    }

    return descendants;
}

function getNestedDescendantNodeIds(nodesList) {
    const nestedIds = new Set();
    nodesList.forEach(node => {
        if (node.type === 'dialogue' || node.type === 'switch' || node.type === 'condition') {
            const descendants = getDescendantNodeIds(node);
            descendants.forEach(id => nestedIds.add(id));
        }
    });
    return nestedIds;
}

function copyNodeAtCursor() {
    if (selectedNodes && selectedNodes.length > 0) {
        // Filter out nodes that are already nested/handled recursively under selected Dialogue, Switch, or Condition parent nodes
        const nestedIds = getNestedDescendantNodeIds(selectedNodes);
        const rootSelectedNodes = selectedNodes.filter(n => !nestedIds.has(n.id));

        const nodesData = rootSelectedNodes.map(node => {
            return {
                id: node.id,
                type: node.type,
                x: node.x,
                y: node.y,
                json: buildNodeJsonWithoutNext(node)
            };
        });

        const selectedNodeIds = selectedNodes.map(n => n.id);
        // Only copy exec connections (dialogue/switch/condition branch connections are handled recursively by layout parser)
        const copiedConns = connections.filter(conn => {
            if (conn.type !== 'exec') return false;
            const fromNodeId = getNodeIdFromPinId(conn.fromPinId);
            const toNodeId = getNodeIdFromPinId(conn.toPinId);
            return selectedNodeIds.includes(fromNodeId) && selectedNodeIds.includes(toNodeId);
        });

        jsActionClipboard = {
            type: "multi-nodes",
            nodes: nodesData,
            connections: copiedConns
        };
    } else if (selectedNode) {
        const nodeJson = buildNodeJsonWithoutNext(selectedNode);
        jsActionClipboard = JSON.parse(JSON.stringify(nodeJson));
    }
    hideContextMenu();
}

function shiftJsonCoordinates(json, offsetX, offsetY) {
    if (!json) return;

    if (json.x !== undefined && json.x !== null) json.x += offsetX;
    if (json.X !== undefined && json.X !== null) json.X += offsetX;
    if (json.y !== undefined && json.y !== null) json.y += offsetY;
    if (json.Y !== undefined && json.Y !== null) json.Y += offsetY;

    if (json.trueBranch) {
        json.trueBranch.forEach(child => shiftJsonCoordinates(child, offsetX, offsetY));
    }
    if (json.TrueBranch) {
        json.TrueBranch.forEach(child => shiftJsonCoordinates(child, offsetX, offsetY));
    }
    if (json.falseBranch) {
        json.falseBranch.forEach(child => shiftJsonCoordinates(child, offsetX, offsetY));
    }
    if (json.FalseBranch) {
        json.FalseBranch.forEach(child => shiftJsonCoordinates(child, offsetX, offsetY));
    }
    if (json.defaultBranch) {
        json.defaultBranch.forEach(child => shiftJsonCoordinates(child, offsetX, offsetY));
    }
    if (json.DefaultBranch) {
        json.DefaultBranch.forEach(child => shiftJsonCoordinates(child, offsetX, offsetY));
    }
    if (json.cases) {
        Object.keys(json.cases).forEach(k => {
            const list = json.cases[k];
            if (Array.isArray(list)) {
                list.forEach(child => shiftJsonCoordinates(child, offsetX, offsetY));
            }
        });
    }
    if (json.choices) {
        json.choices.forEach(c => {
            const list = c.commands || c.Commands;
            if (Array.isArray(list)) {
                list.forEach(child => shiftJsonCoordinates(child, offsetX, offsetY));
            }
        });
    }
}

function pasteNodeAtCursor() {
    if (!jsActionClipboard) return;

    deselectAllNodes();

    if (jsActionClipboard.type === "multi-nodes") {
        const clipboardData = JSON.parse(JSON.stringify(jsActionClipboard));
        const copiedNodes = clipboardData.nodes;
        const copiedConns = clipboardData.connections;

        if (copiedNodes.length === 0) return;

        let minX = Infinity;
        let minY = Infinity;
        copiedNodes.forEach(n => {
            if (n.x < minX) minX = n.x;
            if (n.y < minY) minY = n.y;
        });

        const offsetX = contextCursorX - minX;
        const offsetY = contextCursorY - minY;

        const newlyCreatedNodes = [];

        copiedNodes.forEach(copiedNode => {
            const pasteX = copiedNode.x + offsetX;
            const pasteY = copiedNode.y + offsetY;

            // Shift nested coordinates recursively in the copied JSON
            shiftJsonCoordinates(copiedNode.json, offsetX, offsetY);

            if (copiedNode.json["dialogueId"]) {
                copiedNode.json.dialogueId = 'dialogue_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
            }

            const newNode = parseAndCreateNode(copiedNode.json, pasteX, pasteY);
            if (newNode) {
                newlyCreatedNodes.push(newNode);
            }
        });

        // Scan all nodes and map old original ID to new ID
        const idMap = {};
        nodes.forEach(n => {
            if (n.data && n.data._originalId) {
                idMap[n.data._originalId] = n.id;
            }
        });

        // Restore connections (e.g. exec lines) between pasted/nested node elements
        copiedConns.forEach(conn => {
            const oldFromNodeId = getNodeIdFromPinId(conn.fromPinId);
            const oldToNodeId = getNodeIdFromPinId(conn.toPinId);
            const newFromNodeId = idMap[oldFromNodeId];
            const newToNodeId = idMap[oldToNodeId];

            if (newFromNodeId && newToNodeId) {
                const newFromPinId = conn.fromPinId.replace(oldFromNodeId, newFromNodeId);
                const newToPinId = conn.toPinId.replace(oldToNodeId, newToNodeId);

                connections.push({
                    fromPinId: newFromPinId,
                    toPinId: newToPinId,
                    type: conn.type
                });
            }
        });

        // Select all newly created nodes for convenience
        newlyCreatedNodes.forEach(newNode => {
            newNode.element.classList.add('selected');
            selectedNodes.push(newNode);
        });

        // Cleanup temporary original ID field
        nodes.forEach(n => {
            if (n.data) {
                delete n.data._originalId;
                delete n.data.OriginalId;
                delete n.data._originalid;
            }
        });

        if (newlyCreatedNodes.length > 0) {
            selectedNode = newlyCreatedNodes[newlyCreatedNodes.length - 1];
        }

        redrawConnections();
        triggerAutoSave();
    } else {
        const data = JSON.parse(JSON.stringify(jsActionClipboard));
        data.X = contextCursorX;
        data.Y = contextCursorY;
        if (data["dialogueId"]) {
            data.dialogueId = 'dialogue_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
        }
        const newNode = parseAndCreateNode(data, contextCursorX, contextCursorY);
        if (newNode) {
            newNode.element.classList.add('selected');
            selectedNodes.push(newNode);
            selectedNode = newNode;
        }
        // Cleanup original id if present
        if (newNode && newNode.data) {
            delete newNode.data._originalId;
            delete newNode.data.OriginalId;
            delete newNode.data._originalid;
        }
        redrawConnections();
        triggerAutoSave();
    }

    hideContextMenu();
}

// Custom Right-Click Menu
window.addEventListener('contextmenu', (e) => {
    e.preventDefault();
    const bounds = container.getBoundingClientRect();
    contextCursorX = (e.clientX - bounds.left - panX) / zoom;
    contextCursorY = (e.clientY - bounds.top - panY) / zoom;

    const clickedNodeEl = e.target.closest('.node');
    if (clickedNodeEl) {
        const clickedNode = nodes.find(n => n.element === clickedNodeEl);
        if (clickedNode && clickedNode.type !== 'start') {
            if (!selectedNodes.includes(clickedNode)) {
                deselectAllNodes();
                clickedNode.element.classList.add('selected');
                selectedNode = clickedNode;
                selectedNodes.push(clickedNode);
            }
        }
        
        document.getElementById('menu-add-dialogue').style.display = 'none';
        document.getElementById('menu-add-command').style.display = 'none';
        document.getElementById('menu-add-condition').style.display = 'none';
        document.getElementById('menu-paste').style.display = 'none';
        
        const isStart = clickedNode && clickedNode.type === 'start';
        document.getElementById('menu-sep').style.display = isStart ? 'none' : 'block';
        document.getElementById('menu-copy').style.display = isStart ? 'none' : 'block';
        document.getElementById('menu-delete').style.display = isStart ? 'none' : 'block';
    } else {
        document.getElementById('menu-add-dialogue').style.display = 'block';
        document.getElementById('menu-add-command').style.display = 'block';
        document.getElementById('menu-add-condition').style.display = 'block';
        
        const pasteEl = document.getElementById('menu-paste');
        if (jsActionClipboard) {
            pasteEl.style.display = 'block';
            pasteEl.style.opacity = '1';
            pasteEl.style.pointerEvents = 'auto';
        } else {
            pasteEl.style.display = 'none';
        }
        
        document.getElementById('menu-sep').style.display = 'none';
        document.getElementById('menu-copy').style.display = 'none';
        document.getElementById('menu-delete').style.display = 'none';
    }

    contextMenu.style.display = 'block';
    contextMenu.style.left = `${e.clientX}px`;
    contextMenu.style.top = `${e.clientY}px`;
});

window.addEventListener('click', () => {
    hideContextMenu();
});

function updateTransform() {
    nodesLayer.style.transform = `translate(${panX}px, ${panY}px) scale(${zoom})`;
    redrawConnections();
}

function hideContextMenu() {
    contextMenu.style.display = 'none';
}

function deselectAllNodes() {
    nodes.forEach(n => {
        n.element.classList.remove('selected');
    });
    selectedNode = null;
    selectedNodes = [];
}

// Draw/Redraw SVG Connections
function redrawConnections() {
    while (svgLayer.lastChild && svgLayer.lastChild.tagName === 'path') {
        svgLayer.removeChild(svgLayer.lastChild);
    }

    connections.forEach(conn => {
        const fromPin = document.getElementById(conn.fromPinId);
        const toPin = document.getElementById(conn.toPinId);
        if (!fromPin || !toPin) return;
        if (fromPin.style.display === 'none' || window.getComputedStyle(fromPin).display === 'none') return;
        if (toPin.style.display === 'none' || window.getComputedStyle(toPin).display === 'none') return;

        const path = drawBezierCurve(fromPin, toPin, conn.type);
        path.style.cursor = 'pointer';
        path.addEventListener('click', (e) => {
            e.stopPropagation();
            connections = connections.filter(c => c !== conn);
            redrawConnections();
            triggerAutoSave();
        });
        svgLayer.appendChild(path);
    });

    if (activeDrawingPin) {
        drawTemporaryConnection();
    }
}

function drawBezierCurve(fromPin, toPin, type) {
    const fromRect = fromPin.getBoundingClientRect();
    const toRect = toPin.getBoundingClientRect();
    const bounds = container.getBoundingClientRect();

    const x1 = fromRect.left + fromRect.width / 2 - bounds.left;
    const y1 = fromRect.top + fromRect.height / 2 - bounds.top;
    const x2 = toRect.left + toRect.width / 2 - bounds.left;
    const y2 = toRect.top + toRect.height / 2 - bounds.top;

    const path = document.createElementNS("http://www.w3.org/2000/svg", "path");
    const dx = Math.abs(x2 - x1) * 0.5;
    
    path.setAttribute("d", `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`);
    path.setAttribute("class", `connection-line ${type || ''}`);
    return path;
}

function drawTemporaryConnection() {
    let tempPath = document.getElementById('temp-path');
    if (tempPath) tempPath.remove();

    const pin = document.getElementById(activeDrawingPin.id);
    if (!pin) return;

    const pinRect = pin.getBoundingClientRect();
    const bounds = container.getBoundingClientRect();

    const x1 = pinRect.left + pinRect.width / 2 - bounds.left;
    const y1 = pinRect.top + pinRect.height / 2 - bounds.top;
    
    const x2 = cursorX * zoom + panX;
    const y2 = cursorY * zoom + panY;

    tempPath = document.createElementNS("http://www.w3.org/2000/svg", "path");
    tempPath.setAttribute("id", "temp-path");
    const dx = Math.abs(x2 - x1) * 0.5;
    tempPath.setAttribute("d", `M ${x1} ${y1} C ${x1 + dx} ${y1}, ${x2 - dx} ${y2}, ${x2} ${y2}`);
    tempPath.setAttribute("class", "connection-line");
    svgLayer.appendChild(tempPath);
}

// Renders dynamic live previews of Rags tags
function renderRichTextPreview(text) {
    if (!text) return '<span style="color: var(--text-muted); font-style: italic;">No preview text...</span>';
    let html = text
        .replace(/&/g, "&amp;")
        .replace(/</g, "&lt;")
        .replace(/>/g, "&gt;");

    html = html
        .replace(/&lt;b&gt;/gi, "<strong>")
        .replace(/&lt;\/b&gt;/gi, "</strong>")
        .replace(/&lt;i&gt;/gi, "<em>")
        .replace(/&lt;\/i&gt;/gi, "</em>")
        .replace(/&lt;u&gt;/gi, "<u>")
        .replace(/&lt;\/u&gt;/gi, "</u>");

    let colorRegex = /&lt;color=(#[a-f0-9]{6,8})&gt;(.*?)&lt;\/color&gt;/gi;
    html = html.replace(colorRegex, function(match, color, content) {
        if (color.length === 9) {
            color = '#' + color.substring(3) + color.substring(1, 3);
        }
        return '<span style="color: ' + color + ';">' + content + '</span>';
    });

    let markRegex = /&lt;mark=(#[a-f0-9]{6,8})&gt;(.*?)&lt;\/mark&gt;/gi;
    html = html.replace(markRegex, function(match, color, content) {
        if (color.length === 9) {
            color = '#' + color.substring(3) + color.substring(1, 3);
        }
        return '<span style="background-color: ' + color + '; padding: 2px 4px; border-radius: 4px;">' + content + '</span>';
    });

    html = html.replace(/\n/g, "<br>");
    return html;
}

function updateLivePreview(textarea, previewElement) {
    if (previewElement) {
        previewElement.innerHTML = renderRichTextPreview(textarea.value);
    }
}

// Rich Text formatting Toolbar Helper
function wrapSelection(textarea, startTag, endTag, previewElement) {
    const cursor = textarea.selectionStart;
    const selectionLength = textarea.selectionEnd - cursor;
    const text = textarea.value;

    if (selectionLength > 0 && cursor >= 0 && cursor + selectionLength <= text.length) {
        const before = text.substring(0, cursor);
        const selected = text.substring(cursor, cursor + selectionLength);
        const after = text.substring(cursor + selectionLength);

        // Case A: Selected text is already wrapped in the tags (e.g., "<b>hello</b>")
        if (selected.startsWith(startTag) && selected.endsWith(endTag)) {
            const unwrapped = selected.substring(startTag.length, selected.length - endTag.length);
            textarea.value = before + unwrapped + after;
            textarea.focus();
            textarea.setSelectionRange(cursor, cursor + unwrapped.length);
        }
        // Case B: Selection is immediately bordered by the tags (e.g., "<b>" + "hello" + "</b>")
        else if (before.endsWith(startTag) && after.startsWith(endTag)) {
            const newBefore = before.substring(0, before.length - startTag.length);
            const newAfter = after.substring(endTag.length);
            textarea.value = newBefore + selected + newAfter;
            textarea.focus();
            textarea.setSelectionRange(newBefore.length, newBefore.length + selected.length);
        }
        else {
            // Not wrapped, so wrap it
            textarea.value = before + startTag + selected + endTag + after;
            textarea.focus();
            textarea.setSelectionRange(cursor + startTag.length, cursor + startTag.length + selected.length);
        }
    } else {
        let actualCursor = cursor;
        if (actualCursor < 0 || actualCursor > text.length) {
            actualCursor = text.length;
        }
        const before = text.substring(0, actualCursor);
        const after = text.substring(actualCursor);

        // If cursor is immediately between the tags, remove them (toggle empty)
        if (before.endsWith(startTag) && after.startsWith(endTag)) {
            const newBefore = before.substring(0, before.length - startTag.length);
            const newAfter = after.substring(endTag.length);
            textarea.value = newBefore + newAfter;
            textarea.focus();
            textarea.setSelectionRange(newBefore.length, newBefore.length);
        }
        else {
            textarea.value = before + startTag + endTag + after;
            textarea.focus();
            textarea.setSelectionRange(actualCursor + startTag.length, actualCursor + startTag.length);
        }
    }

    updateLivePreview(textarea, previewElement);

    const event = new Event('input');
    textarea.dispatchEvent(event);
}

function clearSelectionFormatting(textarea, previewElement) {
    let text = textarea.value ?? "";
    let cursor = textarea.selectionStart;
    let selectionLength = textarea.selectionEnd - cursor;

    if (selectionLength > 0 && cursor >= 0 && cursor + selectionLength <= text.length) {
        let before = text.substring(0, cursor);
        let selected = text.substring(cursor, cursor + selectionLength);
        let after = text.substring(cursor + selectionLength);

        // Strip all tags inside the selection
        let cleaned = selected.replace(/<[^>]+>/g, "");

        // Strip bordering tags cleanly
        while (true) {
            let openBeforeMatch = before.match(/<[^>]+>$/);
            let closeAfterMatch = after.match(/^<\/[^>]+>/);
            if (openBeforeMatch && closeAfterMatch) {
                before = before.substring(0, before.length - openBeforeMatch[0].length);
                after = after.substring(closeAfterMatch[0].length);
            } else {
                let borderBefore = before.match(/<[^>]+>$/);
                let borderAfter = after.match(/^<[^>]+>/);
                if (borderBefore && borderAfter) {
                    before = before.substring(0, before.length - borderBefore[0].length);
                    after = after.substring(borderAfter[0].length);
                } else {
                    break;
                }
            }
        }

        textarea.value = before + cleaned + after;
        textarea.focus();
        textarea.setSelectionRange(before.length, before.length + cleaned.length);
    } else {
        // Strip everything if no selection
        let cleaned = text.replace(/<[^>]+>/g, "");
        textarea.value = cleaned;
        textarea.focus();
        textarea.setSelectionRange(Math.min(cursor, cleaned.length), Math.min(cursor, cleaned.length));
    }

    updateLivePreview(textarea, previewElement);

    const event = new Event('input');
    textarea.dispatchEvent(event);
}

function showColorDropdown(button, textarea, previewElement) {
    const existing = document.querySelector('.color-dropdown');
    if (existing) existing.remove();

    const dropdown = document.createElement('div');
    dropdown.className = 'color-dropdown';
    dropdown.style.position = 'absolute';
    dropdown.style.background = 'rgba(25, 25, 35, 0.95)';
    dropdown.style.border = '1px solid rgba(255, 255, 255, 0.1)';
    dropdown.style.borderRadius = '8px';
    dropdown.style.padding = '8px';
    dropdown.style.zIndex = '10000';
    dropdown.style.display = 'flex';
    dropdown.style.flexWrap = 'wrap';
    dropdown.style.gap = '6px';
    dropdown.style.width = '140px';
    dropdown.style.boxShadow = '0 10px 25px rgba(0,0,0,0.5)';
    dropdown.style.backdropFilter = 'blur(12px)';

    const colors = [
        { name: 'Red', hex: '#ef4444' },
        { name: 'Green', hex: '#22c55e' },
        { name: 'Blue', hex: '#3b82f6' },
        { name: 'Yellow', hex: '#eab308' },
        { name: 'Orange', hex: '#f97316' },
        { name: 'Purple', hex: '#a855f7' }
    ];

    colors.forEach(c => {
        const item = document.createElement('div');
        item.style.width = '24px';
        item.style.height = '24px';
        item.style.borderRadius = '50%';
        item.style.backgroundColor = c.hex;
        item.style.cursor = 'pointer';
        item.style.border = '1px solid rgba(255, 255, 255, 0.1)';
        item.title = c.name;
        item.onclick = (e) => {
            e.stopPropagation();
            wrapSelection(textarea, `<color=${c.hex}>`, '</color>', previewElement);
            dropdown.remove();
        };
        dropdown.appendChild(item);
    });

    // Custom Color Picker input
    const pickerLabel = document.createElement('div');
    pickerLabel.innerText = 'Custom:';
    pickerLabel.style.color = '#fff';
    pickerLabel.style.fontSize = '10px';
    pickerLabel.style.width = '100%';
    pickerLabel.style.marginTop = '4px';
    dropdown.appendChild(pickerLabel);

    const customPicker = document.createElement('input');
    customPicker.type = 'color';
    customPicker.value = '#ef4444';
    customPicker.style.width = '100%';
    customPicker.style.height = '28px';
    customPicker.style.border = '1px solid rgba(255, 255, 255, 0.2)';
    customPicker.style.borderRadius = '4px';
    customPicker.style.cursor = 'pointer';
    customPicker.style.padding = '0';
    customPicker.style.backgroundColor = 'transparent';
    customPicker.onchange = (e) => {
        wrapSelection(textarea, `<color=${customPicker.value}>`, '</color>', previewElement);
        dropdown.remove();
    };
    dropdown.appendChild(customPicker);

    const closeBtn = document.createElement('button');
    closeBtn.innerText = '✕ Close';
    closeBtn.className = 'btn-format';
    closeBtn.style.width = '100%';
    closeBtn.style.marginTop = '4px';
    closeBtn.onclick = (e) => {
        e.stopPropagation();
        dropdown.remove();
    };
    dropdown.appendChild(closeBtn);

    const rect = button.getBoundingClientRect();
    const bounds = container.getBoundingClientRect();
    
    const left = (rect.left - bounds.left - panX) / zoom;
    const top = (rect.bottom - bounds.top - panY) / zoom;

    dropdown.style.left = `${left}px`;
    dropdown.style.top = `${top}px`;
    dropdown.style.transformOrigin = 'top left';
    dropdown.style.pointerEvents = 'auto';

    const onOutsideClick = (e) => {
        if (!dropdown.contains(e.target) && e.target !== button) {
            dropdown.remove();
            document.removeEventListener('mousedown', onOutsideClick);
        }
    };
    document.addEventListener('mousedown', onOutsideClick);

    nodesLayer.appendChild(dropdown);
}

function showHighlightDropdown(button, textarea, previewElement) {
    const existing = document.querySelector('.color-dropdown');
    if (existing) existing.remove();

    const dropdown = document.createElement('div');
    dropdown.className = 'color-dropdown';
    dropdown.style.position = 'absolute';
    dropdown.style.background = 'rgba(25, 25, 35, 0.95)';
    dropdown.style.border = '1px solid rgba(255, 255, 255, 0.1)';
    dropdown.style.borderRadius = '8px';
    dropdown.style.padding = '8px';
    dropdown.style.zIndex = '10000';
    dropdown.style.display = 'flex';
    dropdown.style.flexWrap = 'wrap';
    dropdown.style.gap = '6px';
    dropdown.style.width = '140px';
    dropdown.style.boxShadow = '0 10px 25px rgba(0,0,0,0.5)';
    dropdown.style.backdropFilter = 'blur(12px)';

    const highlights = [
        { name: 'Yellow', hex: '#eab30855' },
        { name: 'Green', hex: '#22c55e55' },
        { name: 'Blue', hex: '#3b82f655' },
        { name: 'Red', hex: '#ef444455' },
        { name: 'Orange', hex: '#f9731655' },
        { name: 'Purple', hex: '#a855f755' }
    ];

    highlights.forEach(c => {
        const item = document.createElement('div');
        item.style.width = '24px';
        item.style.height = '24px';
        item.style.borderRadius = '50%';
        item.style.backgroundColor = c.hex.substring(0, 7);
        item.style.cursor = 'pointer';
        item.style.border = '1px solid rgba(255, 255, 255, 0.1)';
        item.title = c.name;
        item.onclick = (e) => {
            e.stopPropagation();
            wrapSelection(textarea, `<mark=${c.hex}>`, '</mark>', previewElement);
            dropdown.remove();
        };
        dropdown.appendChild(item);
    });

    // Custom Color Picker input
    const pickerLabel = document.createElement('div');
    pickerLabel.innerText = 'Custom:';
    pickerLabel.style.color = '#fff';
    pickerLabel.style.fontSize = '10px';
    pickerLabel.style.width = '100%';
    pickerLabel.style.marginTop = '4px';
    dropdown.appendChild(pickerLabel);

    const customPicker = document.createElement('input');
    customPicker.type = 'color';
    customPicker.value = '#eab308';
    customPicker.style.width = '100%';
    customPicker.style.height = '28px';
    customPicker.style.border = '1px solid rgba(255, 255, 255, 0.2)';
    customPicker.style.borderRadius = '4px';
    customPicker.style.cursor = 'pointer';
    customPicker.style.padding = '0';
    customPicker.style.backgroundColor = 'transparent';
    customPicker.onchange = (e) => {
        wrapSelection(textarea, `<mark=${customPicker.value}55>`, '</mark>', previewElement);
        dropdown.remove();
    };
    dropdown.appendChild(customPicker);

    const closeBtn = document.createElement('button');
    closeBtn.innerText = '✕ Close';
    closeBtn.className = 'btn-format';
    closeBtn.style.width = '100%';
    closeBtn.style.marginTop = '4px';
    closeBtn.onclick = (e) => {
        e.stopPropagation();
        dropdown.remove();
    };
    dropdown.appendChild(closeBtn);

    const rect = button.getBoundingClientRect();
    const bounds = container.getBoundingClientRect();
    
    const left = (rect.left - bounds.left - panX) / zoom;
    const top = (rect.bottom - bounds.top - panY) / zoom;

    dropdown.style.left = `${left}px`;
    dropdown.style.top = `${top}px`;
    dropdown.style.transformOrigin = 'top left';
    dropdown.style.pointerEvents = 'auto';

    const onOutsideClick = (e) => {
        if (!dropdown.contains(e.target) && e.target !== button) {
            dropdown.remove();
            document.removeEventListener('mousedown', onOutsideClick);
        }
    };
    document.addEventListener('mousedown', onOutsideClick);

    nodesLayer.appendChild(dropdown);
}

function createFormattingToolbar(textarea, previewElement, fieldName, node) {
    const toolbar = document.createElement('div');
    toolbar.className = 'formatting-toolbar';
    toolbar.style.display = 'flex';
    toolbar.style.gap = '4px';
    toolbar.style.marginBottom = '6px';
    toolbar.style.alignItems = 'center';

    const btnB = document.createElement('button');
    btnB.innerText = 'B';
    btnB.className = 'btn-format';
    btnB.style.fontWeight = 'bold';
    btnB.onmousedown = (e) => { e.preventDefault(); };
    btnB.onclick = (e) => { e.preventDefault(); wrapSelection(textarea, '<b>', '</b>', previewElement); };
    toolbar.appendChild(btnB);

    const btnI = document.createElement('button');
    btnI.innerText = 'I';
    btnI.className = 'btn-format';
    btnI.style.fontStyle = 'italic';
    btnI.onmousedown = (e) => { e.preventDefault(); };
    btnI.onclick = (e) => { e.preventDefault(); wrapSelection(textarea, '<i>', '</i>', previewElement); };
    toolbar.appendChild(btnI);

    const btnU = document.createElement('button');
    btnU.innerText = 'U';
    btnU.className = 'btn-format';
    btnU.style.textDecoration = 'underline';
    btnU.onmousedown = (e) => { e.preventDefault(); };
    btnU.onclick = (e) => { e.preventDefault(); wrapSelection(textarea, '<u>', '</u>', previewElement); };
    toolbar.appendChild(btnU);

    const btnColor = document.createElement('button');
    btnColor.innerHTML = '🎨 Color';
    btnColor.className = 'btn-format';
    btnColor.style.fontSize = '10px';
    btnColor.onmousedown = (e) => { e.preventDefault(); };
    btnColor.onclick = (e) => {
        e.preventDefault();
        showColorDropdown(btnColor, textarea, previewElement);
    };
    toolbar.appendChild(btnColor);

    const btnHighlight = document.createElement('button');
    btnHighlight.innerHTML = '🖊️ Highlight';
    btnHighlight.className = 'btn-format';
    btnHighlight.style.fontSize = '10px';
    btnHighlight.onmousedown = (e) => { e.preventDefault(); };
    btnHighlight.onclick = (e) => { 
        e.preventDefault(); 
        showHighlightDropdown(btnHighlight, textarea, previewElement);
    };
    toolbar.appendChild(btnHighlight);

    const btnClear = document.createElement('button');
    btnClear.innerHTML = '✕ Clear';
    btnClear.className = 'btn-format';
    btnClear.style.fontSize = '10px';
    btnClear.onmousedown = (e) => { e.preventDefault(); };
    btnClear.onclick = (e) => { 
        e.preventDefault(); 
        clearSelectionFormatting(textarea, previewElement); 
    };
    toolbar.appendChild(btnClear);

    const btnCompose = document.createElement('button');
    btnCompose.innerHTML = '📝 Compose';
    btnCompose.className = 'btn-format';
    btnCompose.style.fontSize = '10px';
    btnCompose.onmousedown = (e) => { e.preventDefault(); };
    btnCompose.onclick = (e) => {
        e.preventDefault();
        const currentText = textarea.value;
        let composeUrl = "compose?nodeId=" + node.id + "&fieldName=" + fieldName + "&currentText=" + encodeURIComponent(currentText);
        
        const loopContext = getLoopContextInfo(node.id);
        if (loopContext) {
            composeUrl += "&loopSource=" + encodeURIComponent(loopContext.source) + "&loopArrayVar=" + encodeURIComponent(loopContext.arrayVar);
        }

        if (typeof invokeCSharpAction === 'function') {
            invokeCSharpAction(composeUrl);
        } else {
            window.location.href = "rags-action://" + composeUrl;
        }
    };
    toolbar.appendChild(btnCompose);

    const btnDictate = document.createElement('button');
    btnDictate.innerHTML = '🎙️';
    btnDictate.className = 'btn-format';
    btnDictate.style.fontSize = '10px';
    btnDictate.setAttribute('data-original-html', '🎙️');
    btnDictate.onmousedown = (e) => { e.preventDefault(); };
    btnDictate.onclick = (e) => {
        e.preventDefault();
        toggleSpeechRecognition(textarea, btnDictate);
    };
    toolbar.appendChild(btnDictate);

    // Glowing ✨ AI dialogue trigger calling native C# DI chat service co-author bridge
    const btnAI = document.createElement('button');
    btnAI.innerHTML = '✨ AI dialogue';
    btnAI.className = 'btn-format ai-glow';
    btnAI.style.marginLeft = 'auto';
    btnAI.onmousedown = (e) => { e.preventDefault(); };
    btnAI.onclick = (e) => {
        e.preventDefault();
        const currentText = textarea.value;
        const aiUrl = "ai?nodeId=" + node.id + "&fieldName=" + fieldName + "&currentText=" + encodeURIComponent(currentText);
        if (typeof invokeCSharpAction === 'function') {
            invokeCSharpAction(aiUrl);
        } else {
            window.location.href = "rags-action://" + aiUrl;
        }
    };
    toolbar.appendChild(btnAI);

    return toolbar;
}



// Node Engine Creation Methods
function createBaseNode(id, type, title, x, y) {
    const el = document.createElement('div');
    el.className = `node ${type}`;
    el.style.left = `${x}px`;
    el.style.top = `${y}px`;
    el.style.width = '320px';
    el.id = id;

    const header = document.createElement('div');
    header.className = `node-header ${type}`;
    
    if (type === 'start') {
        header.innerHTML = `<span>${title}</span>`;
    } else {
        header.innerHTML = `<span>${title}</span><span class="node-delete" onclick="deleteNode('${id}')">✕</span>`;
    }
    el.appendChild(header);

    const body = document.createElement('div');
    body.className = 'node-body';
    el.appendChild(body);

    nodesLayer.appendChild(el);

    makeDraggable(el);

    const nodeObj = {
        id,
        type,
        x,
        y,
        width: 320,
        height: null,
        element: el,
        bodyElement: body,
        choices: [],
        data: {},
        inputs: []
    };

    // Add drag resizing handle for nodes
    const resizer = document.createElement('div');
    resizer.className = 'node-resizer';
    el.appendChild(resizer);

    resizer.addEventListener('mousedown', (e) => {
        e.stopPropagation();
        e.preventDefault();

        const startWidth = el.offsetWidth;
        const startHeight = el.offsetHeight;
        const startX = e.clientX;
        const startY = e.clientY;

        const onMouseMove = (ev) => {
            const newWidth = Math.max(200, startWidth + (ev.clientX - startX) / zoom);
            const newHeight = Math.max(80, startHeight + (ev.clientY - startY) / zoom);

            el.style.width = `${newWidth}px`;
            el.style.height = `${newHeight}px`;

            nodeObj.width = newWidth;
            nodeObj.height = newHeight;
            redrawConnections();
        };

        const onMouseUp = () => {
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);
            triggerAutoSave();
        };

        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
    });

    nodes.push(nodeObj);
    return nodeObj;
}

function autoLinkNodeIfPossible(newNode) {
    if (isLoadingGraph) return;

    const allOutputPins = Array.from(document.querySelectorAll('.pin.output'));
    const unconnectedOutputPins = allOutputPins.filter(pinEl => {
        if (pinEl.id.startsWith(newNode.id)) return false;
        if (pinEl.style.display === 'none' || window.getComputedStyle(pinEl).display === 'none') return false;
        return !connections.some(c => c.fromPinId === pinEl.id);
    });

    if (unconnectedOutputPins.length === 1) {
        const fromPin = unconnectedOutputPins[0];
        const toPinId = `${newNode.id}_in`;
        const toPin = document.getElementById(toPinId);
        if (toPin) {
            connections.push({
                fromPinId: fromPin.id,
                toPinId: toPinId,
                type: 'exec'
            });
            redrawConnections();
            triggerAutoSave();
        }
    }
}

function makeDraggable(el) {
    let startPositions = [];

    el.addEventListener('mousedown', (e) => {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT' || e.target.classList.contains('pin') || e.target.classList.contains('node-delete') || e.target.classList.contains('btn-format')) {
            return;
        }
        e.stopPropagation();

        const clickedNode = nodes.find(n => n.id === el.id);
        if (!clickedNode) return;

        const isMulti = e.ctrlKey || e.metaKey || e.shiftKey;

        if (isMulti) {
            // Toggle selection of clicked node
            const idx = selectedNodes.indexOf(clickedNode);
            if (idx > -1) {
                selectedNodes.splice(idx, 1);
                el.classList.remove('selected');
                if (selectedNode === clickedNode) {
                    selectedNode = selectedNodes[selectedNodes.length - 1] || null;
                }
            } else {
                selectedNodes.push(clickedNode);
                el.classList.add('selected');
                selectedNode = clickedNode;
            }
        } else {
            // If clicked node is not already in selectedNodes, make it the only selected node
            if (!selectedNodes.includes(clickedNode)) {
                deselectAllNodes();
                selectedNodes.push(clickedNode);
                el.classList.add('selected');
                selectedNode = clickedNode;
            }
        }

        const startMouseX = e.clientX;
        const startMouseY = e.clientY;

        startPositions = selectedNodes.map(node => ({
            node: node,
            startX: node.x,
            startY: node.y
        }));

        const onMouseMove = (ev) => {
            const dx = (ev.clientX - startMouseX) / zoom;
            const dy = (ev.clientY - startMouseY) / zoom;

            startPositions.forEach(pos => {
                const newX = pos.startX + dx;
                const newY = pos.startY + dy;
                pos.node.x = newX;
                pos.node.y = newY;
                pos.node.element.style.left = `${newX}px`;
                pos.node.element.style.top = `${newY}px`;
            });

            redrawConnections();
        };

        const onMouseUp = () => {
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);
            triggerAutoSave(); // Auto-save coordinates on drag end!
        };

        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
    });
}

// Port Configuration Helper
function addPin(node, direction, type, name, pinId) {
    const row = document.createElement('div');
    row.className = `port-row ${direction}`;
    row.style.textAlign = direction === 'input' ? 'left' : 'right';
    row.innerText = name;

    const pin = document.createElement('div');
    pin.className = `pin ${direction} ${type}`;
    pin.id = pinId;
    
    // Fix closure capture renaming bug: dynamically query pin.id inside events
    pin.addEventListener('mousedown', (e) => {
        e.stopPropagation();
        activeDrawingPin = { id: pin.id, direction, type, node };
    });

    pin.addEventListener('mouseup', (e) => {
        e.stopPropagation();
        if (activeDrawingPin && activeDrawingPin.id !== pin.id && activeDrawingPin.direction !== direction) {
            const from = direction === 'input' ? activeDrawingPin.id : pin.id;
            const to = direction === 'input' ? pin.id : activeDrawingPin.id;
            
            // Limit output pins to only a single connection to enforce standard sequential execution flows
            connections = connections.filter(c => c.fromPinId !== from);
            // Limit input pins to only a single connection to enforce standard sequential execution flows
            connections = connections.filter(c => c.toPinId !== to);

            if (!connections.some(c => c.fromPinId === from && c.toPinId === to)) {
                connections.push({
                    fromPinId: from,
                    toPinId: to,
                    type: activeDrawingPin.type
                });
                triggerAutoSave(); // Auto-save on link creation!
            }
        }
        activeDrawingPin = null;
        redrawConnections();
    });

    row.appendChild(pin);
    node.bodyElement.appendChild(row);
}

function isSameDirection(d1, d2) {
    if (!d1 || !d2) return false;
    const a = String(d1).trim().toLowerCase();
    const b = String(d2).trim().toLowerCase();
    if (a === b) return true;
    if ((a === 'n' || a === 'north') && (b === 'n' || b === 'north')) return true;
    if ((a === 's' || a === 'south') && (b === 's' || b === 'south')) return true;
    if ((a === 'e' || a === 'east') && (b === 'e' || b === 'east')) return true;
    if ((a === 'w' || a === 'west') && (b === 'w' || b === 'west')) return true;
    if ((a === 'nw' || a === 'northwest') && (b === 'nw' || b === 'northwest')) return true;
    if ((a === 'ne' || a === 'northeast') && (b === 'ne' || b === 'northeast')) return true;
    if ((a === 'sw' || a === 'southwest') && (b === 'sw' || b === 'southwest')) return true;
    if ((a === 'se' || a === 'southeast') && (b === 'se' || b === 'southeast')) return true;
    return false;
}

// Create the permanently fixed Start node
function createStartNode() {
    let startNode = nodes.find(n => n.id === 'start');
    if (startNode) {
        if (startNode._nameInp) startNode._nameInp.value = activeActionName;
        if (startNode._triggerSelect) startNode._triggerSelect.value = activeActionTrigger;
        if (startNode._dirSelect) {
            for (let i = 0; i < startNode._dirSelect.options.length; i++) {
                if (isSameDirection(startNode._dirSelect.options[i].value, activeActionDirectionFilter || "All")) {
                    startNode._dirSelect.selectedIndex = i;
                    break;
                }
            }
        }
        if (startNode._activeChk) startNode._activeChk.checked = activeActionInitallyActive;
        if (typeof startNode._updateDirVisibility === 'function') startNode._updateDirVisibility();
        return startNode;
    }

    const node = createBaseNode('start', 'start', '🚀 Action Start', 50, 150);
    
    // Action Name Input
    const nameLabel = document.createElement('label');
    nameLabel.innerText = "Action Name:";
    nameLabel.style.fontSize = "10px";
    nameLabel.style.color = "var(--text-muted)";
    nameLabel.style.marginTop = "4px";
    nameLabel.style.display = "block";
    node.bodyElement.appendChild(nameLabel);

    const nameInp = document.createElement('input');
    nameInp.type = 'text';
    nameInp.value = activeActionName;
    nameInp.style.width = "90%";
    nameInp.style.marginBottom = "8px";
    nameInp.addEventListener('input', () => {
        activeActionName = nameInp.value;
        const titleEl = document.getElementById("editor-title");
        if (titleEl) {
            titleEl.innerText = "Editing Action: " + activeActionName;
        }
        triggerAutoSave();
    });
    node.bodyElement.appendChild(nameInp);

    // Trigger Event Dropdown
    const triggerLabel = document.createElement('label');
    triggerLabel.innerText = "Trigger Event:";
    triggerLabel.style.fontSize = "10px";
    triggerLabel.style.color = "var(--text-muted)";
    triggerLabel.style.display = "block";
    node.bodyElement.appendChild(triggerLabel);

    const triggerSelect = document.createElement('select');
    triggerSelect.style.width = "95%";
    triggerSelect.style.marginBottom = "8px";
    triggerSelect.style.backgroundColor = "#2a2a2a";
    triggerSelect.style.color = "#ffffff";
    triggerSelect.style.border = "1px solid #444";
    triggerSelect.style.borderRadius = "4px";
    triggerSelect.style.padding = "4px";

    const triggers = [
        { val: "UserClicked", label: "User Clicked" },
        { val: "OnGameStart", label: "On Game Start" },
        { val: "OnGameLoad", label: "On Game Load" },
        { val: "OnTurnTick", label: "On Turn Tick" },
        { val: "OnPlayerEnter", label: "On Player Enter" },
        { val: "OnPlayerExit", label: "On Player Exit" },
        { val: "OnCharacterEnter", label: "On Character Enter" },
        { val: "OnCharacterExit", label: "On Character Exit" },
        { val: "OnCharacterKilled", label: "On Character Killed" },
        { val: "OnObjectExamined", label: "On Object Examined" },
        { val: "OnObjectTaken", label: "On Object Taken" },
        { val: "OnObjectDropped", label: "On Object Dropped" }
    ];

    triggers.forEach(t => {
        const opt = document.createElement('option');
        opt.value = t.val;
        opt.innerText = t.label;
        if (t.val === activeActionTrigger) {
            opt.selected = true;
        }
        triggerSelect.appendChild(opt);
    });

    // Direction Filter Container
    const dirContainer = document.createElement('div');
    dirContainer.style.marginTop = "4px";
    dirContainer.style.marginBottom = "8px";

    const dirLabel = document.createElement('label');
    dirLabel.innerText = "Direction Filter:";
    dirLabel.style.fontSize = "10px";
    dirLabel.style.color = "var(--text-muted)";
    dirLabel.style.display = "block";
    dirContainer.appendChild(dirLabel);

    const dirSelect = document.createElement('select');
    dirSelect.style.width = "95%";
    dirSelect.style.backgroundColor = "#2a2a2a";
    dirSelect.style.color = "#ffffff";
    dirSelect.style.border = "1px solid #444";
    dirSelect.style.borderRadius = "4px";
    dirSelect.style.padding = "4px";

    const directions = ["All", "N", "S", "E", "W", "NW", "NE", "SW", "SE", "Up", "Down", "In", "Out"];
    directions.forEach(d => {
        const opt = document.createElement('option');
        opt.value = d;
        opt.innerText = d;
        if (isSameDirection(d, activeActionDirectionFilter || "All")) {
            opt.selected = true;
        }
        dirSelect.appendChild(opt);
    });

    dirSelect.addEventListener('change', () => {
        console.log("[graph_editor] dirSelect changed from", activeActionDirectionFilter, "to", dirSelect.value);
        activeActionDirectionFilter = dirSelect.value;
        triggerAutoSave();
    });
    dirContainer.appendChild(dirSelect);

    const updateDirVisibility = () => {
        const needsDir = ["OnPlayerEnter", "OnPlayerExit", "OnCharacterEnter", "OnCharacterExit"].includes(activeActionTrigger);
        dirContainer.style.display = needsDir ? "block" : "none";
    };

    triggerSelect.addEventListener('change', () => {
        activeActionTrigger = triggerSelect.value;
        updateDirVisibility();
        triggerAutoSave();
    });
    
    // Set initial visibility
    updateDirVisibility();

    node.bodyElement.appendChild(triggerSelect);
    node.bodyElement.appendChild(dirContainer);

    // Initially Active Checkbox
    const activeRow = document.createElement('div');
    activeRow.style.display = 'flex';
    activeRow.style.alignItems = 'center';
    activeRow.style.gap = '8px';
    activeRow.style.marginTop = '4px';
    activeRow.style.marginBottom = '8px';

    const activeChk = document.createElement('input');
    activeChk.type = 'checkbox';
    activeChk.checked = activeActionInitallyActive;
    activeChk.addEventListener('change', () => {
        activeActionInitallyActive = activeChk.checked;
        triggerAutoSave();
    });

    const activeLabel = document.createElement('label');
    activeLabel.innerText = "Initially Active";
    activeLabel.style.fontSize = "10px";
    activeLabel.style.color = "var(--text-muted)";

    node._nameInp = nameInp;
    node._triggerSelect = triggerSelect;
    node._dirSelect = dirSelect;
    node._activeChk = activeChk;
    node._updateDirVisibility = updateDirVisibility;

    activeRow.appendChild(activeChk);
    activeRow.appendChild(activeLabel);
    node.bodyElement.appendChild(activeRow);

    addPin(node, 'output', 'exec', 'Trigger', 'start_out');
    return node;
}

// Custom Dialogue Nodes (Auto-generates clean unique IDs directly at creation, fixing child input resolution)
function addNewDialogueNode(x = null, y = null) {
    if (x === null || y === null) {
        const center = getViewportCenterCoordinates();
        x = center.x - 160;
        y = center.y - 120;
    }
    const id = 'dialogue_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
    const node = createBaseNode(id, 'dialogue', '💬 NPC Dialogue', x, y);

    addPin(node, 'input', 'exec', 'Entry', `${id}_in`);

    const promptLabel = document.createElement('label');
    promptLabel.innerText = "Character Lines:";
    promptLabel.style.fontSize = "10px";
    promptLabel.style.color = "var(--text-muted)";
    node.bodyElement.appendChild(promptLabel);

    const txt = document.createElement('textarea');
    txt.placeholder = "\"What the character says...\"";
    txt.addEventListener('input', () => { 
        node.data.characterLines = txt.value; 
        triggerAutoSave(); // Auto-save on keystroke/input
    });

    node.bodyElement.appendChild(createFormattingToolbar(txt, null, 'characterLines', node));
    node.bodyElement.appendChild(txt);

    const choicesList = document.createElement('div');
    choicesList.id = `${id}_choices_container`;
    node.bodyElement.appendChild(choicesList);

    const btn = document.createElement('button');
    btn.className = 'add-choice-btn';
    btn.innerText = "+ Add Choice";
    btn.onclick = () => {
        addDialogueChoiceRow(node, choicesList, "", Date.now());
        triggerAutoSave();
    };
    node.bodyElement.appendChild(btn);

    autoLinkNodeIfPossible(node);
    return node;
}

function addDialogueChoiceRow(node, container, initialText, choiceId) {
    const rowId = `choice_${choiceId}`;
    const row = document.createElement('div');
    row.style.display = 'flex';
    row.style.gap = '4px';
    row.style.alignItems = 'center';
    row.style.marginBottom = '6px';
    row.id = rowId;

    const inp = document.createElement('input');
    inp.value = initialText || "";
    inp.placeholder = "\"Player choice...\"";
    inp.style.flex = "1";
    inp.addEventListener('input', () => {
        triggerAutoSave();
    });
    row.appendChild(inp);

    const del = document.createElement('span');
    del.innerHTML = "✕";
    del.style.cursor = "pointer";
    del.style.fontSize = "12px";
    del.style.color = "var(--pin-false)";
    del.style.marginRight = "10px";
    del.style.userSelect = "none";
    del.onclick = () => {
        row.remove();
        connections = connections.filter(c => c.fromPinId !== `${rowId}_out`);
        node.choices = node.choices.filter(c => c.id !== choiceId);
        redrawConnections();
        triggerAutoSave();
    };
    row.appendChild(del);

    const pin = document.createElement('div');
    pin.className = 'pin output dialogue-choice';
    pin.id = `${rowId}_out`;
    
    pin.addEventListener('mousedown', (e) => {
        e.stopPropagation();
        activeDrawingPin = { id: pin.id, direction: 'output', type: 'dialogue-choice', node };
    });
    pin.addEventListener('mouseup', (e) => {
        e.stopPropagation();
        if (activeDrawingPin && activeDrawingPin.id !== pin.id && activeDrawingPin.direction === 'input') {
            // Limit choice output pin to only a single connection to enforce standard sequential execution flows
            connections = connections.filter(c => c.fromPinId !== pin.id);
            // Limit input pin to only a single connection to enforce standard sequential execution flows
            connections = connections.filter(c => c.toPinId !== activeDrawingPin.id);
            connections.push({ fromPinId: pin.id, toPinId: activeDrawingPin.id, type: 'dialogue-choice' });
            triggerAutoSave();
        }
        activeDrawingPin = null;
        redrawConnections();
    });
    row.appendChild(pin);

    container.appendChild(row);

    const choiceObj = { id: choiceId, textElement: inp, rowId };
    node.choices.push(choiceObj);
}

function addNewSwitchNode(x = null, y = null) {
    if (x === null || y === null) {
        const center = getViewportCenterCoordinates();
        x = center.x - 160;
        y = center.y - 120;
    }
    const id = 'switch_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
    const node = createBaseNode(id, 'switch', '🔀 Switch Control Flow', x, y);

    addPin(node, 'input', 'exec', 'Entry', `${id}_in`);

    // Expression input
    const exprLabel = document.createElement('label');
    exprLabel.innerText = "Expression:";
    exprLabel.style.fontSize = "10px";
    exprLabel.style.color = "var(--text-muted)";
    node.bodyElement.appendChild(exprLabel);

    const exprInput = document.createElement('input');
    exprInput.type = 'text';
    exprInput.placeholder = "e.g. {var.State}";
    exprInput.style.width = '90%';
    exprInput.style.marginBottom = '8px';
    exprInput.value = node.data.expression || "";
    exprInput.addEventListener('input', () => {
        node.data.expression = exprInput.value;
        triggerAutoSave();
    });
    node.bodyElement.appendChild(exprInput);

    // Static Default pin
    addPin(node, 'output', 'exec', 'Default', `${id}_default`);

    // Cases list container
    const casesList = document.createElement('div');
    casesList.id = `${id}_cases_container`;
    node.bodyElement.appendChild(casesList);

    node.cases = [];

    const btn = document.createElement('button');
    btn.className = 'add-choice-btn';
    btn.innerText = "+ Add Case";
    btn.onclick = () => {
        addSwitchCaseRow(node, casesList, "", Date.now());
        triggerAutoSave();
    };
    node.bodyElement.appendChild(btn);

    autoLinkNodeIfPossible(node);
    return node;
}

function addSwitchCaseRow(node, container, initialText, caseId) {
    const rowId = `case_${caseId}`;
    const row = document.createElement('div');
    row.style.display = 'flex';
    row.style.gap = '4px';
    row.style.alignItems = 'center';
    row.style.marginBottom = '6px';
    row.id = rowId;

    const inp = document.createElement('input');
    inp.type = 'text';
    inp.style.flex = '1';
    inp.placeholder = "Value (e.g. 1)";
    inp.value = initialText;
    inp.addEventListener('input', () => {
        triggerAutoSave();
    });
    row.appendChild(inp);

    const del = document.createElement('span');
    del.innerHTML = "✕";
    del.style.cursor = "pointer";
    del.style.fontSize = "12px";
    del.style.color = "var(--pin-false)";
    del.style.marginRight = "10px";
    del.style.userSelect = "none";
    del.onclick = () => {
        node.cases = node.cases.filter(c => c.id !== caseId);
        connections = connections.filter(c => c.fromPinId !== `${rowId}_out`);
        redrawConnections();
        row.remove();
        triggerAutoSave();
    };
    row.appendChild(del);

    const pin = document.createElement('div');
    pin.id = `${rowId}_out`;
    pin.className = 'pin output switch-case';
    pin.style.backgroundColor = '#94A3B8';

    // Make pin connectable
    pin.addEventListener('mousedown', (e) => {
        e.stopPropagation();
        activeDrawingPin = { id: pin.id, direction: 'output', type: 'switch-case', node };
        drawTempConnection(e);
    });
    row.appendChild(pin);

    container.appendChild(row);

    const caseObj = { id: caseId, textElement: inp, rowId };
    node.cases.push(caseObj);
}


function setupSearchableDropdown(select, items) {
    select.style.display = 'none';

    const wrapper = document.createElement('div');
    wrapper.className = 'searchable-dropdown';
    select.parentNode.insertBefore(wrapper, select.nextSibling);

    const header = document.createElement('button');
    header.className = 'searchable-dropdown-header';
    header.type = 'button';
    
    const getActiveLabel = (val) => {
        const item = items.find(i => i.type === val);
        if (!item) return val || 'Select...';
        let label = item.label;
        if (item.category && label.startsWith(item.category + ":")) {
            label = label.substring(item.category.length + 1).trim();
        }
        return (item.category ? `[${item.category}] ` : "") + label;
    };
    
    header.innerText = getActiveLabel(select.value);
    wrapper.appendChild(header);

    const popup = document.createElement('div');
    popup.className = 'searchable-dropdown-popup';
    wrapper.appendChild(popup);

    const searchWrapper = document.createElement('div');
    searchWrapper.className = 'searchable-dropdown-search-wrapper';
    const searchInput = document.createElement('input');
    searchInput.type = 'text';
    searchInput.placeholder = 'Search...';
    searchWrapper.appendChild(searchInput);
    popup.appendChild(searchWrapper);

    const listContainer = document.createElement('div');
    listContainer.className = 'searchable-dropdown-list';
    popup.appendChild(listContainer);

    let currentValueList = items;

    const renderList = (filterText = '') => {
        listContainer.innerHTML = '';
        const groups = {};
        currentValueList.forEach(item => {
            const cat = item.category || 'General';
            let label = item.label;
            if (item.category && label.startsWith(item.category + ":")) {
                label = label.substring(item.category.length + 1).trim();
            }
            
            const matchFilter = !filterText || 
                label.toLowerCase().includes(filterText.toLowerCase()) || 
                cat.toLowerCase().includes(filterText.toLowerCase());

            if (matchFilter) {
                if (!groups[cat]) groups[cat] = [];
                groups[cat].push({ item, cleanLabel: label });
            }
        });

        const sortedCategories = Object.keys(groups).sort((a, b) => {
            if (a === 'General') return -1;
            if (b === 'General') return 1;
            return a.localeCompare(b);
        });

        sortedCategories.forEach(cat => {
            const catHeader = document.createElement('div');
            catHeader.className = 'searchable-dropdown-category';
            catHeader.innerText = cat;
            
            const isSearching = !filterText;
            const expandedKey = `dropdown_cat_${cat}_expanded`;
            let isExpanded = !isSearching || (wrapper.dataset[expandedKey] === 'true');
            
            if (isExpanded) {
                catHeader.classList.add('expanded');
            }

            catHeader.addEventListener('click', (e) => {
                e.stopPropagation();
                if (!isSearching) return;
                isExpanded = !isExpanded;
                wrapper.dataset[expandedKey] = isExpanded;
                catHeader.classList.toggle('expanded', isExpanded);
            });

            listContainer.appendChild(catHeader);

            const itemsContainer = document.createElement('div');
            itemsContainer.className = 'searchable-dropdown-category-items';
            
            const sortedGroupItems = groups[cat].sort((a, b) => a.cleanLabel.localeCompare(b.cleanLabel));
            sortedGroupItems.forEach(({ item, cleanLabel }) => {
                const itemDiv = document.createElement('div');
                itemDiv.className = 'searchable-dropdown-item';
                if (item.type === select.value) {
                    itemDiv.classList.add('selected');
                }
                itemDiv.innerText = cleanLabel;

                itemDiv.addEventListener('click', (e) => {
                    e.stopPropagation();
                    select.value = item.type;
                    header.innerText = getActiveLabel(item.type);
                    select.dispatchEvent(new Event('change'));
                    closeDropdown();
                });

                itemsContainer.appendChild(itemDiv);
            });

            listContainer.appendChild(itemsContainer);
        });
        
        if (sortedCategories.length === 0) {
            const noResults = document.createElement('div');
            noResults.style.padding = '8px';
            noResults.style.color = 'var(--text-muted)';
            noResults.style.textAlign = 'center';
            noResults.innerText = 'No matches found';
            listContainer.appendChild(noResults);
        }
    };

    const openDropdown = () => {
        document.querySelectorAll('.searchable-dropdown.open').forEach(d => {
            if (d !== wrapper) d.classList.remove('open');
        });
        wrapper.classList.add('open');
        searchInput.value = '';
        renderList('');
        setTimeout(() => searchInput.focus(), 50);
    };

    const closeDropdown = () => {
        wrapper.classList.remove('open');
    };

    header.addEventListener('click', (e) => {
        e.stopPropagation();
        if (wrapper.classList.contains('open')) {
            closeDropdown();
        } else {
            openDropdown();
        }
    });

    searchInput.addEventListener('input', () => {
        renderList(searchInput.value);
    });

    searchInput.addEventListener('click', (e) => {
        e.stopPropagation();
    });

    popup.addEventListener('wheel', (e) => {
        e.stopPropagation();
    });

    const clickOutsideHandler = (e) => {
        if (!wrapper.contains(e.target)) {
            closeDropdown();
        }
    };
    document.addEventListener('click', clickOutsideHandler);

    // Intercept select value assignment
    const originalValueProp = Object.getOwnPropertyDescriptor(HTMLSelectElement.prototype, 'value');
    Object.defineProperty(select, 'value', {
        get() {
            return originalValueProp.get.call(this);
        },
        set(val) {
            originalValueProp.set.call(this, val);
            header.innerText = getActiveLabel(val);
        }
    });

    select.refreshCustomDropdown = (newItems) => {
        currentValueList = newItems;
        header.innerText = getActiveLabel(select.value);
    };
}

function populateSelectWithOptions(select, items) {
    select.innerHTML = "";
    
    const groups = {};
    items.forEach(item => {
        const cat = item.category || "General";
        if (!groups[cat]) {
            groups[cat] = [];
        }
        groups[cat].push(item);
    });

    const sortedCategories = Object.keys(groups).sort((a, b) => {
        if (a === "General") return -1;
        if (b === "General") return 1;
        return a.localeCompare(b);
    });

    sortedCategories.forEach(cat => {
        const optgroup = document.createElement('optgroup');
        optgroup.label = cat;
        
        const sortedItems = groups[cat].sort((a, b) => {
            let labelA = a.label;
            if (a.category && labelA.startsWith(a.category + ":")) {
                labelA = labelA.substring(a.category.length + 1).trim();
            }
            let labelB = b.label;
            if (b.category && labelB.startsWith(b.category + ":")) {
                labelB = labelB.substring(b.category.length + 1).trim();
            }
            return labelA.localeCompare(labelB);
        });

        sortedItems.forEach(item => {
            const opt = document.createElement('option');
            opt.value = item.type;
            let label = item.label;
            if (item.category && label.startsWith(item.category + ":")) {
                label = label.substring(item.category.length + 1).trim();
            }
            opt.innerText = label;
            optgroup.appendChild(opt);
        });
        select.appendChild(optgroup);
    });
}

const originalPopulate = populateSelectWithOptions;
populateSelectWithOptions = function(select, items) {
    originalPopulate(select, items);
    if (select.refreshCustomDropdown) {
        select.refreshCustomDropdown(items);
    }
};


// Custom Command Nodes
function addNewCommandNode(x = null, y = null) {
    if (x === null || y === null) {
        const center = getViewportCenterCoordinates();
        x = center.x - 160;
        y = center.y - 100;
    }
    const id = 'command_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
    const node = createBaseNode(id, 'command', '➡️ Execute Command', x, y);

    addPin(node, 'input', 'exec', 'In', `${id}_in`);
    addPin(node, 'output', 'exec', 'Out', `${id}_out`);

    const select = document.createElement('select');
    populateSelectWithOptions(select, AVAILABLE_COMMANDS);
    select.addEventListener('change', () => { 
        node.data.commandType = select.value; 
        if (select.value === 'media.playSound' || select.value === 'media.playVideo' || select.value === 'media.setBackgroundMusic') {
            node.data["Start Time"] = "0.00";
            node.data["StartTime"] = "0.00";
            node.data["End Time"] = "";
            node.data["EndTime"] = "";
        }
        refreshCommandFields(node); 
        triggerAutoSave();
    });
    node.bodyElement.appendChild(select);
    setupSearchableDropdown(select, AVAILABLE_COMMANDS);

    // Inline node Help button
    const helpBtn = document.createElement('span');
    helpBtn.className = 'node-help-link';
    helpBtn.textContent = '?';
    helpBtn.title = 'View documentation';
    helpBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        showNodeHelp(select.value);
    });
    const wrapper = select.nextSibling;
    if (wrapper) {
        wrapper.parentNode.insertBefore(helpBtn, wrapper.nextSibling);
    }

    const fieldContainer = document.createElement('div');
    fieldContainer.className = 'fields-container';
    fieldContainer.id = `${id}_fields`;
    node.bodyElement.appendChild(fieldContainer);

    if (AVAILABLE_COMMANDS.length > 0) {
        const defaultCmd = AVAILABLE_COMMANDS.find(c => c.type === 'general.displayText') || AVAILABLE_COMMANDS[0];
        node.data.commandType = defaultCmd.type;
        select.value = defaultCmd.type;
        refreshCommandFields(node);
    }

    autoLinkNodeIfPossible(node);
    return node;
}

// Custom Condition Nodes (Auto-generates clean unique IDs directly at creation, fixing child input resolution)
function addNewConditionNode(x = null, y = null) {
    if (x === null || y === null) {
        const center = getViewportCenterCoordinates();
        x = center.x - 160;
        y = center.y - 100;
    }
    const id = 'cond_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
    const node = createBaseNode(id, 'condition', '🔀 Branch Condition', x, y);

    addPin(node, 'input', 'exec', 'In', `${id}_in`);
    addPin(node, 'output', 'true', 'True', `${id}_true`);
    addPin(node, 'output', 'false', 'False', `${id}_false`);

    const select = document.createElement('select');
    populateSelectWithOptions(select, AVAILABLE_CONDITIONS);
    
    // Helper to dynamically rename port labels based on condition type (e.g. For Each Loop)
    const updateOutputLabels = (condType) => {
        const truePin = node.element.querySelector('.pin.output.true');
        const falsePin = node.element.querySelector('.pin.output.false');
        if (condType === 'variable.forEachLoop') {
            if (truePin && truePin.parentNode) truePin.parentNode.firstChild.textContent = 'Loop Body';
            if (falsePin && falsePin.parentNode) falsePin.parentNode.firstChild.textContent = 'Completed';
        } else {
            if (truePin && truePin.parentNode) truePin.parentNode.firstChild.textContent = 'True';
            if (falsePin && falsePin.parentNode) falsePin.parentNode.firstChild.textContent = 'False';
        }
    };

    select.addEventListener('change', () => { 
        node.data.conditionType = select.value; 
        updateOutputLabels(select.value);
        refreshCommandFields(node); 
        triggerAutoSave();
    });
    node.bodyElement.appendChild(select);
    setupSearchableDropdown(select, AVAILABLE_CONDITIONS);

    // Inline node Help button
    const helpBtn = document.createElement('span');
    helpBtn.className = 'node-help-link';
    helpBtn.textContent = '?';
    helpBtn.title = 'View documentation';
    helpBtn.addEventListener('click', (e) => {
        e.stopPropagation();
        showNodeHelp(select.value);
    });
    const wrapper = select.nextSibling;
    if (wrapper) {
        wrapper.parentNode.insertBefore(helpBtn, wrapper.nextSibling);
    }

    const fieldContainer = document.createElement('div');
    fieldContainer.className = 'fields-container';
    fieldContainer.id = `${id}_fields`;
    node.bodyElement.appendChild(fieldContainer);

    if (AVAILABLE_CONDITIONS.length > 0) {
        const defaultCond = AVAILABLE_CONDITIONS.find(c => c.type === 'variable.forEachLoop') || AVAILABLE_CONDITIONS[0];
        node.data.conditionType = defaultCond.type;
        select.value = defaultCond.type;
        updateOutputLabels(node.data.conditionType);
        refreshCommandFields(node);
    }

    autoLinkNodeIfPossible(node);
    return node;
}

function getAttributesForNode(node) {
    const type = node.type === 'command' ? node.data.commandType : node.data.conditionType;
    let attrs = [];
    
    if (type === 'char.setAttribute' || type === 'Dialogue: Damage / Heal' || type === 'Dialogue: Set State' || type === 'character.setAttribute' || type === 'SetCharacterAttributeCommandData' || type === 'char.attributeCheck' || type === 'CharacterAttributeCheckCondition') {
        const charId = getPropertyValue(node.data, "Character");
        if (charId && catalogs.Characters) {
            const char = catalogs.Characters.find(c => c.Id === charId);
            if (char && char.Attributes) attrs = char.Attributes;
        }
        if (attrs.length === 0 && catalogs.Characters) {
            const allAttrs = new Set();
            catalogs.Characters.forEach(c => {
                if (c.Attributes) {
                    c.Attributes.forEach(a => allAttrs.add(a));
                }
            });
            attrs = Array.from(allAttrs);
        }
    } else if (type === 'player.setAttribute' || type === 'SetPlayerAttributeCommandData' || type === 'player.attributeCheck' || type === 'PlayerAttributeCheckCondition') {
        if (catalogs.Player && catalogs.Player.Attributes) {
            attrs = catalogs.Player.Attributes;
        }
    } else if (type === 'timer.setAttribute' || type === 'SetTimerAttributeCommandData') {
        const timerId = getPropertyValue(node.data, "Timer");
        if (timerId && catalogs.Timers) {
            const timer = catalogs.Timers.find(t => t.Id === timerId || t.Name === timerId);
            if (timer && timer.Attributes) attrs = timer.Attributes;
        }
    } else if (type === 'item.setAttribute' || type === 'SetItemAttributeCommandData' || type === 'item.attributeCheck' || type === 'ItemAttributeCheckCondition') {
        const itemId = getPropertyValue(node.data, "Item") || getPropertyValue(node.data, "Object");
        if (itemId && catalogs.GameObjects) {
            const obj = catalogs.GameObjects.find(o => o.Id === itemId);
            if (obj && obj.Attributes) attrs = obj.Attributes;
        }
        if (attrs.length === 0 && catalogs.GameObjects) {
            const allAttrs = new Set();
            catalogs.GameObjects.forEach(o => {
                if (o.Attributes) {
                    o.Attributes.forEach(a => allAttrs.add(a));
                }
            });
            attrs = Array.from(allAttrs);
        }
    } else if (type === 'room.attributeCheck' || type === 'RoomAttributeCheckCondition' || type === 'room.setAttribute' || type === 'SetRoomAttributeCommandData') {
        const roomId = getPropertyValue(node.data, "Room");
        if (roomId && catalogs.Rooms) {
            const room = catalogs.Rooms.find(r => r.Id === roomId);
            if (room && room.Attributes) attrs = room.Attributes;
        }
        if (attrs.length === 0 && catalogs.Rooms) {
            const allAttrs = new Set();
            catalogs.Rooms.forEach(r => {
                if (r.Attributes) {
                    r.Attributes.forEach(a => allAttrs.add(a));
                }
            });
            attrs = Array.from(allAttrs);
        }
    }
    return attrs;
}

function refreshCommandFields(node) {
    const fieldsContainer = document.getElementById(`${node.id}_fields`);
    if (!fieldsContainer) return;
    fieldsContainer.innerHTML = "";
    if (node.element) {
        if (node.height) {
            node.element.style.height = `${node.height}px`;
        } else {
            node.element.style.height = 'auto';
        }
    }

    const type = node.type === 'command' ? node.data.commandType : node.data.conditionType;

    // Hide output pins for terminal nodes
    const outPin = node.element ? node.element.querySelector('.pin.output') : null;
    if (outPin) {
        if (type === 'general.endGame' || type === 'item.showInteractiveScreen') {
            outPin.style.display = 'none';
            // Break any connections originating from this pin
            connections = connections.filter(conn => conn.fromPinId !== outPin.id);
            redrawConnections();
        } else {
            outPin.style.display = 'flex';
        }
    }

    const schema = typeToInputsMap[type];
    node.inputs = [];

    if (!schema || !schema.inputs) {
        // Fallback standard text input if no schema found
        const row = document.createElement('div');
        row.className = 'field-row';
        row.style.marginBottom = '6px';
        
        const label = document.createElement('label');
        label.innerText = "Text Parameter:";
        row.appendChild(label);
        
        const inp = document.createElement('input');
        inp.placeholder = "Parameters / Details";
        inp.value = node.data.text || "";
        inp.addEventListener('input', () => {
            node.data.text = inp.value;
            triggerAutoSave();
        });
        row.appendChild(inp);
        fieldsContainer.appendChild(row);

        node.inputs.push({ label: 'text', element: inp });
        return;
    }

    schema.inputs.forEach(inputSchema => {
        let inputElement = null;
        const currentInputType = getPropertyValue(node.data, "Input Type") || getPropertyValue(node.data, "InputType") || "Text";
        if ((inputSchema.label === "Custom Options" || inputSchema.label === "CustomOptions") && currentInputType !== "Custom") {
            return;
        }

        const currentLoopSource = getPropertyValue(node.data, "Loop Source") || getPropertyValue(node.data, "LoopSource") || "Variable";
        if ((inputSchema.label === "Array Variable" || inputSchema.label === "ArrayVariable") && currentLoopSource !== "Variable") {
            return;
        }


        const row = document.createElement('div');
        row.className = 'field-row';
        row.style.marginBottom = '6px';
        row.style.display = 'flex';
        row.style.flexDirection = 'column';
        row.style.gap = '2px';

        const label = document.createElement('label');
        label.style.fontSize = '10px';
        label.style.color = 'var(--text-muted)';
        label.style.display = 'flex';
        label.style.justifyContent = 'space-between';
        label.style.alignItems = 'center';

        const labelTextSpan = document.createElement('span');
        labelTextSpan.innerText = inputSchema.label + ":";
        label.appendChild(labelTextSpan);

        const lowerFieldLabel = (inputSchema.label || '').toLowerCase();
        const schemaName = ((schema && schema.name) || node.title || node.name || '').toLowerCase();
        const isFormulaField = lowerFieldLabel === 'formula' || 
            (lowerFieldLabel === 'value' && (type.endsWith('.setAttribute') || type === 'var.set' || schemaName.includes('set attribute') || schemaName.includes('variable: set')));

        if (isFormulaField) {
            const formulaInfoBtn = document.createElement('span');
            formulaInfoBtn.className = 'formula-info-btn';
            formulaInfoBtn.innerHTML = '🧪 Formula';
            formulaInfoBtn.title = "Formulas & Math Supported!\n\nOperators: +, -, *, /, %, ^, ()\nFunctions: random(min,max), min(a,b), max(a,b), abs(x), round(x)\n\nExamples:\n• {player.attribute.Strength} + 1\n• min(100, {var.Health} + 25)\n• random(1, 20) + {player.attribute.Mind}\n\n(Click to open full Scripting Guide)";
            formulaInfoBtn.style.fontSize = '9px';
            formulaInfoBtn.style.color = '#a855f7';
            formulaInfoBtn.style.background = 'rgba(168, 85, 247, 0.15)';
            formulaInfoBtn.style.border = '1px solid rgba(168, 85, 247, 0.3)';
            formulaInfoBtn.style.borderRadius = '4px';
            formulaInfoBtn.style.padding = '1px 5px';
            formulaInfoBtn.style.cursor = 'pointer';
            formulaInfoBtn.style.marginLeft = 'auto';

            formulaInfoBtn.addEventListener('click', (e) => {
                e.stopPropagation();
                e.preventDefault();
                const sidebar = document.getElementById('help-sidebar');
                if (sidebar) sidebar.classList.remove('hide');
                switchHelpTab('syntax');
            });
            label.appendChild(formulaInfoBtn);
        }
        row.appendChild(label);

        let initialVal = getPropertyValue(node.data, inputSchema.label);
        if (inputSchema.label === 'Volume' && (initialVal === undefined || initialVal === null || initialVal === "")) {
            initialVal = "100";
            node.data[inputSchema.label] = "100";
            const aliases = propertyMappings[inputSchema.label] || [];
            aliases.forEach(alias => {
                node.data[alias] = "100";
            });
        }

        // Normalize numeric enums serialized as integers
        if (inputSchema.label === 'InputType' || inputSchema.label === 'Input Type') {
            if (initialVal === 0 || initialVal === '0') initialVal = "Text";
            else if (initialVal === 1 || initialVal === '1') initialVal = "Objects";
            else if (initialVal === 2 || initialVal === '2') initialVal = "Characters";
            else if (initialVal === 3 || initialVal === '3') initialVal = "Custom";
            
            node.data[inputSchema.label] = initialVal;
            const aliases = propertyMappings[inputSchema.label] || [];
            aliases.forEach(alias => {
                node.data[alias] = initialVal;
            });
        } else if (inputSchema.label === 'Gender') {
            if (initialVal === 0 || initialVal === '0') initialVal = "Male";
            else if (initialVal === 1 || initialVal === '1') initialVal = "Female";
            else if (initialVal === 2 || initialVal === '2') initialVal = "Non-binary";
            else if (initialVal === 3 || initialVal === '3') initialVal = "Other";
            
            node.data[inputSchema.label] = initialVal;
            const aliases = propertyMappings[inputSchema.label] || [];
            aliases.forEach(alias => {
                node.data[alias] = initialVal;
            });
        }

        if (inputSchema.label === 'Patrol Path' || inputSchema.label === 'PatrolPath') {
            const listWrapper = document.createElement('div');
            listWrapper.className = 'patrol-path-wrapper';
            listWrapper.style.display = 'flex';
            listWrapper.style.flexDirection = 'column';
            listWrapper.style.gap = '6px';
            listWrapper.style.marginTop = '4px';

            const stepsContainer = document.createElement('div');
            stepsContainer.className = 'patrol-steps-container';
            stepsContainer.style.display = 'flex';
            stepsContainer.style.flexDirection = 'column';
            stepsContainer.style.gap = '6px';
            listWrapper.appendChild(stepsContainer);

            const updatePatrolUI = () => {
                stepsContainer.innerHTML = '';
                const charId = node.data["Character"] || node.data["CharacterId"] || "";
                if (!charId) {
                    const errorMsg = document.createElement('div');
                    errorMsg.innerText = "⚠️ Please select a Character first.";
                    errorMsg.style.color = '#f38ba8';
                    errorMsg.style.fontSize = '12px';
                    stepsContainer.appendChild(errorMsg);
                    return;
                }

                const character = (catalogs.Characters || []).find(c => c.Id === charId);
                const startingRoomId = character ? character.StartingRoomId : "";

                let currentVal = getPropertyValue(node.data, "Patrol Path") || getPropertyValue(node.data, "PatrolPath") || "";
                let steps = currentVal ? currentVal.split(',').map(s => s.trim()).filter(s => s.length > 0) : [];

                if (startingRoomId) {
                    if (steps.length === 0 || steps[0] !== startingRoomId) {
                        steps = [startingRoomId, ...steps.slice(1)];
                        const joined = steps.join(',');
                        node.data["Patrol Path"] = joined;
                        node.data["PatrolPath"] = joined;
                        triggerAutoSave();
                    }
                }

                steps.forEach((stepRoomId, idx) => {
                    const row = document.createElement('div');
                    row.style.display = 'flex';
                    row.style.alignItems = 'center';
                    row.style.gap = '6px';

                    const indexLabel = document.createElement('span');
                    indexLabel.innerText = idx === 0 ? "Start:" : `Step ${idx}:`;
                    indexLabel.style.fontSize = '11px';
                    indexLabel.style.color = '#a6adc8';
                    indexLabel.style.width = '45px';
                    row.appendChild(indexLabel);

                    const select = document.createElement('select');
                    select.style.flex = '1';
                    select.style.fontSize = '12px';

                    if (idx === 0) {
                        (catalogs.Rooms || []).forEach(r => {
                            const opt = document.createElement('option');
                            opt.value = r.Id;
                            opt.innerText = r.Name;
                            if (r.Id === stepRoomId) opt.selected = true;
                            select.appendChild(opt);
                        });

                        select.addEventListener('change', () => {
                            const newRoomId = select.value;
                            if (charId && newRoomId) {
                                window.location.href = `rags-action://update-char-starting-room?charId=${charId}&roomId=${newRoomId}`;
                            }
                            steps[0] = newRoomId;
                            steps = [newRoomId];
                            const joined = steps.join(',');
                            node.data["Patrol Path"] = joined;
                            node.data["PatrolPath"] = joined;
                            updatePatrolUI();
                            triggerAutoSave();
                        });
                    } else {
                        const prevRoomId = steps[idx - 1];
                        const prevRoom = (catalogs.Rooms || []).find(r => r.Id === prevRoomId);
                        
                        const validDestRoomIds = [];
                        if (prevRoom && prevRoom.Exits) {
                            Object.keys(prevRoom.Exits).forEach(dir => {
                                validDestRoomIds.push(prevRoom.Exits[dir]);
                            });
                        }

                        (catalogs.Rooms || []).forEach(r => {
                            if (validDestRoomIds.includes(r.Id)) {
                                const opt = document.createElement('option');
                                opt.value = r.Id;
                                opt.innerText = r.Name;
                                if (r.Id === stepRoomId) opt.selected = true;
                                select.appendChild(opt);
                            }
                        });

                        if (!validDestRoomIds.includes(stepRoomId)) {
                            const opt = document.createElement('option');
                            opt.value = stepRoomId;
                            const rMatch = (catalogs.Rooms || []).find(r => r.Id === stepRoomId);
                            opt.innerText = (rMatch ? rMatch.Name : "Unknown Room") + " (Disconnected)";
                            opt.selected = true;
                            select.appendChild(opt);
                        }

                        select.addEventListener('change', () => {
                            steps[idx] = select.value;
                            steps = steps.slice(0, idx + 1);
                            const joined = steps.join(',');
                            node.data["Patrol Path"] = joined;
                            node.data["PatrolPath"] = joined;
                            updatePatrolUI();
                            triggerAutoSave();
                        });
                    }

                    row.appendChild(select);

                    if (idx > 0) {
                        const delBtn = document.createElement('button');
                        delBtn.innerHTML = '🗑️';
                        delBtn.style.background = 'none';
                        delBtn.style.border = 'none';
                        delBtn.style.cursor = 'pointer';
                        delBtn.style.padding = '0 4px';
                        delBtn.addEventListener('click', (e) => {
                            e.preventDefault();
                            steps = steps.slice(0, idx);
                            const joined = steps.join(',');
                            node.data["Patrol Path"] = joined;
                            node.data["PatrolPath"] = joined;
                            updatePatrolUI();
                            triggerAutoSave();
                        });
                        row.appendChild(delBtn);
                    }

                    stepsContainer.appendChild(row);
                });

                const lastRoomId = steps[steps.length - 1];
                const lastRoom = (catalogs.Rooms || []).find(r => r.Id === lastRoomId);
                const hasExits = lastRoom && lastRoom.Exits && Object.keys(lastRoom.Exits).length > 0;

                if (hasExits) {
                    const addBtn = document.createElement('button');
                    addBtn.innerText = '➕ Add Patrol Step';
                    addBtn.style.padding = '6px';
                    addBtn.style.background = '#313244';
                    addBtn.style.color = '#cdd6f4';
                    addBtn.style.border = '1px solid #45475a';
                    addBtn.style.borderRadius = '6px';
                    addBtn.style.cursor = 'pointer';
                    addBtn.style.fontSize = '12px';
                    addBtn.style.marginTop = '4px';

                    addBtn.addEventListener('click', (e) => {
                        e.preventDefault();
                        const firstExitKey = Object.keys(lastRoom.Exits)[0];
                        const destRoomId = lastRoom.Exits[firstExitKey];
                        steps.push(destRoomId);
                        const joined = steps.join(',');
                        node.data["Patrol Path"] = joined;
                        node.data["PatrolPath"] = joined;
                        updatePatrolUI();
                        triggerAutoSave();
                    });
                    stepsContainer.appendChild(addBtn);
                }
            };

            updatePatrolUI();
            row.appendChild(listWrapper);
            inputElement = listWrapper;
        } else if (inputSchema.label === 'Custom Options' || inputSchema.label === 'CustomOptions') {
            // Render interactive option list builder!
            const listWrapper = document.createElement('div');
            listWrapper.className = 'custom-options-wrapper';
            listWrapper.style.display = 'flex';
            listWrapper.style.flexDirection = 'column';
            listWrapper.style.gap = '6px';
            listWrapper.style.marginTop = '4px';

            const optionsListContainer = document.createElement('div');
            optionsListContainer.className = 'options-list-container';
            optionsListContainer.style.display = 'flex';
            optionsListContainer.style.flexDirection = 'column';
            optionsListContainer.style.gap = '4px';

            const updateOptionsUI = () => {
                optionsListContainer.innerHTML = '';
                const currentVal = getPropertyValue(node.data, "Custom Options") || "";
                const options = currentVal ? currentVal.split(',').map(s => s.trim()).filter(s => s.length > 0) : [];

                options.forEach((opt, idx) => {
                    const itemRow = document.createElement('div');
                    itemRow.style.display = 'flex';
                    itemRow.style.justifyContent = 'space-between';
                    itemRow.style.alignItems = 'center';
                    itemRow.style.padding = '6px 8px';
                    itemRow.style.background = '#1e1e2e'; // dark slate bento style
                    itemRow.style.border = '1px solid #313244';
                    itemRow.style.borderRadius = '6px';
                    itemRow.style.fontSize = '12px';

                    const textSpan = document.createElement('span');
                    textSpan.innerText = opt;
                    textSpan.style.color = '#cdd6f4';
                    itemRow.appendChild(textSpan);

                    const deleteBtn = document.createElement('button');
                    deleteBtn.innerHTML = '🗑️';
                    deleteBtn.style.background = 'none';
                    deleteBtn.style.border = 'none';
                    deleteBtn.style.cursor = 'pointer';
                    deleteBtn.style.fontSize = '12px';
                    deleteBtn.style.padding = '0';
                    deleteBtn.style.color = '#f38ba8';
                    deleteBtn.addEventListener('click', (e) => {
                        e.preventDefault();
                        const newOptions = options.filter((_, i) => i !== idx);
                        const joined = newOptions.join(',');
                        node.data["Custom Options"] = joined;
                        node.data["CustomOptions"] = joined;
                        updateOptionsUI();
                        triggerAutoSave();
                    });
                    itemRow.appendChild(deleteBtn);
                    optionsListContainer.appendChild(itemRow);
                });
            };

            updateOptionsUI();
            listWrapper.appendChild(optionsListContainer);

            // Add new option control group
            const addGroup = document.createElement('div');
            addGroup.style.display = 'flex';
            addGroup.style.gap = '6px';
            addGroup.style.marginTop = '4px';

            const addInput = document.createElement('input');
            addInput.type = 'text';
            addInput.placeholder = 'another option to add';
            addInput.style.flex = '1';
            addInput.style.fontSize = '12px';
            addInput.style.padding = '6px 8px';
            addGroup.appendChild(addInput);

            const addBtn = document.createElement('button');
            addBtn.innerText = 'Add';
            addBtn.style.padding = '6px 12px';
            addBtn.style.background = '#89b4fa';
            addBtn.style.color = '#11111b';
            addBtn.style.border = 'none';
            addBtn.style.borderRadius = '6px';
            addBtn.style.cursor = 'pointer';
            addBtn.style.fontWeight = 'bold';
            addBtn.style.fontSize = '12px';

            const handleAdd = (e) => {
                if (e) e.preventDefault();
                const newOpt = addInput.value.trim();
                if (newOpt) {
                    const currentVal = getPropertyValue(node.data, "Custom Options") || "";
                    const options = currentVal ? currentVal.split(',').map(s => s.trim()).filter(s => s.length > 0) : [];
                    options.push(newOpt);
                    const joined = options.join(',');
                    node.data["Custom Options"] = joined;
                    node.data["CustomOptions"] = joined;
                    addInput.value = '';
                    updateOptionsUI();
                    triggerAutoSave();
                }
            };

            addBtn.addEventListener('click', handleAdd);
            addInput.addEventListener('keydown', (e) => {
                if (e.key === 'Enter') {
                    e.preventDefault();
                    handleAdd();
                }
            });

            addGroup.appendChild(addBtn);
            listWrapper.appendChild(addGroup);

            inputElement = listWrapper;
        } else if (inputSchema.controlType === 'ComboBox' || inputSchema.label === 'Attribute Name' || inputSchema.label === 'AttributeName' || inputSchema.dataType === 'Room' || inputSchema.dataType === 'GameObject' || inputSchema.dataType === 'Character' || inputSchema.dataType === 'Variable' || inputSchema.dataType === 'Media' || inputSchema.dataType === 'Function' || inputSchema.dataType === 'Timer' || inputSchema.dataType === 'Item' || inputSchema.dataType === 'PromptName' || inputSchema.dataType === 'ActionName' || inputSchema.dataType === 'StatusBarElement') {
            // Container for both controls
            const fieldWrapper = document.createElement('div');
            fieldWrapper.className = 'toggle-field-wrapper';
            fieldWrapper.style.display = 'flex';
            fieldWrapper.style.flexDirection = 'column';
            fieldWrapper.style.gap = '4px';

            const pickerSelect = document.createElement('select');
            pickerSelect.style.width = "100%";
            
            const blankOpt = document.createElement('option');
            blankOpt.value = "";
            blankOpt.innerText = "-- Select --";
            pickerSelect.appendChild(blankOpt);

            let optionsList = [];
            if (inputSchema.dataType === 'Room') optionsList = catalogs.Rooms || [];
            else if (inputSchema.dataType === 'Hotspot') optionsList = catalogs.Hotspots || [];
            else if (inputSchema.dataType === 'GameObject' || inputSchema.dataType === 'Item') {
                optionsList = catalogs.GameObjects || [];
                if (inputSchema.label === 'Container Object' || inputSchema.label === 'ContainerObject') {
                    optionsList = optionsList.filter(o => {
                        const isCont = o.IsContainer || o.isContainer;
                        const idVal = o.Id !== undefined ? o.Id : o.id;
                        return isCont || String(idVal) === String(initialVal);
                    });
                }
            }
            else if (inputSchema.dataType === 'Character') optionsList = catalogs.Characters || [];
            else if (inputSchema.dataType === 'Variable') {
                optionsList = catalogs.Variables || [];
                if (inputSchema.label === 'Array Variable' || inputSchema.label === 'ArrayVariable' || inputSchema.label === 'Array Variable Name' || inputSchema.label === 'ArrayVariableName') {
                    optionsList = optionsList.filter(v => {
                        const t = (v.VarType || v.varType || v.Type || v.type || "").toLowerCase();
                        return t === 'array';
                    });
                }
            }
            else if (inputSchema.dataType === 'Media') {
                optionsList = catalogs.Media || [];
                const isSoundCommand = (type === 'media.playSound' || type === 'media.setBackgroundMusic' || type === 'media.stopSound');
                const isVideoCommand = (type === 'media.playVideo');
                if (isSoundCommand) {
                    optionsList = optionsList.filter(m => {
                        const name = (m.Name || m.name || "").toLowerCase();
                        return name.endsWith('.mp3') || name.endsWith('.wav') || name.endsWith('.ogg') || name.endsWith('.m4a') || name.endsWith('.aac') || name.endsWith('.flac');
                    });
                } else if (isVideoCommand) {
                    optionsList = optionsList.filter(m => {
                        const name = (m.Name || m.name || "").toLowerCase();
                        return name.endsWith('.mp4') || name.endsWith('.mov') || name.endsWith('.avi') || name.endsWith('.mkv') || name.endsWith('.webm');
                    });
                }
            }
            else if (inputSchema.dataType === 'Function') optionsList = catalogs.Functions || [];
            else if (inputSchema.dataType === 'Timer') optionsList = catalogs.Timers || [];
            else if (inputSchema.dataType === 'StatusBarElement') optionsList = catalogs.StatusBarElements || [];
            else if (inputSchema.dataType === 'SplashScreen') optionsList = catalogs.SplashScreens || [];
            else if (inputSchema.dataType === 'PromptName') {
                optionsList = [];
                if (catalogs.PromptNames) {
                    catalogs.PromptNames.forEach(pName => {
                        optionsList.push({ Id: pName, Name: pName });
                    });
                }
                nodes.forEach(n => {
                    if (n.type === 'command' && n.data && n.data.commandType === 'general.promptInput') {
                        const pName = getPropertyValue(n.data, "Prompt Name") || n.data.PromptName;
                        if (pName) {
                            optionsList.push({ Id: pName, Name: pName });
                        }
                    }
                });
                const seen = new Set();
                optionsList = optionsList.filter(opt => {
                    if (!opt.Id || seen.has(opt.Id)) return false;
                    seen.add(opt.Id);
                    return true;
                });
            }
            else if (inputSchema.label === 'Attribute Name' || inputSchema.label === 'AttributeName') {
                const attrs = getAttributesForNode(node);
                optionsList = attrs.map(a => ({ Id: a, Name: a }));
            }
            // Bug #5: ActionName is a dynamic list scoped to the entity selected in this node.
            else if (inputSchema.dataType === 'ActionName') {
                // Determine which entity is selected and pull its actions from the catalog.
                const commandType = node.data.commandType || '';
                let entityActions = [];

                if (commandType === 'char.setActionActive') {
                    const charId = getPropertyValue(node.data, 'Character');
                    const ch = (catalogs.Characters || []).find(c => (c.Id || c.id) === charId);
                    entityActions = ch ? (ch.Actions || ch.actions || []) : [];
                } else if (commandType === 'item.setActionActive') {
                    const itemId = getPropertyValue(node.data, 'Item');
                    const obj = (catalogs.GameObjects || []).find(o => (o.Id || o.id) === itemId);
                    entityActions = obj ? (obj.Actions || obj.actions || []) : [];
                } else if (commandType === 'room.setActionActive') {
                    const roomId = getPropertyValue(node.data, 'Room');
                    const rm = (catalogs.Rooms || []).find(r => (r.Id || r.id) === roomId);
                    entityActions = rm ? (rm.Actions || rm.actions || []) : [];
                } else if (commandType === 'player.setActionActive') {
                    entityActions = catalogs.PlayerActions || [];
                }

                optionsList = entityActions.map(a => {
                    const name = a.Name || a.name || a;
                    return { Id: name, Name: name };
                });
            }
            else if (inputSchema.label === 'Gender') {
                optionsList = [
                    { Id: "Male", Name: "Male" },
                    { Id: "Female", Name: "Female" },
                    { Id: "Non-binary", Name: "Non-binary" },
                    { Id: "Other", Name: "Other" }
                ];
            } else if (inputSchema.label === 'Transition Intensity' || inputSchema.label === 'TransitionIntensity' || inputSchema.label === 'Intensity') {
                optionsList = [
                    { Id: "0.1", Name: "Very Low (0.1)" },
                    { Id: "0.3", Name: "Low (0.3)" },
                    { Id: "0.5", Name: "Medium (0.5)" },
                    { Id: "0.8", Name: "High (0.8)" },
                    { Id: "1.0", Name: "Extreme (1.0)" }
                ];
            } else if (inputSchema.label === 'Transition Style' || inputSchema.label === 'TransitionStyle') {
                optionsList = [
                    { Id: "None", Name: "None" },
                    { Id: "Smoke", Name: "Smoke" },
                    { Id: "Sand", Name: "Sand" },
                    { Id: "Embers", Name: "Embers" },
                    { Id: "Rain", Name: "Rain" },
                    { Id: "Snow", Name: "Snow" }
                ];
            } else if (inputSchema.label === 'InputType' || inputSchema.label === 'Input Type') {
                optionsList = [
                    { Id: "Text", Name: "Text" },
                    { Id: "Objects", Name: "Objects" },
                    { Id: "Characters", Name: "Characters" },
                    { Id: "Custom", Name: "Custom" }
                ];
            } else if (inputSchema.label === 'Comparison') {
                optionsList = [
                    { Id: "=", Name: "=" },
                    { Id: "!=", Name: "!=" },
                    { Id: ">", Name: ">" },
                    { Id: ">=", Name: ">=" },
                    { Id: "<", Name: "<" },
                    { Id: "<=", Name: "<=" }
                ];
            } else if (inputSchema.label === 'DateTime Component' || inputSchema.label === 'DateTimeComponent') {
                optionsList = [
                    { Id: "second", Name: "Second" },
                    { Id: "minute", Name: "Minute" },
                    { Id: "hour", Name: "Hour" },
                    { Id: "day", Name: "Day" },
                    { Id: "month", Name: "Month" },
                    { Id: "year", Name: "Year" }
                ];
            } else if (inputSchema.label === 'Loop Source' || inputSchema.label === 'LoopSource') {
                optionsList = [
                    { Id: "Variable", Name: "Variable" },
                    { Id: "Items", Name: "Items" },
                    { Id: "Characters", Name: "Characters" },
                    { Id: "Rooms", Name: "Rooms" }
                ];
            } else if (inputSchema.label === 'Filter Type' || inputSchema.label === 'FilterType') {
                const currentLoopSource = getPropertyValue(node.data, "Loop Source") || getPropertyValue(node.data, "LoopSource") || "Variable";
                if (currentLoopSource === "Characters") {
                    optionsList = [
                        { Id: "All", Name: "All" },
                        { Id: "In Current Room", Name: "In Current Room" }
                    ];
                } else {
                    optionsList = [
                        { Id: "All", Name: "All" },
                        { Id: "Inventory", Name: "Inventory" },
                        { Id: "Worn", Name: "Worn" },
                        { Id: "In Current Room", Name: "In Current Room" }
                    ];
                }
            } else if (inputSchema.label === 'Direction') {
                optionsList = [
                    { Id: "North", Name: "North" },
                    { Id: "South", Name: "South" },
                    { Id: "East", Name: "East" },
                    { Id: "West", Name: "West" },
                    { Id: "Up", Name: "Up" },
                    { Id: "Down", Name: "Down" },
                    { Id: "In", Name: "In" },
                    { Id: "Out", Name: "Out" }
                ];
            } else if (inputSchema.label === 'Map Style' || inputSchema.label === 'MapStyle') {
                optionsList = [
                    { Id: "Clean", Name: "Clean" },
                    { Id: "SciFi", Name: "SciFi" },
                    { Id: "Fantasy", Name: "Fantasy" }
                ];
            } else if (inputSchema.label === 'Column Name' || inputSchema.label === 'ColumnName') {
                optionsList = [];
                const varName = getPropertyValue(node.data, "Array Variable") || getPropertyValue(node.data, "ArrayVariable");
                const variable = (catalogs.Variables || []).find(v => (v.Name || v.name || "").toLowerCase() === (varName || "").toLowerCase());
                const columns = variable ? (variable.Columns || variable.columns) : null;
                if (columns) {
                    optionsList = columns.map(c => ({ Id: c, Name: c }));
                }
            } else if (inputSchema.label === 'Row Index' || inputSchema.label === 'RowIndex') {
                optionsList = [];
                const varName = getPropertyValue(node.data, "Array Variable") || getPropertyValue(node.data, "ArrayVariable");
                const variable = (catalogs.Variables || []).find(v => (v.Name || v.name || "").toLowerCase() === (varName || "").toLowerCase());
                const rowCount = variable ? (variable.RowCount !== undefined ? variable.RowCount : (variable.rowCount || 0)) : 0;
                
                // Show indices up to the current row count, but at least 0-4 as fallback
                const limit = Math.max(5, rowCount);
                for (let i = 0; i < limit; i++) {
                    optionsList.push({ Id: i.toString(), Name: i.toString() });
                }
            }

            // Add "+ Add New..." option if elements catalog type or attribute name is supported
            const supportQuickAdd = ['Room', 'Character', 'GameObject', 'Item', 'Variable', 'Timer', 'Function'].includes(inputSchema.dataType);
            const isAttributeField = (inputSchema.label === 'Attribute Name' || inputSchema.label === 'AttributeName');
            if (supportQuickAdd || isAttributeField) {
                const addOpt = document.createElement('option');
                addOpt.value = isAttributeField ? "_add_new_attribute_" : "_add_new_";
                addOpt.innerText = "+ Add New...";
                addOpt.style.color = "#a855f7";
                addOpt.style.fontWeight = "bold";
                pickerSelect.appendChild(addOpt);
            }

            optionsList.forEach(opt => {
                const o = document.createElement('option');
                const nameVal = opt.Name !== undefined ? opt.Name : opt.name;
                const idVal = opt.Id !== undefined ? opt.Id : opt.id;
                if (inputSchema.dataType === 'Variable' || inputSchema.dataType === 'PromptName' || inputSchema.label === 'Attribute Name' || inputSchema.label === 'AttributeName' || inputSchema.label === 'Column Name' || inputSchema.label === 'ColumnName' || inputSchema.label === 'Row Index' || inputSchema.label === 'RowIndex') {
                    o.value = nameVal;
                    o.innerText = nameVal;
                } else {
                    o.value = idVal;
                    o.innerText = nameVal;
                }
                pickerSelect.appendChild(o);
            });

            const textInput = document.createElement('input');
            textInput.type = 'text';
            textInput.placeholder = `Enter expression / {this.name}...`;
            textInput.style.width = "100%";

            const existsInOptions = optionsList.some(opt => {
                const nameVal = opt.Name !== undefined ? opt.Name : opt.name;
                const idVal = opt.Id !== undefined ? opt.Id : opt.id;
                const target = (inputSchema.dataType === 'Variable' || inputSchema.dataType === 'PromptName' || inputSchema.label === 'Attribute Name' || inputSchema.label === 'AttributeName') ? nameVal : idVal;
                return String(target) === String(initialVal);
            });
            let isExprMode = (initialVal && typeof initialVal === 'string' && (initialVal.includes('{') || initialVal.includes('}'))) || (initialVal !== undefined && initialVal !== null && initialVal !== "" && !existsInOptions);

            pickerSelect.style.display = isExprMode ? 'none' : 'block';
            textInput.style.display = isExprMode ? 'block' : 'none';

            label.style.display = 'flex';
            label.style.justifyContent = 'space-between';
            label.style.alignItems = 'center';

            const toggleLink = document.createElement('span');
            toggleLink.className = 'field-toggle-mode';
            toggleLink.style.fontSize = '9px';
            toggleLink.style.cursor = 'pointer';
            toggleLink.style.textDecoration = 'underline';
            toggleLink.style.color = '#a855f7';
            toggleLink.style.marginLeft = 'auto';
            toggleLink.innerText = isExprMode ? "👁️ Dropdown" : "📝 Text Mode";

            toggleLink.addEventListener('click', (e) => {
                e.preventDefault();
                isExprMode = !isExprMode;
                if (isExprMode) {
                    textInput.style.display = 'block';
                    pickerSelect.style.display = 'none';
                    toggleLink.innerText = "👁️ Dropdown";
                    textInput.value = pickerSelect.value === "_add_new_" ? "" : pickerSelect.value;
                } else {
                    textInput.style.display = 'none';
                    pickerSelect.style.display = 'block';
                    toggleLink.innerText = "📝 Text Mode";
                    pickerSelect.value = textInput.value;
                }
            });
            label.appendChild(toggleLink);

            pickerSelect.value = existsInOptions ? initialVal : "";
            textInput.value = initialVal || "";

            pickerSelect.addEventListener('change', () => {
                if (pickerSelect.value === "_add_new_") {
                    let dt = inputSchema.dataType;
                    if (dt === "Item") dt = "GameObject"; // align with backend types
                    openAddElementModal(dt, node, pickerSelect, inputSchema);
                    return;
                }
                if (pickerSelect.value === "_add_new_attribute_") {
                    openAddAttributeModal(node, pickerSelect, inputSchema);
                    return;
                }
                textInput.value = pickerSelect.value;
                node.data[inputSchema.label] = pickerSelect.value;
                const aliases = propertyMappings[inputSchema.label] || [];
                aliases.forEach(alias => {
                    node.data[alias] = pickerSelect.value;
                });

                if (inputSchema.dataType === 'Media') {
                    node.data["Start Time"] = "0.00";
                    node.data["StartTime"] = "0.00";
                    node.data["End Time"] = "";
                    node.data["EndTime"] = "";
                }
                if (inputSchema.label === 'Map Style' || inputSchema.label === 'MapStyle' || inputSchema.label === 'Input Type' || inputSchema.label === 'InputType' || inputSchema.label === 'Loop Source' || inputSchema.label === 'LoopSource' || inputSchema.label === 'Filter Type' || inputSchema.label === 'FilterType' || inputSchema.dataType === 'Room' || inputSchema.dataType === 'GameObject' || inputSchema.dataType === 'Character' || inputSchema.dataType === 'Item' || inputSchema.dataType === 'Timer' || inputSchema.dataType === 'Variable' || inputSchema.dataType === 'Media' || inputSchema.dataType === 'ActionName') {
                    refreshCommandFields(node);
                }
                triggerAutoSave();
            });

            textInput.addEventListener('input', () => {
                pickerSelect.value = textInput.value;
                node.data[inputSchema.label] = textInput.value;
                const aliases = propertyMappings[inputSchema.label] || [];
                aliases.forEach(alias => {
                    node.data[alias] = textInput.value;
                });
                if (inputSchema.dataType === 'Media') {
                    node.data["Start Time"] = "0.00";
                    node.data["StartTime"] = "0.00";
                    node.data["End Time"] = "";
                    node.data["EndTime"] = "";
                }
                triggerAutoSave();
            });

            fieldWrapper.appendChild(pickerSelect);
            fieldWrapper.appendChild(textInput);
            inputElement = fieldWrapper;
        } else if (inputSchema.controlType === 'RichText' || inputSchema.controlType === 'TextArea' || inputSchema.label.toLowerCase().includes('text') || inputSchema.label.toLowerCase().includes('lines') || inputSchema.label.toLowerCase().includes('description') || inputSchema.label.toLowerCase().includes('dialogue')) {
            // Multi-line rich text editor with Live Preview and AI dialogue bridge!
            inputElement = document.createElement('textarea');
            inputElement.placeholder = `Enter ${inputSchema.label}...`;
            inputElement.value = initialVal;
            inputElement.style.width = "100%";
            inputElement.addEventListener('input', () => {
                node.data[inputSchema.label] = inputElement.value;
                const aliases = propertyMappings[inputSchema.label] || [];
                aliases.forEach(alias => {
                    node.data[alias] = inputElement.value;
                });
                triggerAutoSave();
            });

            row.classList.add('textarea-row');
            row.appendChild(createFormattingToolbar(inputElement, null, inputSchema.label, node));
            row.appendChild(inputElement);
        } else if (inputSchema.label === 'Value' || inputSchema.label === 'Expected Value' || inputSchema.label === 'ExpectedValue') {
            const varType = getSelectedVariableType(node);
            if (varType === 'boolean' || varType === 'bool' || varType === 'true / false') {
                const fieldWrapper = document.createElement('div');
                fieldWrapper.className = 'toggle-field-wrapper';
                fieldWrapper.style.display = 'flex';
                fieldWrapper.style.flexDirection = 'column';
                fieldWrapper.style.gap = '4px';

                const pickerSelect = document.createElement('select');
                pickerSelect.style.width = "100%";
                
                const optTrue = document.createElement('option');
                optTrue.value = "true";
                optTrue.innerText = "true";
                pickerSelect.appendChild(optTrue);

                const optFalse = document.createElement('option');
                optFalse.value = "false";
                optFalse.innerText = "false";
                pickerSelect.appendChild(optFalse);

                const textInput = document.createElement('input');
                textInput.type = 'text';
                textInput.placeholder = `Enter expression / {this.name}...`;
                textInput.style.width = "100%";

                const cleanInitial = (initialVal || "").toLowerCase().trim();
                const existsInOptions = cleanInitial === 'true' || cleanInitial === 'false';
                let isExprMode = (initialVal && !existsInOptions);

                pickerSelect.style.display = isExprMode ? 'none' : 'block';
                textInput.style.display = isExprMode ? 'block' : 'none';

                label.style.display = 'flex';
                label.style.justifyContent = 'space-between';
                label.style.alignItems = 'center';

                const toggleLink = document.createElement('span');
                toggleLink.className = 'field-toggle-mode';
                toggleLink.style.fontSize = '9px';
                toggleLink.style.cursor = 'pointer';
                toggleLink.style.textDecoration = 'underline';
                toggleLink.style.color = '#a855f7';
                toggleLink.style.marginLeft = 'auto';
                toggleLink.innerText = isExprMode ? "👁️ Boolean Dropdown" : "📝 Text Mode";

                toggleLink.addEventListener('click', (e) => {
                    e.preventDefault();
                    isExprMode = !isExprMode;
                    if (isExprMode) {
                        textInput.style.display = 'block';
                        pickerSelect.style.display = 'none';
                        toggleLink.innerText = "👁️ Boolean Dropdown";
                        textInput.value = pickerSelect.value;
                    } else {
                        textInput.style.display = 'none';
                        pickerSelect.style.display = 'block';
                        toggleLink.innerText = "📝 Text Mode";
                        pickerSelect.value = textInput.value;
                    }
                });
                label.appendChild(toggleLink);

                pickerSelect.value = existsInOptions ? cleanInitial : "false";
                textInput.value = initialVal || "";

                pickerSelect.addEventListener('change', () => {
                    textInput.value = pickerSelect.value;
                    node.data[inputSchema.label] = pickerSelect.value;
                    const aliases = propertyMappings[inputSchema.label] || [];
                    aliases.forEach(alias => {
                        node.data[alias] = pickerSelect.value;
                    });
                    triggerAutoSave();
                });

                textInput.addEventListener('input', () => {
                    pickerSelect.value = textInput.value;
                    node.data[inputSchema.label] = textInput.value;
                    const aliases = propertyMappings[inputSchema.label] || [];
                    aliases.forEach(alias => {
                        node.data[alias] = textInput.value;
                    });
                    triggerAutoSave();
                });

                fieldWrapper.appendChild(pickerSelect);
                fieldWrapper.appendChild(textInput);
                inputElement = fieldWrapper;
            } else if (varType === 'number') {
                const fieldWrapper = document.createElement('div');
                fieldWrapper.className = 'toggle-field-wrapper';
                fieldWrapper.style.display = 'flex';
                fieldWrapper.style.flexDirection = 'column';
                fieldWrapper.style.gap = '4px';

                const numberInput = document.createElement('input');
                numberInput.type = 'number';
                numberInput.style.width = "100%";
                if (inputSchema.label && inputSchema.label.toLowerCase().includes('duration')) {
                    numberInput.placeholder = "Enter duration in seconds...";
                } else {
                    numberInput.placeholder = "Enter number...";
                }

                const textInput = document.createElement('input');
                textInput.type = 'text';
                textInput.placeholder = `Enter expression / {this.name}...`;
                textInput.style.width = "100%";

                let isExprMode = (initialVal && isNaN(initialVal));

                numberInput.style.display = isExprMode ? 'none' : 'block';
                textInput.style.display = isExprMode ? 'block' : 'none';

                label.style.display = 'flex';
                label.style.justifyContent = 'space-between';
                label.style.alignItems = 'center';

                const toggleLink = document.createElement('span');
                toggleLink.className = 'field-toggle-mode';
                toggleLink.style.fontSize = '9px';
                toggleLink.style.cursor = 'pointer';
                toggleLink.style.textDecoration = 'underline';
                toggleLink.style.color = '#a855f7';
                toggleLink.style.marginLeft = 'auto';
                toggleLink.innerText = isExprMode ? "👁️ Number Input" : "📝 Text Mode";

                toggleLink.addEventListener('click', (e) => {
                    e.preventDefault();
                    isExprMode = !isExprMode;
                    if (isExprMode) {
                        textInput.style.display = 'block';
                        numberInput.style.display = 'none';
                        toggleLink.innerText = "👁️ Number Input";
                        textInput.value = numberInput.value;
                    } else {
                        textInput.style.display = 'none';
                        numberInput.style.display = 'block';
                        toggleLink.innerText = "📝 Text Mode";
                        numberInput.value = textInput.value;
                    }
                });
                label.appendChild(toggleLink);

                numberInput.value = isExprMode ? "" : initialVal;
                textInput.value = initialVal || "";

                numberInput.addEventListener('input', () => {
                    textInput.value = numberInput.value;
                    node.data[inputSchema.label] = numberInput.value;
                    const aliases = propertyMappings[inputSchema.label] || [];
                    aliases.forEach(alias => {
                        node.data[alias] = numberInput.value;
                    });
                    triggerAutoSave();
                });

                textInput.addEventListener('input', () => {
                    numberInput.value = textInput.value;
                    node.data[inputSchema.label] = textInput.value;
                    const aliases = propertyMappings[inputSchema.label] || [];
                    aliases.forEach(alias => {
                        node.data[alias] = textInput.value;
                    });
                    triggerAutoSave();
                });

                fieldWrapper.appendChild(numberInput);
                fieldWrapper.appendChild(textInput);
                inputElement = fieldWrapper;
            } else if (varType === 'datetime') {
                inputElement = document.createElement('input');
                inputElement.type = 'text';
                inputElement.placeholder = `e.g. 10 seconds, 1 hour, or YYYY-MM-DDTHH:MM:SS`;
                inputElement.value = initialVal;
                inputElement.style.width = "100%";
                inputElement.addEventListener('input', () => {
                    node.data[inputSchema.label] = inputElement.value;
                    const aliases = propertyMappings[inputSchema.label] || [];
                    aliases.forEach(alias => {
                        node.data[alias] = inputElement.value;
                    });
                    triggerAutoSave();
                });
            } else {
                const varName = node.data["Name"] || node.data["name"] || node.data["VariableName"] || node.data["variableName"] || node.data["Variable"] || node.data["variable"] || "";
                if (varName === "theme.preset") {
                    const fieldWrapper = document.createElement('div');
                    fieldWrapper.className = 'toggle-field-wrapper';
                    fieldWrapper.style.display = 'flex';
                    fieldWrapper.style.flexDirection = 'column';
                    fieldWrapper.style.gap = '4px';

                    const pickerSelect = document.createElement('select');
                    pickerSelect.style.width = "100%";
                    let presets = (catalogs.ThemePresets || []).map(p => p.toLowerCase());
                    if (presets.length === 0) {
                        presets = ["default", "pink", "blue", "dark", "glass"];
                    } else {
                        if (!presets.includes("default")) {
                            presets.unshift("default");
                        }
                    }
                    presets.forEach(p => {
                        const opt = document.createElement('option');
                        opt.value = p;
                        opt.innerText = p;
                        pickerSelect.appendChild(opt);
                    });

                    const textInput = document.createElement('input');
                    textInput.type = 'text';
                    textInput.placeholder = `Enter expression / {this.name}...`;
                    textInput.style.width = "100%";

                    const existsInOptions = presets.includes(initialVal);
                    let isExprMode = (initialVal && !existsInOptions);

                    pickerSelect.style.display = isExprMode ? 'none' : 'block';
                    textInput.style.display = isExprMode ? 'block' : 'none';

                    label.style.display = 'flex';
                    label.style.justifyContent = 'space-between';
                    label.style.alignItems = 'center';

                    const toggleLink = document.createElement('span');
                    toggleLink.className = 'field-toggle-mode';
                    toggleLink.style.fontSize = '9px';
                    toggleLink.style.cursor = 'pointer';
                    toggleLink.style.textDecoration = 'underline';
                    toggleLink.style.color = '#a855f7';
                    toggleLink.style.marginLeft = 'auto';
                    toggleLink.innerText = isExprMode ? "👁️ Preset Dropdown" : "📝 Text Mode";

                    toggleLink.addEventListener('click', (e) => {
                        e.preventDefault();
                        isExprMode = !isExprMode;
                        if (isExprMode) {
                            textInput.style.display = 'block';
                            pickerSelect.style.display = 'none';
                            toggleLink.innerText = "👁️ Preset Dropdown";
                            textInput.value = pickerSelect.value;
                        } else {
                            textInput.style.display = 'none';
                            pickerSelect.style.display = 'block';
                            toggleLink.innerText = "📝 Text Mode";
                            pickerSelect.value = textInput.value;
                        }
                    });
                    label.appendChild(toggleLink);

                    pickerSelect.value = existsInOptions ? initialVal : "default";
                    textInput.value = initialVal || "";

                    pickerSelect.addEventListener('change', () => {
                        textInput.value = pickerSelect.value;
                        node.data[inputSchema.label] = pickerSelect.value;
                        const aliases = propertyMappings[inputSchema.label] || [];
                        aliases.forEach(alias => {
                            node.data[alias] = pickerSelect.value;
                        });
                        triggerAutoSave();
                    });

                    textInput.addEventListener('input', () => {
                        pickerSelect.value = textInput.value;
                        node.data[inputSchema.label] = textInput.value;
                        const aliases = propertyMappings[inputSchema.label] || [];
                        aliases.forEach(alias => {
                            node.data[alias] = textInput.value;
                        });
                        triggerAutoSave();
                    });

                    fieldWrapper.appendChild(pickerSelect);
                    fieldWrapper.appendChild(textInput);
                    inputElement = fieldWrapper;
                } else if (varName === "theme.primaryBgColor" || varName === "theme.textMainColor" || varName === "theme.borderAccentColor") {
                    const fieldWrapper = document.createElement('div');
                    fieldWrapper.className = 'toggle-field-wrapper';
                    fieldWrapper.style.display = 'flex';
                    fieldWrapper.style.flexDirection = 'column';
                    fieldWrapper.style.gap = '4px';

                    const colorInput = document.createElement('input');
                    colorInput.type = 'color';
                    colorInput.style.width = "100%";
                    colorInput.style.height = "32px";
                    colorInput.style.padding = "0";
                    colorInput.style.border = "none";
                    colorInput.style.cursor = "pointer";

                    const textInput = document.createElement('input');
                    textInput.type = 'text';
                    textInput.placeholder = `Enter hex color (e.g. #ff00ff) or expression...`;
                    textInput.style.width = "100%";

                    const existsInOptions = initialVal && /^#[0-9A-Fa-f]{6}$/.test(initialVal);
                    let isExprMode = (initialVal && !existsInOptions);

                    colorInput.style.display = isExprMode ? 'none' : 'block';
                    textInput.style.display = isExprMode ? 'block' : 'none';

                    label.style.display = 'flex';
                    label.style.justifyContent = 'space-between';
                    label.style.alignItems = 'center';

                    const toggleLink = document.createElement('span');
                    toggleLink.className = 'field-toggle-mode';
                    toggleLink.style.fontSize = '9px';
                    toggleLink.style.cursor = 'pointer';
                    toggleLink.style.textDecoration = 'underline';
                    toggleLink.style.color = '#a855f7';
                    toggleLink.style.marginLeft = 'auto';
                    toggleLink.innerText = isExprMode ? "👁️ Color Picker" : "📝 Text Mode";

                    toggleLink.addEventListener('click', (e) => {
                        e.preventDefault();
                        isExprMode = !isExprMode;
                        if (isExprMode) {
                            textInput.style.display = 'block';
                            colorInput.style.display = 'none';
                            toggleLink.innerText = "👁️ Color Picker";
                            textInput.value = colorInput.value;
                        } else {
                            textInput.style.display = 'none';
                            colorInput.style.display = 'block';
                            toggleLink.innerText = "📝 Text Mode";
                            if (/^#[0-9A-Fa-f]{6}$/.test(textInput.value)) {
                                colorInput.value = textInput.value;
                            }
                        }
                    });
                    label.appendChild(toggleLink);

                    colorInput.value = existsInOptions ? initialVal : "#000000";
                    textInput.value = initialVal || "";

                    colorInput.addEventListener('input', () => {
                        textInput.value = colorInput.value;
                        node.data[inputSchema.label] = colorInput.value;
                        const aliases = propertyMappings[inputSchema.label] || [];
                        aliases.forEach(alias => {
                            node.data[alias] = colorInput.value;
                        });
                        triggerAutoSave();
                    });

                    textInput.addEventListener('input', () => {
                        if (/^#[0-9A-Fa-f]{6}$/.test(textInput.value)) {
                            colorInput.value = textInput.value;
                        }
                        node.data[inputSchema.label] = textInput.value;
                        const aliases = propertyMappings[inputSchema.label] || [];
                        aliases.forEach(alias => {
                            node.data[alias] = textInput.value;
                        });
                        triggerAutoSave();
                    });

                    fieldWrapper.appendChild(colorInput);
                    fieldWrapper.appendChild(textInput);
                    inputElement = fieldWrapper;
                } else {
                    inputElement = document.createElement('input');
                    inputElement.type = 'text';
                    inputElement.placeholder = `Enter ${inputSchema.label}...`;
                    inputElement.value = initialVal;
                    inputElement.style.width = "100%";
                    inputElement.addEventListener('input', () => {
                        node.data[inputSchema.label] = inputElement.value;
                        const aliases = propertyMappings[inputSchema.label] || [];
                        aliases.forEach(alias => {
                            node.data[alias] = inputElement.value;
                        });
                        triggerAutoSave();
                    });
                }
            }
            row.appendChild(inputElement);
        } else {
            // Standard Text / Input field or Checkbox
            inputElement = document.createElement('input');
            if (inputSchema.controlType === 'Checkbox') {
                inputElement.type = 'checkbox';
                inputElement.checked = initialVal === true || initialVal === 'true';
                inputElement.style.width = 'auto';
                inputElement.style.margin = '4px 0';
                inputElement.addEventListener('change', () => {
                    node.data[inputSchema.label] = inputElement.checked;
                    const aliases = propertyMappings[inputSchema.label] || [];
                    aliases.forEach(alias => {
                        node.data[alias] = inputElement.checked;
                    });
                    triggerAutoSave();
                });
                
                // Align label and checkbox side-by-side for bento look
                row.style.flexDirection = 'row';
                row.style.alignItems = 'center';
                row.style.gap = '8px';
                label.style.order = '2';
                inputElement.style.order = '1';
                label.style.margin = '0';
            } else {
                inputElement.type = inputSchema.dataType === 'Integer' || inputSchema.dataType === 'Number' ? 'number' : 'text';
                const lowerLabel = (inputSchema.label || '').toLowerCase();
                const nodeName = (node.title || node.name || '').toLowerCase();
                if (lowerLabel === 'value' && nodeName.includes('set attribute')) {
                    inputElement.placeholder = "e.g. {player.attribute.Strength} + 1, 10, or Text";
                } else if (lowerLabel === 'value' && (nodeName.includes('variable: set') || nodeName === 'variable: set')) {
                    inputElement.placeholder = "e.g. {var.Health} + 5, random(1, 6), or Text";
                } else if (lowerLabel === 'formula') {
                    inputElement.placeholder = "e.g. {var.Health} + 5 or random(1, 6)";
                } else {
                    inputElement.placeholder = `Enter ${inputSchema.label}...`;
                }
                inputElement.value = initialVal;
                inputElement.style.width = "100%";
                inputElement.addEventListener('input', () => {
                    node.data[inputSchema.label] = inputElement.value;
                    const aliases = propertyMappings[inputSchema.label] || [];
                    aliases.forEach(alias => {
                        node.data[alias] = inputElement.value;
                    });
                    triggerAutoSave();
                });
            }
            row.appendChild(inputElement);
        }

        if (inputSchema.controlType !== 'RichText' && inputSchema.controlType !== 'TextArea' && !inputSchema.label.toLowerCase().includes('text') && !inputSchema.label.toLowerCase().includes('lines') && !inputSchema.label.toLowerCase().includes('description') && !inputSchema.label.toLowerCase().includes('dialogue')) {
            row.appendChild(inputElement);
        }

        if (inputSchema.label === 'Formula') {
            const helperLink = document.createElement('a');
            helperLink.innerText = "ℹ️ View Supported Math Functions";
            helperLink.style.fontSize = "10px";
            helperLink.style.color = "#89b4fa";
            helperLink.style.cursor = "pointer";
            helperLink.style.marginTop = "2px";
            helperLink.style.textDecoration = "underline";

            const helperPanel = document.createElement('div');
            helperPanel.style.display = "none";
            helperPanel.style.marginTop = "4px";
            helperPanel.style.padding = "6px";
            helperPanel.style.background = "#181825";
            helperPanel.style.border = "1px solid #313244";
            helperPanel.style.borderRadius = "4px";
            helperPanel.style.fontSize = "10px";
            helperPanel.style.color = "#cdd6f4";
            helperPanel.style.lineHeight = "1.4";
            helperPanel.innerHTML = `
                <strong>Operators:</strong> +, -, *, /, %, ^ (power)<br/>
                <strong>Variables:</strong> use {player.health}, {my_var}, etc.<br/>
                <strong>Functions:</strong><br/>
                • <code>random(min, max)</code>: Random integer<br/>
                • <code>min(a, b, ...)</code> / <code>max(a, b, ...)</code><br/>
                • <code>abs(x)</code>: Absolute value<br/>
                • <code>round(x)</code>: Round to nearest whole number<br/>
                • <code>rand(min, max)</code><br/>
                <strong>Example:</strong><br/>
                <code>{weapon_damage} * random(1, 20) - {player_armor}</code>
            `;

            helperLink.addEventListener('click', (e) => {
                e.preventDefault();
                if (helperPanel.style.display === "none") {
                    helperPanel.style.display = "block";
                    helperLink.innerText = "ℹ️ Hide Math Functions";
                } else {
                    helperPanel.style.display = "none";
                    helperLink.innerText = "ℹ️ View Supported Math Functions";
                }
            });

            row.appendChild(helperLink);
            row.appendChild(helperPanel);
        }

        fieldsContainer.appendChild(row);
        node.inputs.push({ label: inputSchema.label, element: inputElement });
    });

    if (type === 'media.playSound' || type === 'media.playVideo' || type === 'media.setBackgroundMusic') {
        const isSound = (type === 'media.playSound' || type === 'media.setBackgroundMusic');
        
        // Helper to grab media ID directly from UI elements to bypass data mapping delays
        const getActiveMediaId = () => {
            const mediaLabel = isSound ? (type === 'media.setBackgroundMusic' ? "Music File" : "Sound File") : "Video File";
            const inputObj = node.inputs.find(inp => inp.label === mediaLabel);
            if (inputObj && inputObj.element) {
                const selectEl = inputObj.element.querySelector('select');
                const textEl = inputObj.element.querySelector('input[type="text"]');
                if (selectEl && selectEl.style.display !== 'none') {
                    return selectEl.value;
                } else if (textEl) {
                    return textEl.value;
                }
            }
            return isSound ? (type === 'media.setBackgroundMusic' ? getPropertyValue(node.data, "Music File") : getPropertyValue(node.data, "Sound File")) : getPropertyValue(node.data, "Video File");
        };

        const soundId = getActiveMediaId();
        const hasEndTime = node.inputs.some(inp => inp.label === "End Time");

        // Create container for Visual Waveform or timeline
        const timelineWrapper = document.createElement('div');
        timelineWrapper.style.marginTop = '8px';
        timelineWrapper.style.padding = '8px';
        timelineWrapper.style.background = '#11111b';
        timelineWrapper.style.border = '1px solid #313244';
        timelineWrapper.style.borderRadius = '6px';
        timelineWrapper.style.position = 'relative';
        
        // Prevent click/drag on timeline container from dragging the entire node
        timelineWrapper.addEventListener('mousedown', (e) => {
            e.stopPropagation();
        });

        const canvas = document.createElement('canvas');
        canvas.width = 240;
        canvas.height = 40;
        canvas.style.width = '100%';
        canvas.style.height = '40px';
        canvas.style.display = 'block';
        canvas.style.borderRadius = '4px';
        timelineWrapper.appendChild(canvas);

        // HTML5 Video Preview Container inside the node
        let videoPreview = null;
        if (!isSound) {
            videoPreview = document.createElement('video');
            videoPreview.style.width = '100%';
            videoPreview.style.maxHeight = '140px';
            videoPreview.style.marginTop = '8px';
            videoPreview.style.borderRadius = '4px';
            videoPreview.style.background = '#000';
            videoPreview.style.display = 'none'; // Only show during preview playback
            timelineWrapper.appendChild(videoPreview);
        }

        const ctx = canvas.getContext('2d');
        let clipDuration = 10.0;
        let peaksData = null;
        let playheadTime = null;

        // Visual markers overlay
        const startMarker = document.createElement('div');
        startMarker.style.position = 'absolute';
        startMarker.style.top = '0';
        startMarker.style.width = '6px';
        startMarker.style.height = '40px';
        startMarker.style.background = '#00ffcc';
        startMarker.style.cursor = 'ew-resize';
        startMarker.style.zIndex = '5';
        timelineWrapper.appendChild(startMarker);

        const endMarker = document.createElement('div');
        endMarker.style.position = 'absolute';
        endMarker.style.top = '0';
        endMarker.style.width = '6px';
        endMarker.style.height = '40px';
        endMarker.style.background = '#ff007f';
        endMarker.style.cursor = 'ew-resize';
        endMarker.style.zIndex = '5';
        timelineWrapper.appendChild(endMarker);

        // Value text displays
        const durationText = document.createElement('div');
        durationText.style.display = 'flex';
        durationText.style.justifyContent = 'space-between';
        durationText.style.fontSize = '9px';
        durationText.style.color = '#cdd6f4';
        durationText.style.marginTop = '4px';
        durationText.innerHTML = `<span>Start: 0.0s</span><span>End: 10.0s</span>`;
        timelineWrapper.appendChild(durationText);

        function drawTimeline() {
            ctx.clearRect(0, 0, canvas.width, canvas.height);
            if (isSound && peaksData && peaksData.length > 0) {
                // Draw true waveform peaks
                ctx.fillStyle = '#89b4fa';
                const barWidth = canvas.width / peaksData.length;
                for (let i = 0; i < peaksData.length; i++) {
                    const h = peaksData[i] * canvas.height;
                    const y = (canvas.height - h) / 2;
                    ctx.fillRect(i * barWidth, y, barWidth - 1, h);
                }
            } else {
                // Draw a simple modern grid for Video
                ctx.strokeStyle = '#313244';
                ctx.lineWidth = 1;
                for (let x = 0; x < canvas.width; x += 20) {
                    ctx.beginPath();
                    ctx.moveTo(x, 0);
                    ctx.lineTo(x, canvas.height);
                    ctx.stroke();
                }
                ctx.fillStyle = 'rgba(137, 180, 250, 0.2)';
                ctx.fillRect(0, 0, canvas.width, canvas.height);
            }

            // Draw interactive playhead
            if (playheadTime !== null && clipDuration > 0) {
                const x = (playheadTime / clipDuration) * canvas.width;
                ctx.strokeStyle = '#ffffff';
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.moveTo(x, 0);
                ctx.lineTo(x, canvas.height);
                ctx.stroke();
            }
        }

        function updateMarkersFromData() {
            let startVal = parseFloat(getPropertyValue(node.data, "Start Time")) || 0;
            let endVal = hasEndTime ? (parseFloat(getPropertyValue(node.data, "End Time")) || clipDuration) : clipDuration;
            
            if (startVal < 0) startVal = 0;
            if (endVal > clipDuration || endVal <= 0) endVal = clipDuration;
            if (startVal > endVal) startVal = endVal;

            const startPct = (startVal / clipDuration) * 100;
            const endPct = (endVal / clipDuration) * 100;

            startMarker.style.left = `calc(${startPct}% - 3px)`;
            if (hasEndTime) {
                endMarker.style.display = 'block';
                endMarker.style.left = `calc(${endPct}% - 3px)`;
                durationText.innerHTML = `<span>Start: ${startVal.toFixed(1)}s</span><span>End: ${endVal.toFixed(1)}s</span>`;
            } else {
                endMarker.style.display = 'none';
                durationText.innerHTML = `<span>Start: ${startVal.toFixed(1)}s</span><span>Duration: ${clipDuration.toFixed(1)}s</span>`;
            }
        }

        // Drag markers behavior
        function setupMarkerDrag(marker, isStart) {
            let active = false;
            marker.addEventListener('mousedown', (e) => {
                e.preventDefault();
                e.stopPropagation(); // Stop propagation to prevent dragging the entire node!
                active = true;
                document.addEventListener('mousemove', onMove);
                document.addEventListener('mouseup', onUp);
            });

            function onMove(e) {
                if (!active) return;
                const rect = canvas.getBoundingClientRect();
                let x = e.clientX - rect.left;
                if (x < 0) x = 0;
                if (x > rect.width) x = rect.width;
                const pct = x / rect.width;
                const seconds = pct * clipDuration;

                if (isStart) {
                    let endVal = hasEndTime ? (parseFloat(getPropertyValue(node.data, "End Time")) || clipDuration) : clipDuration;
                    if (seconds > endVal) return;
                    node.data["Start Time"] = seconds.toFixed(2);
                    node.data["StartTime"] = seconds.toFixed(2);
                } else if (hasEndTime) {
                    let startVal = parseFloat(getPropertyValue(node.data, "Start Time")) || 0;
                    if (seconds < startVal) return;
                    node.data["End Time"] = seconds.toFixed(2);
                    node.data["EndTime"] = seconds.toFixed(2);
                }
                updateMarkersFromData();
                
                // Keep input field values in sync!
                node.inputs.forEach(inp => {
                    if (inp.label === 'Start Time' || inp.label === 'End Time') {
                        const val = getPropertyValue(node.data, inp.label);
                        const inputs = inp.element.querySelectorAll('input');
                        inputs.forEach(i => i.value = val);
                    }
                });
            }

            function onUp() {
                if (active) {
                    active = false;
                    document.removeEventListener('mousemove', onMove);
                    document.removeEventListener('mouseup', onUp);
                    triggerAutoSave();
                }
            }
        }

        setupMarkerDrag(startMarker, true);
        setupMarkerDrag(endMarker, false);

        // Registry for waveform callbacks to support multiple nodes without overwrites
        const canvasId = `wf_${node.id}`;
        canvas.id = canvasId;
        if (!window.waveformCallbacks) {
            window.waveformCallbacks = {};
        }
        window.loadWaveformData = function(elementId, peaks, duration) {
            if (window.waveformCallbacks[elementId]) {
                window.waveformCallbacks[elementId](peaks, duration);
            }
        };
        window.waveformCallbacks[canvasId] = function(peaks, duration) {
            peaksData = peaks;
            clipDuration = duration;

            // Try to resolve exact duration using browser audio element as a safety net
            if (isSound && resolvedFileUri) {
                const aud = new Audio(resolvedFileUri);
                aud.addEventListener('loadedmetadata', () => {
                    if (aud.duration && aud.duration > 0 && Math.abs(clipDuration - aud.duration) > 0.5) {
                        clipDuration = aud.duration;
                        drawTimeline();
                        updateMarkersFromData();
                    }
                }, { once: true });
            }

            drawTimeline();
            updateMarkersFromData();
        };

        // File Path Resolution hooks for local previewing
        let resolvedFileUri = null;
        if (!window.mediaPathCallbacks) {
            window.mediaPathCallbacks = {};
        }
        window.resolveMediaPath = function(mediaId, callbackId, fileUri) {
            if (window.mediaPathCallbacks[callbackId]) {
                window.mediaPathCallbacks[callbackId](fileUri);
            }
        };
        window.mediaPathCallbacks[canvasId] = function(fileUri) {
            resolvedFileUri = fileUri;
            if (videoPreview) {
                videoPreview.src = fileUri;
            }

            // Unconditional local metadata query as fallback/primary for accurate timeline bounds
            if (isSound && fileUri) {
                const aud = new Audio(fileUri);
                aud.addEventListener('loadedmetadata', () => {
                    if (aud.duration && aud.duration > 0) {
                        clipDuration = aud.duration;
                        drawTimeline();
                        updateMarkersFromData();
                    }
                }, { once: true });
            }

            // Decode audio dynamically to build high-fidelity waveforms
            if (isSound && fileUri) {
                fetch(fileUri)
                    .then(response => response.arrayBuffer())
                    .then(arrayBuffer => {
                        const audioCtx = new (window.AudioContext || window.webkitAudioContext)();
                        return audioCtx.decodeAudioData(arrayBuffer);
                    })
                    .then(audioBuffer => {
                        const duration = audioBuffer.duration;
                        const channelData = audioBuffer.getChannelData(0);
                        const steps = 150;
                        const blockSize = Math.floor(channelData.length / steps) || 1;
                        const peaks = new Float32Array(steps);
                        for (let i = 0; i < steps; i++) {
                            let max = 0;
                            for (let j = 0; j < blockSize; j++) {
                                const val = Math.abs(channelData[i * blockSize + j] || 0);
                                if (val > max) max = val;
                            }
                            peaks[i] = max;
                        }
                        let maxPeak = 0;
                        for (let i = 0; i < steps; i++) if (peaks[i] > maxPeak) maxPeak = peaks[i];
                        if (maxPeak > 0) {
                            for (let i = 0; i < steps; i++) peaks[i] /= maxPeak;
                        }
                        if (window.waveformCallbacks[canvasId]) {
                            window.waveformCallbacks[canvasId](Array.from(peaks), duration);
                        }
                    })
                    .catch(err => {
                        console.error("WebView waveform decoding failed, falling back to C# thread:", err);
                        window.invokeCSharpAction(`get-waveform?soundId=${encodeURIComponent(soundId)}&elementId=${canvasId}`);
                    });
            } else if (!isSound && fileUri) {
                // For videos, automatically read duration metadata
                if (videoPreview) {
                    videoPreview.addEventListener('loadedmetadata', () => {
                        clipDuration = videoPreview.duration || 10.0;
                        drawTimeline();
                        updateMarkersFromData();
                    }, { once: true });
                }
            }
        };

        // Trigger C# data fetch
        if (soundId) {
            window.invokeCSharpAction(`get-media-path?mediaId=${encodeURIComponent(soundId)}&callbackId=${canvasId}`);
        }

        drawTimeline();
        updateMarkersFromData();
        fieldsContainer.appendChild(timelineWrapper);

        const previewRow = document.createElement('div');
        previewRow.className = 'field-row';
        previewRow.style.marginTop = '10px';
        previewRow.style.display = 'flex';
        previewRow.style.gap = '8px';

        const playBtn = document.createElement('button');
        playBtn.innerText = "🔊 Play Preview";
        playBtn.style.flex = "1";
        playBtn.style.padding = "6px 8px";
        playBtn.style.fontSize = "11px";
        playBtn.style.background = "#8e2de2";
        playBtn.style.color = "#ffffff";
        playBtn.style.border = "none";
        playBtn.style.borderRadius = "4px";
        playBtn.style.cursor = "pointer";
        playBtn.style.fontWeight = "bold";

        // HTML5 Audio/Video player instance
        const mediaElement = isSound ? document.createElement('audio') : videoPreview;
        let animationFrameId = null;

        function trackPlayhead() {
            if (mediaElement && !mediaElement.paused) {
                playheadTime = mediaElement.currentTime;
                drawTimeline();

                let endVal = hasEndTime ? (parseFloat(getPropertyValue(node.data, "End Time")) || clipDuration) : clipDuration;
                if (mediaElement.currentTime >= endVal) {
                    stopPlayback();
                    return;
                }
                animationFrameId = requestAnimationFrame(trackPlayhead);
            }
        }

        function stopPlayback() {
            if (mediaElement) {
                mediaElement.pause();
                mediaElement.currentTime = parseFloat(getPropertyValue(node.data, "Start Time")) || 0;
            }
            if (animationFrameId) {
                cancelAnimationFrame(animationFrameId);
                animationFrameId = null;
            }
            playheadTime = null;
            drawTimeline();
            if (!isSound && videoPreview) {
                videoPreview.style.display = 'none';
            }
            playBtn.innerText = "🔊 Play Preview";
        }

        mediaElement.addEventListener('ended', stopPlayback);

        playBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();

            const currentId = getActiveMediaId();
            if (!currentId || !resolvedFileUri) {
                alert(isSound ? "Please select a sound file first." : "Please select a video file first.");
                return;
            }

            // Play/Pause toggle logic
            if (window.currentPlayingMedia === mediaElement && !mediaElement.paused) {
                mediaElement.pause();
                if (animationFrameId) {
                    cancelAnimationFrame(animationFrameId);
                    animationFrameId = null;
                }
                playBtn.innerText = "🔊 Play Preview";
                return;
            }

            // Stop other playing previews first
            if (window.currentPlayingMedia && window.currentPlayingMedia !== mediaElement) {
                window.currentPlayingMedia.pause();
                if (window.currentPlayingMedia.onStopCleanup) {
                    window.currentPlayingMedia.onStopCleanup();
                }
            }

            const volume = parseFloat(getPropertyValue(node.data, "Volume")) || 100;

            if (!mediaElement.src || mediaElement.src === "" || mediaElement.ended || playheadTime === null) {
                if (isSound) {
                    mediaElement.src = resolvedFileUri;
                }
                const startTime = parseFloat(getPropertyValue(node.data, "Start Time")) || 0;
                mediaElement.currentTime = startTime;
            }

            mediaElement.volume = volume / 100;

            if (!isSound && videoPreview) {
                videoPreview.style.display = 'block';
            }

            mediaElement.play().then(() => {
                window.currentPlayingMedia = mediaElement;
                mediaElement.onStopCleanup = stopPlayback;
                playBtn.innerText = "⏸️ Pause Preview";
                trackPlayhead();
            }).catch(err => {
                console.error("Playback failed", err);
            });
        });

        const stopBtn = document.createElement('button');
        stopBtn.innerText = "⏹️ Stop";
        stopBtn.style.padding = "6px 8px";
        stopBtn.style.fontSize = "11px";
        stopBtn.style.background = "#3a3a4c";
        stopBtn.style.color = "#ffffff";
        stopBtn.style.border = "none";
        stopBtn.style.borderRadius = "4px";
        stopBtn.style.cursor = "pointer";

        stopBtn.addEventListener('click', (e) => {
            e.preventDefault();
            e.stopPropagation();
            stopPlayback();
        });

        previewRow.appendChild(playBtn);
        previewRow.appendChild(stopBtn);
        fieldsContainer.appendChild(previewRow);
    }
}

// Node Position Context shortcuts
function addNewDialogueNodeAtCursor() { const node = addNewDialogueNode(contextCursorX, contextCursorY); if (node) autoLinkNodeIfPossible(node); triggerAutoSave(); hideContextMenu(); }
function addNewCommandNodeAtCursor() { const node = addNewCommandNode(contextCursorX, contextCursorY); if (node) autoLinkNodeIfPossible(node); triggerAutoSave(); hideContextMenu(); }
function addNewConditionNodeAtCursor() { const node = addNewConditionNode(contextCursorX, contextCursorY); if (node) autoLinkNodeIfPossible(node); triggerAutoSave(); hideContextMenu(); }
function addNewSwitchNodeAtCursor() { const node = addNewSwitchNode(contextCursorX, contextCursorY); if (node) autoLinkNodeIfPossible(node); triggerAutoSave(); hideContextMenu(); }

function deleteNode(id) {
    if (id === 'start') return;

    const node = nodes.find(n => n.id === id);
    if (!node) return;

    node.element.remove();
    nodes = nodes.filter(n => n.id !== id);

    connections = connections.filter(c => !c.fromPinId.startsWith(id) && !c.toPinId.startsWith(id));
    redrawConnections();
    triggerAutoSave(); // Auto-save on node deletion!
}

function clearSelectedNode() {
    if (selectedNode) {
        deleteNode(selectedNode.id);
        selectedNode = null;
    }
}

function getReachableNodeIds() {
    const reachable = new Set();
    reachable.add('start');
    const startConn = connections.find(c => c.fromPinId === "start_out");
    if (!startConn) return reachable;
    
    const startNode = nodes.find(n => n.id === getNodeIdFromPinId(startConn.toPinId));
    if (!startNode) return reachable;
    
    const queue = [startNode];
    while (queue.length > 0) {
        const curr = queue.shift();
        if (reachable.has(curr.id)) continue;
        reachable.add(curr.id);
        
        if (curr.type === 'command') {
            const nextPin = connections.find(c => c.fromPinId === `${curr.id}_out`);
            if (nextPin) {
                const nextNode = nodes.find(n => n.id === getNodeIdFromPinId(nextPin.toPinId));
                if (nextNode) queue.push(nextNode);
            }
        } else if (curr.type === 'condition') {
            const truePin = connections.find(c => c.fromPinId === `${curr.id}_true`);
            if (truePin) {
                const trueNode = nodes.find(n => n.id === getNodeIdFromPinId(truePin.toPinId));
                if (trueNode) queue.push(trueNode);
            }
            const falsePin = connections.find(c => c.fromPinId === `${curr.id}_false`);
            if (falsePin) {
                const falseNode = nodes.find(n => n.id === getNodeIdFromPinId(falsePin.toPinId));
                if (falseNode) queue.push(falseNode);
            }
        } else if (curr.type === 'dialogue') {
            curr.choices.forEach(c => {
                const destPin = connections.find(conn => conn.fromPinId === `${c.rowId}_out`);
                if (destPin) {
                    const destNode = nodes.find(n => n.id === getNodeIdFromPinId(destPin.toPinId));
                    if (destNode) queue.push(destNode);
                }
            });
        } else if (curr.type === 'switch') {
            const defaultPin = connections.find(c => c.fromPinId === `${curr.id}_default`);
            if (defaultPin) {
                const defaultNode = nodes.find(n => n.id === getNodeIdFromPinId(defaultPin.toPinId));
                if (defaultNode) queue.push(defaultNode);
            }
            if (curr.cases) {
                curr.cases.forEach(c => {
                    const destPin = connections.find(conn => conn.fromPinId === `${c.rowId}_out`);
                    if (destPin) {
                        const destNode = nodes.find(n => n.id === getNodeIdFromPinId(destPin.toPinId));
                        if (destNode) queue.push(destNode);
                    }
                });
            }
        }
    }
    return reachable;
}

// Bidirectional Sync back to C#
function saveAndSyncCsharp(isAutoSave = false) {
    const reachable = getReachableNodeIds();
    const unreachableCount = nodes.filter(n => n.id !== 'start' && !reachable.has(n.id)).length;
    if (!isAutoSave && unreachableCount > 0) {
        const confirmMsg = `You have ${unreachableCount} unconnected node(s) in your graph. If you save, these unconnected nodes will be permanently removed. \n\nClick OK to discard them and save/return, or Cancel to stay and fix them.`;
        if (!confirm(confirmMsg)) {
            return "CANCELLED"; // Cancel save
        }
    }

    const actionDto = serializeGraph();
    const json = JSON.stringify(actionDto);
    const base64 = btoa(unescape(encodeURIComponent(json)));
    if (typeof invokeCSharpAction === 'function') {
        invokeCSharpAction("sync?data=" + base64);
    } else {
        window.location.href = "rags-action://sync?data=" + base64;
    }
    return base64;
}

function serializeGraph() {
    const startConn = connections.find(c => c.fromPinId === "start_out");
    const startNode = startConn ? nodes.find(n => n.id === getNodeIdFromPinId(startConn.toPinId)) : null;

    const rootNodes = startNode ? buildFlatSequence(startNode) : [];

    return {
        Name: activeActionName,
        Trigger: activeActionTrigger,
        InitallyActive: activeActionInitallyActive,
        DirectionFilter: activeActionDirectionFilter,
        Nodes: rootNodes
    };
}

// Generate flat sequence of steps connected in a straight line
function buildFlatSequence(startNode) {
    const list = [];
    let current = startNode;
    const visited = new Set();

    while (current && !visited.has(current.id)) {
        visited.add(current.id);
        
        const nodeJson = buildNodeJsonWithoutNext(current);
        if (nodeJson) {
            list.push(nodeJson);
        }

        if (current.type === 'command') {
            const nextPin = connections.find(c => c.fromPinId === `${current.id}_out`);
            current = nextPin ? nodes.find(n => n.id === getNodeIdFromPinId(nextPin.toPinId)) : null;
        } else {
            current = null;
        }
    }
    return list;
}

function buildNodeJsonWithoutNext(node) {
    const res = buildNodeJsonWithoutNextRaw(node);
    if (res) {
        res._originalId = node.id;
    }
    return res;
}

function buildNodeJsonWithoutNextRaw(node) {
    if (!node) return null;

    if (node.type === 'dialogue') {
        const choiceDtos = node.choices.map(c => {
            const destPin = connections.find(conn => conn.fromPinId === `${c.rowId}_out`);
            const destNode = destPin ? nodes.find(n => n.id === getNodeIdFromPinId(destPin.toPinId)) : null;
            return {
                text: c.textElement.value,
                destinationNodeId: destNode ? destNode.id : "",
                commands: destNode ? buildFlatSequence(destNode) : []
            };
        });

        return {
            "$type": "general.startDialogue",
            "dialogueId": node.id,
            "characterLines": node.data.characterLines || "",
            "choices": choiceDtos,
            "x": node.x,
            "y": node.y,
            "width": node.width || null,
            "height": node.height || null
        };
    } else if (node.type === 'switch') {
        const casesDict = {};
        node.cases.forEach(c => {
            const destPin = connections.find(conn => conn.fromPinId === `${c.rowId}_out`);
            const destNode = destPin ? nodes.find(n => n.id === getNodeIdFromPinId(destPin.toPinId)) : null;
            casesDict[c.textElement.value] = destNode ? buildFlatSequence(destNode) : [];
        });

        const defaultPin = connections.find(c => c.fromPinId === `${node.id}_default`);
        const defaultNode = defaultPin ? nodes.find(n => n.id === getNodeIdFromPinId(defaultPin.toPinId)) : null;

        return {
            "$type": "general.switch",
            "expression": node.data.expression || "",
            "cases": casesDict,
            "defaultBranch": defaultNode ? buildFlatSequence(defaultNode) : [],
            "x": node.x,
            "y": node.y,
            "width": node.width || null,
            "height": node.height || null
        };
    } else if (node.type === 'command') {
        const commandJson = {
            "$type": node.data.commandType,
            "label": node.data.label || "",
            "x": node.x,
            "y": node.y,
            "width": node.width || null,
            "height": node.height || null
        };
        if (node.inputs) {
            node.inputs.forEach(inp => {
                let val = getPropertyValue(node.data, inp.label);
                if (val === undefined) val = "";
                
                // Map to primary C# property name
                const aliases = propertyMappings[inp.label] || [];
                let primaryCsharpProp = aliases[0] || inp.label;
                
                // Override property mapping for C# commands which expect ObjectId/ContainerObjectId
                if (inp.label === 'Item') {
                    if (node.data.commandType && node.data.commandType.startsWith('item.')) {
                        primaryCsharpProp = 'ItemId';
                    } else {
                        primaryCsharpProp = 'ObjectId';
                    }
                } else if (inp.label === 'Container Object' && node.data.commandType === 'object.moveInsideObject') {
                    primaryCsharpProp = 'ContainerObjectId';
                }
                
                commandJson[primaryCsharpProp] = val;
                
                // Keep original label for JS graph canvas reload consistency
                commandJson[inp.label] = val;
            });
        } else {
            commandJson.text = node.data.text || "";
            commandJson.characterId = node.data.characterId || "";
            commandJson.amount = node.data.amount || 0;
        }
        return commandJson;
    } else if (node.type === 'condition') {
        const truePin = connections.find(c => c.fromPinId === `${node.id}_true`);
        const falsePin = connections.find(c => c.fromPinId === `${node.id}_false`);

        const trueNode = truePin ? nodes.find(n => n.id === getNodeIdFromPinId(truePin.toPinId)) : null;
        const falseNode = falsePin ? nodes.find(n => n.id === getNodeIdFromPinId(falsePin.toPinId)) : null;

        const conditionJson = {
            "$type": node.data.conditionType,
            "label": node.data.label || "",
            "trueBranch": trueNode ? buildFlatSequence(trueNode) : [],
            "falseBranch": falseNode ? buildFlatSequence(falseNode) : [],
            "x": node.x,
            "y": node.y,
            "width": node.width || null,
            "height": node.height || null
        };

        if (node.inputs) {
            node.inputs.forEach(inp => {
                let val = getPropertyValue(node.data, inp.label);
                if (val === undefined) val = "";
                
                // Map to primary C# property name
                const aliases = propertyMappings[inp.label] || [];
                let primaryCsharpProp = aliases[0] || inp.label;
                
                if (inp.label === 'Container Object' && (node.data.conditionType === 'item.inObject' || node.data.conditionType === 'item.notInObject')) {
                    primaryCsharpProp = 'ContainerObjectId';
                } else if (node.data.conditionType && node.data.conditionType.startsWith('date.')) {
                    if (inp.label === 'Variable') {
                        primaryCsharpProp = 'VariableName';
                    } else if (inp.label === 'Variable A') {
                        primaryCsharpProp = 'VariableNameA';
                    } else if (inp.label === 'Variable B') {
                        primaryCsharpProp = 'VariableNameB';
                    }
                }
                
                conditionJson[primaryCsharpProp] = val;
                
                // Keep original label for JS graph canvas reload consistency
                conditionJson[inp.label] = val;
            });
        } else {
            conditionJson.value = node.data.value || "";
        }
        return conditionJson;
    }
    return null;
}

// C# Hook to populate existing JSON action trees and databases
window.loadActionGraph = function(actionJson, commandsDb, conditionsDb, catalogsDb, typesMap) {
    isLoadingGraph = true;
    try {
        // Cache parameters for AI revert
        lastActionJson = actionJson;
        lastCommandsDb = commandsDb;
        lastConditionsDb = conditionsDb;
        lastCatalogsDb = catalogsDb;
        lastTypesMap = typesMap;

        // Reset panning and zoom to center and show the Start Node
        panX = 0;
        panY = 0;
        zoom = 1.0;

        nodesLayer.innerHTML = "";
        nodes = [];
        connections = [];

        // Store dynamic databases
        catalogs = catalogsDb || {};
        nameToTypeMap = {};
        typeToNameMap = {};
        typeToInputsMap = {};

        // Build C# type maps
        if (typesMap) {
            getArray(typesMap).forEach(tm => {
                nameToTypeMap[tm.TypeName] = tm.Discriminator;
                nameToTypeMap[normalize(tm.TypeName)] = tm.Discriminator;
                typeToNameMap[tm.Discriminator] = tm.TypeName;
            });
        }

        // Map Inputs Schema
        if (commandsDb && commandsDb.commands) {
            getArray(commandsDb.commands).forEach(cmd => {
                let type = nameToTypeMap[cmd.name] || nameToTypeMap[normalize(cmd.name)] || fallbackDiscriminators[normalize(cmd.name)];
                if (!type) {
                    const combined = cmd.category + ": " + cmd.name;
                    type = nameToTypeMap[combined] || nameToTypeMap[normalize(combined)] || fallbackDiscriminators[normalize(combined)];
                }
                if (type) {
                    typeToInputsMap[type] = cmd;
                }
            });
        }

        if (conditionsDb && conditionsDb.conditions) {
            getArray(conditionsDb.conditions).forEach(cond => {
                let type = nameToTypeMap[cond.name] || nameToTypeMap[normalize(cond.name)] || fallbackDiscriminators[normalize(cond.name)];
                if (!type) {
                    const combined = cond.category + ": " + cond.name;
                    type = nameToTypeMap[combined] || nameToTypeMap[normalize(combined)] || fallbackDiscriminators[normalize(combined)];
                }
                if (type) {
                    typeToInputsMap[type] = cond;
                }
            });
        }

        AVAILABLE_COMMANDS = [];
        if (commandsDb && commandsDb.commands) {
            getArray(commandsDb.commands).forEach(cmd => {
                let type = nameToTypeMap[cmd.name] || nameToTypeMap[normalize(cmd.name)] || fallbackDiscriminators[normalize(cmd.name)];
                if (!type) {
                    const combined = cmd.category + ": " + cmd.name;
                    type = nameToTypeMap[combined] || nameToTypeMap[normalize(combined)] || fallbackDiscriminators[normalize(combined)];
                }
                if (type) {
                    AVAILABLE_COMMANDS.push({ type: type, label: cmd.name, category: cmd.category, inputs: cmd.inputs || [] });
                }
            });
        }
        AVAILABLE_COMMANDS.sort((a, b) => a.label.localeCompare(b.label));

        AVAILABLE_CONDITIONS = [];
        if (conditionsDb && conditionsDb.conditions) {
            getArray(conditionsDb.conditions).forEach(cond => {
                let type = nameToTypeMap[cond.name] || nameToTypeMap[normalize(cond.name)] || fallbackDiscriminators[normalize(cond.name)];
                if (!type) {
                    const combined = cond.category + ": " + cond.name;
                    type = nameToTypeMap[combined] || nameToTypeMap[normalize(combined)] || fallbackDiscriminators[normalize(combined)];
                }
                if (type) {
                    AVAILABLE_CONDITIONS.push({ type: type, label: cond.name, category: cond.category, inputs: cond.inputs || [] });
                }
            });
        }
        AVAILABLE_CONDITIONS.sort((a, b) => a.label.localeCompare(b.label));

        // Dynamic header title update
        activeActionName = actionJson?.Name || actionJson?.name || "Visual Action Node";
        activeActionTrigger = actionJson?.Trigger || actionJson?.trigger || "UserClicked";
        activeActionInitallyActive = (actionJson?.InitallyActive !== undefined) ? actionJson.InitallyActive : 
                                     ((actionJson?.initallyActive !== undefined) ? actionJson.initallyActive : 
                                     ((actionJson?.initiallyActive !== undefined) ? actionJson.initiallyActive : true));
        activeActionDirectionFilter = actionJson?.DirectionFilter || actionJson?.directionFilter || "All";
        console.log("[graph_editor] loadActionGraph received activeActionDirectionFilter:", activeActionDirectionFilter, "actionJson:", actionJson);

        const titleEl = document.getElementById("editor-title");
        if (titleEl) {
            titleEl.innerText = "Editing Action: " + activeActionName;
        }

        // Always create the permanent Start Node at (50, 150)
        createStartNode();

        const rawNodesList = actionJson?.Nodes || actionJson?.nodes || actionJson?.Steps || actionJson?.steps;
        const nodesList = getArray(rawNodesList);
        if (!nodesList || nodesList.length === 0) {
            redrawConnections();
            updateTransform();
            return;
        }

        // Render the sequential node-graph connected list starting from Start Node
        const firstNode = parseFlatSequence(nodesList, 430, 150);
        if (firstNode) {
            connections.push({
                fromPinId: "start_out",
                toPinId: `${firstNode.id}_in`,
                type: 'exec'
            });
        }

        redrawConnections();
        updateTransform();

        // Automatically assign newly created element to the calling node's field
        if (window.lastAddedElementContext) {
            const ctx = window.lastAddedElementContext;
            window.lastAddedElementContext = null;
            const targetNode = nodes.find(n => n.id === ctx.nodeId);
            if (targetNode) {
                // Find matching newly added element ID / Name in catalogs
                let valToAssign = ctx.name;
                if (ctx.dataType === "Room" && catalogs.Rooms) {
                    const match = catalogs.Rooms.find(r => r.Name === ctx.name);
                    if (match) valToAssign = match.Id || match.id;
                } else if (ctx.dataType === "GameObject" && catalogs.GameObjects) {
                    const match = catalogs.GameObjects.find(o => o.Name === ctx.name);
                    if (match) valToAssign = match.Id || match.id;
                } else if (ctx.dataType === "Character" && catalogs.Characters) {
                    const match = catalogs.Characters.find(c => c.Name === ctx.name);
                    if (match) valToAssign = match.Id || match.id;
                } else if (ctx.dataType === "Timer" && catalogs.Timers) {
                    const match = catalogs.Timers.find(t => t.Name === ctx.name);
                    if (match) valToAssign = match.Id || match.id;
                } else if (ctx.dataType === "Function" && catalogs.Functions) {
                    const match = catalogs.Functions.find(f => f.Name === ctx.name);
                    if (match) valToAssign = match.Id || match.id;
                }
                
                targetNode.data[ctx.fieldLabel] = valToAssign;
                const aliases = propertyMappings[ctx.fieldLabel] || [];
                aliases.forEach(alias => {
                    targetNode.data[alias] = valToAssign;
                });
                
                // Refresh visual representation and save graph state
                refreshCommandFields(targetNode);
                triggerAutoSave();
            }
        }
    } catch (err) {
        console.error("Visual editor load failed: ", err);
        alert("Visual action editor failed to load:\n\nError: " + err.message + "\n\nStack:\n" + err.stack);
    } finally {
        isLoadingGraph = false;
    }
};

// Generate a sequence of nodes drawn connected sequentially
function parseFlatSequence(nodeList, x, y) {
    const list = getArray(nodeList);
    if (!list || list.length === 0) return null;

    let firstNode = null;
    let prevNode = null;

    list.forEach((stepData, idx) => {
        // Bug #2: Use saved node position if present (non-zero).
        // Fall back to auto-layout only for nodes that have never been positioned.
        const rawX = stepData.x !== undefined ? stepData.x : (stepData.X !== undefined ? stepData.X : 0);
        const rawY = stepData.y !== undefined ? stepData.y : (stepData.Y !== undefined ? stepData.Y : 0);
        const nodeX = (rawX !== 0) ? rawX : x + idx * 380;
        const nodeY = (rawY !== 0) ? rawY : y;

        const currNode = parseAndCreateNode(stepData, nodeX, nodeY);
        if (!firstNode) firstNode = currNode;

        if (prevNode && currNode) {
            connections.push({
                fromPinId: `${prevNode.id}_out`,
                toPinId: `${currNode.id}_in`,
                type: 'exec'
            });
        }
        prevNode = currNode;
    });

    return firstNode;
}

function parseAndCreateNode(data, x, y) {
    if (!data) return null;

    if (data["$type"] === "general.switch") {
        const node = addNewSwitchNode(x, y);
        node.data.expression = data.Expression !== undefined ? data.Expression : (data.expression || "");
        const input = node.element.querySelector('input[type="text"]');
        if (input) input.value = node.data.expression;

        // Restore Cases
        const cases = data.Cases || data.cases;
        if (cases) {
            let idx = 0;
            const container = document.getElementById(`${node.id}_cases_container`);
            for (const key of Object.keys(cases)) {
                if (key.startsWith('$')) continue;
                const caseId = Date.now() + idx;
                addSwitchCaseRow(node, container, key, caseId);

                const caseCmds = getArray(cases[key]);
                if (caseCmds && caseCmds.length > 0) {
                    const child = parseFlatSequence(caseCmds, x + 380, y + idx * 240);
                    if (child) {
                        connections.push({
                            fromPinId: `case_${caseId}_out`,
                            toPinId: `${child.id}_in`,
                            type: 'switch-case'
                        });
                    }
                }
                idx++;
            }
        }

        // Restore DefaultBranch
        const defaultBranch = getArray(data.DefaultBranch || data.defaultBranch);
        if (defaultBranch && defaultBranch.length > 0) {
            const child = parseFlatSequence(defaultBranch, x + 380, y - 120);
            if (child) {
                connections.push({
                    fromPinId: `${node.id}_default`,
                    toPinId: `${child.id}_in`,
                    type: 'default'
                });
            }
        }
        return node;
    }

    if (data["$type"] === "general.startDialogue") {
        const node = addNewDialogueNode(x, y);
        node.data.characterLines = data.CharacterLines !== undefined ? data.CharacterLines : (data.characterLines || "");
        const textarea = node.element.querySelector('textarea');
        if (textarea) textarea.value = node.data.characterLines;

        const previewBody = node.element.querySelector('.live-preview-body');
        updateLivePreview(textarea, previewBody);

        const dialogueChoices = getArray(data.Choices || data.choices);
        if (dialogueChoices && dialogueChoices.length > 0) {
            dialogueChoices.forEach((choice, idx) => {
                const choiceId = Date.now() + idx;
                const container = document.getElementById(`${node.id}_choices_container`);
                const choiceText = choice.Text !== undefined ? choice.Text : (choice.text || "");
                addDialogueChoiceRow(node, container, choiceText, choiceId);
                
                const choiceCmds = getArray(choice.Commands || choice.commands);
                if (choiceCmds && choiceCmds.length > 0) {
                    const child = parseFlatSequence(choiceCmds, x + 380, y + idx * 240);
                    if (child) {
                        connections.push({
                            fromPinId: `choice_${choiceId}_out`,
                            toPinId: `${child.id}_in`,
                            type: 'dialogue-choice'
                        });
                    }
                }
            });
        }
        if (node) {
            const w = data.Width !== undefined ? data.Width : data.width;
            const h = data.Height !== undefined ? data.Height : data.height;
            if (w) {
                node.width = w;
                node.element.style.width = `${w}px`;
            }
            if (h) {
                node.height = h;
                node.element.style.minHeight = `${h}px`;
                node.element.style.height = 'auto';
            }
        }
        return node;
    } else {
        const isCondition = AVAILABLE_CONDITIONS.some(c => c.type === data["$type"]) || 
                            data["$type"].includes("equals") || 
                            data["$type"].includes("compare") || 
                            data["$type"].includes("held") || 
                            data["$type"].includes("inRoom") || 
                            data["$type"].includes("isExitLocked") || 
                            data["$type"].includes("gender");

        if (isCondition) {
            if (!AVAILABLE_CONDITIONS.some(c => c.type === data["$type"])) {
                let label = typeToNameMap[data["$type"]] || data["$type"].replace('.', ': ');
                AVAILABLE_CONDITIONS.push({ type: data["$type"], label: label });
            }
            const node = addNewConditionNode(x, y);
            node.data.conditionType = data["$type"];
            
            // Populate parameters data
            Object.keys(data).forEach(key => {
                if (key !== "trueBranch" && key !== "falseBranch" && key !== "TrueBranch" && key !== "FalseBranch") {
                    node.data[key] = data[key];
                    const capKey = key.charAt(0).toUpperCase() + key.slice(1);
                    const lowKey = key.charAt(0).toLowerCase() + key.slice(1);
                    node.data[capKey] = data[key];
                    node.data[lowKey] = data[key];
                }
            });

            const select = node.element.querySelector('select');
            if (select) {
                populateSelectWithOptions(select, AVAILABLE_CONDITIONS);
                select.value = data["$type"];
                // Trigger label rename if needed
                const truePin = node.element.querySelector('.pin.output.true');
                const falsePin = node.element.querySelector('.pin.output.false');
                if (select.value === 'variable.forEachLoop') {
                    if (truePin && truePin.parentNode) truePin.parentNode.firstChild.textContent = 'Loop Body';
                    if (falsePin && falsePin.parentNode) falsePin.parentNode.firstChild.textContent = 'Completed';
                } else {
                    if (truePin && truePin.parentNode) truePin.parentNode.firstChild.textContent = 'True';
                    if (falsePin && falsePin.parentNode) falsePin.parentNode.firstChild.textContent = 'False';
                }
            }

            refreshCommandFields(node);

            const trueBr = getArray(data.TrueBranch || data.trueBranch);
            if (trueBr && trueBr.length > 0) {
                const child = parseFlatSequence(trueBr, x + 350, y - 120);
                if (child) {
                    connections.push({
                        fromPinId: `${node.id}_true`,
                        toPinId: `${child.id}_in`,
                        type: 'true'
                    });
                }
            }

            const falseBr = getArray(data.FalseBranch || data.falseBranch);
            if (falseBr && falseBr.length > 0) {
                const child = parseFlatSequence(falseBr, x + 350, y + 120);
                if (child) {
                    connections.push({
                        fromPinId: `${node.id}_false`,
                        toPinId: `${child.id}_in`,
                        type: 'false'
                    });
                }
            }
            if (node) {
                const w = data.Width !== undefined ? data.Width : data.width;
                const h = data.Height !== undefined ? data.Height : data.height;
                if (w) {
                    node.width = w;
                    node.element.style.width = `${w}px`;
                }
                if (h) {
                    node.height = h;
                    node.element.style.height = `${h}px`;
                }
            }
            return node;
        } else {
            // Command Node
            if (!AVAILABLE_COMMANDS.some(c => c.type === data["$type"])) {
                let label = typeToNameMap[data["$type"]] || data["$type"].replace('.', ': ');
                AVAILABLE_COMMANDS.push({ type: data["$type"], label: label });
            }
            const node = addNewCommandNode(x, y);
            node.data.commandType = data["$type"];
            
            Object.keys(data).forEach(key => {
                if (key !== "nextStep") {
                    node.data[key] = data[key];
                    const capKey = key.charAt(0).toUpperCase() + key.slice(1);
                    const lowKey = key.charAt(0).toLowerCase() + key.slice(1);
                    node.data[capKey] = data[key];
                    node.data[lowKey] = data[key];
                }
            });

            const select = node.element.querySelector('select');
            if (select) {
                populateSelectWithOptions(select, AVAILABLE_COMMANDS);
                select.value = data["$type"];
            }

            refreshCommandFields(node);
            if (node) {
                const w = data.Width !== undefined ? data.Width : data.width;
                const h = data.Height !== undefined ? data.Height : data.height;
                if (w) {
                    node.width = w;
                    node.element.style.width = `${w}px`;
                }
                if (h) {
                    node.height = h;
                    node.element.style.height = `${h}px`;
                }
            }
            return node;
        }
    }
    return null;
}

window.showNodeAISpinner = function(nodeId, fieldName, show) {
    const node = nodes.find(n => n.id === nodeId);
    if (!node) return;

    const aiBtn = node.element.querySelector('.btn-format.ai-glow');
    if (aiBtn) {
        if (show) {
            aiBtn.innerHTML = "⟳ Generating...";
            aiBtn.classList.add('spinning');
            aiBtn.disabled = true;
        } else {
            aiBtn.innerHTML = "✨ AI dialogue";
            aiBtn.classList.remove('spinning');
            aiBtn.disabled = false;
        }
    }
};

window.updateNodeAIResult = function(nodeId, fieldName, resultText) {
    const node = nodes.find(n => n.id === nodeId);
    if (!node) return;

    let txt = null;
    if (node.type === 'dialogue') {
        txt = node.element.querySelector('textarea');
    } else {
        if (node.inputs) {
            const inp = node.inputs.find(i => i.label === fieldName);
            if (inp && inp.element && inp.element.tagName === 'TEXTAREA') {
                txt = inp.element;
            }
        }
    }

    if (txt) {
        txt.value = resultText;
        const previewBody = node.element.querySelector('.live-preview-body');
        if (previewBody) {
            previewBody.innerHTML = renderRichTextPreview(resultText);
        }
        node.data[fieldName || 'characterLines'] = resultText;
        saveAndSyncCsharp(true);
    }
};

window.checkUnconnectedNodes = function() {
    const reached = new Set(['start']);
    const queue = ['start'];
    
    while (queue.length > 0) {
        const currId = queue.shift();
        
        connections.forEach(c => {
            let matchedFromId = null;
            if (c.fromPinId.startsWith('choice_')) {
                const choiceId = parseInt(c.fromPinId.split('_')[1]);
                const parentNode = nodes.find(n => n.choices && n.choices.some(ch => ch.id === choiceId));
                if (parentNode) matchedFromId = parentNode.id;
            } else {
                matchedFromId = getNodeIdFromPinId(c.fromPinId);
            }
            
            if (matchedFromId === currId) {
                const toNodeId = getNodeIdFromPinId(c.toPinId);
                if (toNodeId && !reached.has(toNodeId)) {
                    reached.add(toNodeId);
                    queue.push(toNodeId);
                }
            }
        });
    }
    
    const unconnected = nodes.filter(n => !reached.has(n.id));
    return unconnected.length > 0;
};

// Start visual scripting canvas on page load
initGraph();
createStartNode();
updateTransform();

// Global Dynamic Autocomplete Engine for template braces { and [
let activeAutocomplete = {
    targetInput: null,
    triggerChar: null,
    bracketIndex: -1,
    suggestions: [],
    activeIndex: 0
};

function findUpstreamLoopNode(nodeId, visited = new Set()) {
    if (!nodeId || visited.has(nodeId)) return null;
    visited.add(nodeId);

    const conn = connections.find(c => c.toPinId === `${nodeId}_in`);
    if (!conn) return null;

    const fromPin = conn.fromPinId;
    const upstreamNodeId = getNodeIdFromPinId(fromPin);
    if (!upstreamNodeId) return null;

    const upstreamNode = nodes.find(n => n.id === upstreamNodeId);
    if (!upstreamNode) return null;

    if (upstreamNode.type === 'condition' && upstreamNode.data.conditionType === 'variable.forEachLoop') {
        if (fromPin === `${upstreamNodeId}_true`) {
            return upstreamNode;
        }
        return null;
    }

    return findUpstreamLoopNode(upstreamNodeId, visited);
}

function getLoopContextInfo(nodeId) {
    const loopNode = findUpstreamLoopNode(nodeId);
    if (!loopNode) return null;

    const source = getPropertyValue(loopNode.data, "Loop Source") || getPropertyValue(loopNode.data, "LoopSource") || "Variable";
    const arrayVar = getPropertyValue(loopNode.data, "Array Variable") || getPropertyValue(loopNode.data, "ArrayVariable") || "";
    return { source, arrayVar };
}

function getAutocompleteSuggestions(triggerChar) {
    const list = [];
    if (triggerChar === '{') {
        // Current Object Property (this.*)
        list.push({ token: "this.Id", typeName: "Current Object Property", desc: "Unique ID of this object." });
        list.push({ token: "this.Name", typeName: "Current Object Property", desc: "Name of this object." });
        list.push({ token: "this.Description", typeName: "Current Object Property", desc: "Description of this object." });
        list.push({ token: "this.portrait", typeName: "Current Object Property", desc: "Portrait or image path." });
        list.push({ token: "room.Id", typeName: "Current Room Property", desc: "Unique ID of the current room." });
        list.push({ token: "player.currentroom", typeName: "Player Property", desc: "ID of the room the player is currently in." });
        
        // Populate this.attributes.* only from catalogs.Owner.Attributes (matching context of the active action owner)
        if (catalogs.Owner && catalogs.Owner.Attributes) {
            getArray(catalogs.Owner.Attributes).forEach(a => {
                list.push({ token: `this.attributes.${a}`, typeName: "Current Object Attribute", desc: `Context object custom attribute '${a}'.` });
            });
        }
        
        // Scan custom attributes dynamically from catalogs databases
        const uniqueAttrNames = new Set();
        if (catalogs.Player && catalogs.Player.Attributes) {
            getArray(catalogs.Player.Attributes).forEach(a => {
                uniqueAttrNames.add(a);
                list.push({ token: `player.attributes.${a}`, typeName: "Player Attribute", desc: `Custom attribute '${a}' on player.` });
            });
        }
        if (catalogs.Characters) {
            getArray(catalogs.Characters).forEach(c => {
                if (c.Attributes) {
                    const nameClean = c.Name.replace(/\s+/g, "");
                    getArray(c.Attributes).forEach(a => {
                        uniqueAttrNames.add(a);
                        list.push({ token: `characters.${nameClean}.attributes.${a}`, typeName: "Character Attribute", desc: `Custom attribute '${a}' on character '${c.Name}'.` });
                    });
                }
            });
        }
        if (catalogs.GameObjects) {
            getArray(catalogs.GameObjects).forEach(o => {
                if (o.Attributes) {
                    const nameClean = o.Name.replace(/\s+/g, "");
                    getArray(o.Attributes).forEach(a => {
                        uniqueAttrNames.add(a);
                        list.push({ token: `objects.${nameClean}.attributes.${a}`, typeName: "Object Attribute", desc: `Custom attribute '${a}' on object '${o.Name}'.` });
                    });
                }
            });
        }
        if (catalogs.Rooms) {
            getArray(catalogs.Rooms).forEach(r => {
                if (r.Attributes) {
                    const nameClean = r.Name.replace(/\s+/g, "");
                    getArray(r.Attributes).forEach(a => {
                        uniqueAttrNames.add(a);
                        list.push({ token: `rooms.${nameClean}.attributes.${a}`, typeName: "Room Attribute", desc: `Custom attribute '${a}' on room '${r.Name}'.` });
                    });
                }
            });
        }
        if (catalogs.Timers) {
            getArray(catalogs.Timers).forEach(t => {
                if (t.Attributes) {
                    const nameClean = t.Name.replace(/\s+/g, "");
                    getArray(t.Attributes).forEach(a => {
                        uniqueAttrNames.add(a);
                        list.push({ token: `timers.${nameClean}.attributes.${a}`, typeName: "Timer Attribute", desc: `Custom attribute '${a}' on timer '${t.Name}'.` });
                    });
                }
            });
        }
        uniqueAttrNames.forEach(a => {
            list.push({ token: `room.attributes.${a}`, typeName: "Current Room Attribute", desc: `Current room custom attribute '${a}'.` });
        });

        // General For Each Loop tokens (only if inside a For Each Loop block)
        const nodeEl = activeAutocomplete.targetInput ? activeAutocomplete.targetInput.closest('.node') : null;
        const nodeId = nodeEl ? nodeEl.id : null;
        const loopContext = getLoopContextInfo(nodeId);

        if (loopContext) {
            if (loopContext.source === "Items" || loopContext.source === "Characters") {
                list.push({ token: "loop.Id", typeName: "Loop Property", desc: "Unique ID of the current loop iteration item." });
                list.push({ token: "loop.Name", typeName: "Loop Property", desc: "Name of the current loop iteration item." });
                list.push({ token: "loop.Description", typeName: "Loop Property", desc: "Description of the current loop iteration item." });
                
                if (loopContext.source === "Items") {
                    list.push({ token: "loop.IsWorn", typeName: "Loop Property (Item)", desc: "Whether the current loop item is worn." });
                    list.push({ token: "loop.IsContainer", typeName: "Loop Property (Item)", desc: "Whether the current loop item is a container." });
                    list.push({ token: "loop.IsCollectible", typeName: "Loop Property (Item)", desc: "Whether the current loop item is collectible." });
                }

                uniqueAttrNames.forEach(a => {
                    list.push({ token: `loop.attributes.${a}`, typeName: "Loop Custom Attribute", desc: `Custom attribute '${a}' of the current loop iteration item.` });
                    list.push({ token: `loop.${a}`, typeName: "Loop Custom Attribute (Direct)", desc: `Direct access to custom attribute '${a}' of the current loop iteration item.` });
                });
            }
        }

        // Player
        list.push({ token: "player.Name", typeName: "Player Property", desc: "Name of the protagonist." });
        list.push({ token: "player.Description", typeName: "Player Property", desc: "Description of the protagonist." });
        list.push({ token: "player.Gender", typeName: "Player Property", desc: "Gender of the protagonist." });
        list.push({ token: "player.portrait", typeName: "Player Property", desc: "Protagonist image portrait path." });

        // Room
        list.push({ token: "room.Name", typeName: "Room Property", desc: "Name of current room." });
        list.push({ token: "room.Description", typeName: "Room Property", desc: "Description of current room." });
        list.push({ token: "room.portrait", typeName: "Room Property", desc: "Image path of current room." });

        // Focus / Object
        list.push({ token: "focus.Name", typeName: "Focus Object Property", desc: "Name of current focus object." });
        list.push({ token: "focus.Description", typeName: "Focus Object Property", desc: "Description of current focus object." });
        list.push({ token: "focus.portrait", typeName: "Focus Object Property", desc: "Image of current focus object." });

        // Variables
        if (catalogs.Variables) {
            catalogs.Variables.forEach(v => {
                const vt = (v.varType || v.VarType || v.Type || v.type || "").toLowerCase();
                if (vt === "datetime") {
                    list.push({ token: `variables.${v.Name}`, typeName: "Datetime Variable (Default)", desc: "Friendly: October 31, 2026 8:00 AM" });
                    list.push({ token: `variables.${v.Name}:date`, typeName: "Datetime Date-only", desc: "Displays date portion: 2026-10-31" });
                    list.push({ token: `variables.${v.Name}:time`, typeName: "Datetime Time-only", desc: "Displays time portion: 08:00:00" });
                    list.push({ token: `variables.${v.Name}:datetime`, typeName: "Datetime Raw ISO-8601", desc: `Raw value: ${v.Value || ''}` });
                } else if (vt === "array") {
                    list.push({ token: `variables.${v.Name}`, typeName: "Array Variable", desc: "Multi-Dimensional Array variable." });
                    // Row count tokens — resolve to the number of rows at runtime
                    list.push({ token: `variables.${v.Name}.count`, typeName: "Array Row Count", desc: `Number of rows in array '${v.Name}'. Use in formulas: random(0, {variables.${v.Name}.count} - 1)` });
                    list.push({ token: `variables.${v.Name}.length`, typeName: "Array Row Count (alias)", desc: `Alias for .count — number of rows in '${v.Name}'.` });
                    list.push({ token: `variables.${v.Name}.rowcount`, typeName: "Array Row Count (alias)", desc: `Alias for .count — number of rows in '${v.Name}'.` });
                    const cols = v.Columns || v.columns;
                    if (cols) {
                        if (loopContext && loopContext.source === "Variable" && String(loopContext.arrayVar).toLowerCase() === String(v.Name).toLowerCase()) {
                            getArray(cols).forEach(col => {
                                list.push({ token: `loop.${col}`, typeName: `Loop Variable (${v.Name})`, desc: `Value of column '${col}' for current iteration of '${v.Name}'.` });
                            });
                        }
                        getArray(cols).forEach(col => {
                            list.push({ token: `variables.${v.Name}.${col}.<row_index>`, typeName: "Array Template (Col-First)", desc: `Access column '${col}' for any row index.` });
                            list.push({ token: `variables.${v.Name}.<row_index>.${col}`, typeName: "Array Template (Row-First)", desc: `Access column '${col}' for any row index.` });
                        });

                        const rowCount = v.RowCount || v.rowCount || 0;
                        if (rowCount > 0 && rowCount <= 10) {
                            for (let r = 0; r < rowCount; r++) {
                                getArray(cols).forEach(col => {
                                    list.push({ token: `variables.${v.Name}.${col}.${r}`, typeName: "Array Cell (Col-First)", desc: `Value of column '${col}' at row ${r} in '${v.Name}'.` });
                                    list.push({ token: `variables.${v.Name}.${r}.${col}`, typeName: "Array Cell (Row-First)", desc: `Value of column '${col}' at row ${r} in '${v.Name}'.` });
                                });
                            }
                        } else {
                            // Fallback if no rows or too many rows
                            getArray(cols).forEach(col => {
                                list.push({ token: `variables.${v.Name}.${col}.0`, typeName: "Array Cell Lookup", desc: `Value of column '${col}' at Row 0 for array '${v.Name}'.` });
                                list.push({ token: `variables.${v.Name}.0.${col}`, typeName: "Array Cell Lookup (Alt)", desc: `Value at Row 0, column '${col}' for array '${v.Name}'.` });
                            });
                        }
                    }
                } else {
                    list.push({ token: `variables.${v.Name}`, typeName: "Global Variable", desc: `State variable. Current: ${v.Value || '0'}` });
                }
            });
        }

        // Characters
        if (catalogs.Characters) {
            catalogs.Characters.forEach(c => {
                const nameClean = c.Name.replace(/\s+/g, "");
                list.push({ token: `characters.${nameClean}.id`, typeName: "Character Property", desc: `Unique ID of character '${c.Name}'.` });
                list.push({ token: `characters.${nameClean}.Name`, typeName: "Character Property", desc: `Name of character '${c.Name}'.` });
                list.push({ token: `characters.${nameClean}.Description`, typeName: "Character Property", desc: `Description of character '${c.Name}'.` });
                list.push({ token: `characters.${nameClean}.Health`, typeName: "Character Property", desc: `Health of character '${c.Name}'.` });
                list.push({ token: `characters.${nameClean}.portrait`, typeName: "Character Property", desc: `Portrait of character '${c.Name}'.` });
            });
        }

        // GameObjects
        if (catalogs.GameObjects) {
            catalogs.GameObjects.forEach(o => {
                const nameClean = o.Name.replace(/\s+/g, "");
                list.push({ token: `objects.${nameClean}.id`, typeName: "Object Property", desc: `Unique ID of object '${o.Name}'.` });
                list.push({ token: `objects.${nameClean}.Name`, typeName: "Object Property", desc: `Name of object '${o.Name}'.` });
                list.push({ token: `objects.${nameClean}.Description`, typeName: "Object Property", desc: `Description of object '${o.Name}'.` });
                list.push({ token: `objects.${nameClean}.portrait`, typeName: "Object Property", desc: `Portrait of object '${o.Name}'.` });
            });
        }

        // Rooms (Add rooms.{name}.id & properties)
        if (catalogs.Rooms) {
            catalogs.Rooms.forEach(r => {
                const nameClean = r.Name.replace(/\s+/g, "");
                list.push({ token: `rooms.${nameClean}.id`, typeName: "Room Property", desc: `Unique ID of room '${r.Name}'.` });
                list.push({ token: `rooms.${nameClean}.Name`, typeName: "Room Property", desc: `Name of room '${r.Name}'.` });
                list.push({ token: `rooms.${nameClean}.Description`, typeName: "Room Property", desc: `Description of room '${r.Name}'.` });
                list.push({ token: `rooms.${nameClean}.portrait`, typeName: "Room Property", desc: `Portrait of room '${r.Name}'.` });
            });
        }
    } else if (triggerChar === '[') {
        // Inline linking entity suggestions
        const directions = ["North", "South", "East", "West", "Up", "Down", "In", "Out"];
        directions.forEach(dir => {
            list.push({ token: dir, typeName: "Exit Direction", desc: "Clickable exit shortcut in player navigation." });
        });

        if (catalogs.GameObjects) {
            catalogs.GameObjects.forEach(o => {
                list.push({ token: o.Name, typeName: "Game Object", desc: `Interactive inline link to object '${o.Name}'.` });
            });
        }
        if (catalogs.Characters) {
            catalogs.Characters.forEach(c => {
                list.push({ token: c.Name, typeName: "Character", desc: `Interactive inline link to character '${c.Name}'.` });
            });
        }
        if (catalogs.Rooms) {
            catalogs.Rooms.forEach(r => {
                list.push({ token: r.Name, typeName: "Room", desc: `Navigation/travel link to room '${r.Name}'.` });
            });
        }
    }
    
    if (triggerChar === '{') {
        list.sort((a, b) => {
            const getGroup = (item) => {
                if (item.token.startsWith("this.")) return 0;
                if (item.token.startsWith("player.")) return 1;
                if (item.token.startsWith("room.")) return 2;
                if (item.token.startsWith("focus.")) return 3;
                return 4;
            };
            const gA = getGroup(a);
            const gB = getGroup(b);
            if (gA !== gB) return gA - gB;
            return a.token.localeCompare(b.token);
        });
    }
    
    return list;
}

function showAutocompletePopup(input, triggerChar, index) {
    activeAutocomplete.targetInput = input;
    activeAutocomplete.triggerChar = triggerChar;
    activeAutocomplete.bracketIndex = index;
    activeAutocomplete.suggestions = getAutocompleteSuggestions(triggerChar);
    activeAutocomplete.activeIndex = 0;

    let popup = document.getElementById('autocomplete-popup');
    if (!popup) {
        popup = document.createElement('div');
        popup.id = 'autocomplete-popup';
        popup.className = 'glass-dropdown';
        popup.style.cssText = `
            position: fixed;
            z-index: 10000;
            max-height: 220px;
            overflow-y: auto;
            min-width: 280px;
            border-radius: 8px;
            border: 1px solid rgba(255, 255, 255, 0.1);
            background: rgba(22, 22, 30, 0.96);
            backdrop-filter: blur(10px);
            box-shadow: 0 10px 30px rgba(0, 0, 0, 0.5);
            padding: 4px;
            font-family: system-ui, -apple-system, sans-serif;
            font-size: 12px;
            color: #f1f5f9;
            display: none;
            cursor: pointer;
        `;
        document.body.appendChild(popup);
    }
    popup.style.position = 'fixed';

    renderAutocompleteItems("");
}

function hideAutocompletePopup() {
    activeAutocomplete.targetInput = null;
    activeAutocomplete.triggerChar = null;
    activeAutocomplete.bracketIndex = -1;
    const popup = document.getElementById('autocomplete-popup');
    if (popup) popup.style.display = 'none';
}

function renderAutocompleteItems(query) {
    const popup = document.getElementById('autocomplete-popup');
    if (!popup || !activeAutocomplete.targetInput) return;

    const filtered = activeAutocomplete.suggestions.filter(s => 
        s.token.toLowerCase().includes(query.toLowerCase())
    );

    if (filtered.length === 0) {
        popup.style.display = 'none';
        return;
    }

    popup.innerHTML = '';
    filtered.forEach((item, idx) => {
        const div = document.createElement('div');
        div.style.cssText = `
            padding: 6px 10px;
            border-radius: 4px;
            display: flex;
            flex-direction: column;
            gap: 2px;
            transition: background 0.15s;
            margin-bottom: 2px;
        `;
        if (idx === activeAutocomplete.activeIndex) {
            div.style.background = 'rgba(142, 45, 226, 0.35)';
            div.style.borderLeft = '3px solid #a855f7';
        } else {
            div.style.borderLeft = '3px solid transparent';
        }

        const tokenSpan = document.createElement('span');
        tokenSpan.style.fontWeight = 'bold';
        tokenSpan.style.color = '#fff';
        tokenSpan.textContent = (activeAutocomplete.triggerChar === '{' ? '{' : '[') + item.token + (activeAutocomplete.triggerChar === '{' ? '}' : ']');

        const typeSpan = document.createElement('span');
        typeSpan.style.fontSize = '10px';
        typeSpan.style.color = '#a855f7';
        typeSpan.textContent = item.typeName;

        const descSpan = document.createElement('span');
        descSpan.style.fontSize = '10px';
        descSpan.style.color = '#94a3b8';
        descSpan.textContent = item.desc;

        div.appendChild(tokenSpan);
        div.appendChild(typeSpan);
        div.appendChild(descSpan);

        div.addEventListener('mouseenter', () => {
            activeAutocomplete.activeIndex = idx;
            const children = popup.children;
            for (let i = 0; i < children.length; i++) {
                if (i === idx) {
                    children[i].style.background = 'rgba(142, 45, 226, 0.35)';
                    children[i].style.borderLeft = '3px solid #a855f7';
                } else {
                    children[i].style.background = 'transparent';
                    children[i].style.borderLeft = '3px solid transparent';
                }
            }
        });

        div.addEventListener('click', (e) => {
            e.stopPropagation();
            applyAutocompleteChoice(item);
        });

        popup.appendChild(div);
    });

    const input = activeAutocomplete.targetInput;
    if (!input || input.offsetParent === null) {
        popup.style.display = 'none';
        return;
    }

    const rect = input.getBoundingClientRect();
    if (rect.width === 0 || rect.height === 0 || rect.top < 0 || rect.left < 0) {
        popup.style.display = 'none';
        return;
    }

    popup.style.left = `${rect.left}px`;
    
    const popupHeight = Math.min(220, filtered.length * 48 + 8);
    if (rect.bottom + popupHeight > window.innerHeight) {
        popup.style.top = `${Math.max(10, rect.top - popupHeight - 4)}px`;
    } else {
        popup.style.top = `${rect.bottom + 4}px`;
    }
    
    popup.style.display = 'block';
    
    // Explicitly restore focus to target input to prevent typing lockout
    input.focus();
}

function applyAutocompleteChoice(item) {
    const input = activeAutocomplete.targetInput;
    if (!input) return;

    const val = input.value;
    const bracketIndex = activeAutocomplete.bracketIndex;
    const cursor = input.selectionStart;

    const before = val.substring(0, bracketIndex);
    const after = val.substring(cursor);
    const insertion = activeAutocomplete.triggerChar === '{' ? `{${item.token}}` : `[${item.token}]`;

    input.value = before + insertion + after;
    
    const newCursorPos = before.length + insertion.length;
    input.setSelectionRange(newCursorPos, newCursorPos);
    
    input.dispatchEvent(new Event('input', { bubbles: true }));

    hideAutocompletePopup();
    input.focus();
}

document.addEventListener('input', (e) => {
    const target = e.target;
    if (target.tagName !== 'TEXTAREA' && (target.tagName !== 'INPUT' || target.type !== 'text')) {
        return;
    }
    if (target.offsetParent === null || target.style.display === 'none') {
        return;
    }

    const val = target.value;
    const cursor = target.selectionStart;

    if (cursor > 0) {
        const lastChar = val[cursor - 1];
        if (lastChar === '{' || lastChar === '[') {
            showAutocompletePopup(target, lastChar, cursor - 1);
            return;
        }
    }

    if (activeAutocomplete.targetInput === target) {
        const bracketIndex = activeAutocomplete.bracketIndex;
        if (cursor <= bracketIndex || cursor > val.length) {
            hideAutocompletePopup();
            return;
        }

        const query = val.substring(bracketIndex + 1, cursor);
        const closingBracket = activeAutocomplete.triggerChar === '{' ? '}' : ']';

        if ((activeAutocomplete.triggerChar === '{' && query.includes(' ')) || query.includes(closingBracket)) {
            hideAutocompletePopup();
            return;
        }

        renderAutocompleteItems(query);
    }
});

document.addEventListener('keydown', (e) => {
    if (!activeAutocomplete.targetInput) return;

    const popup = document.getElementById('autocomplete-popup');
    if (!popup || popup.style.display === 'none') return;

    const filtered = activeAutocomplete.suggestions.filter(s => {
        const val = activeAutocomplete.targetInput.value;
        const query = val.substring(activeAutocomplete.bracketIndex + 1, activeAutocomplete.targetInput.selectionStart);
        return s.token.toLowerCase().includes(query.toLowerCase());
    });

    if (e.key === 'ArrowDown') {
        e.preventDefault();
        activeAutocomplete.activeIndex = (activeAutocomplete.activeIndex + 1) % filtered.length;
        renderAutocompleteItems(activeAutocomplete.targetInput.value.substring(activeAutocomplete.bracketIndex + 1, activeAutocomplete.targetInput.selectionStart));
    } else if (e.key === 'ArrowUp') {
        e.preventDefault();
        activeAutocomplete.activeIndex = (activeAutocomplete.activeIndex - 1 + filtered.length) % filtered.length;
        renderAutocompleteItems(activeAutocomplete.targetInput.value.substring(activeAutocomplete.bracketIndex + 1, activeAutocomplete.targetInput.selectionStart));
    } else if (e.key === 'Enter') {
        e.preventDefault();
        const selected = filtered[activeAutocomplete.activeIndex];
        if (selected) {
            applyAutocompleteChoice(selected);
        }
    } else if (e.key === 'Escape') {
        e.preventDefault();
        hideAutocompletePopup();
    }
});

document.addEventListener('click', (e) => {
    const popup = document.getElementById('autocomplete-popup');
    if (popup && !popup.contains(e.target) && (!activeAutocomplete.targetInput || e.target !== activeAutocomplete.targetInput)) {
        hideAutocompletePopup();
    }
});

// Document-level drag/drop handlers to autofill media paths
document.addEventListener('dragover', (e) => {
    e.preventDefault();
});

document.addEventListener('drop', (e) => {
    const text = e.dataTransfer.getData('text') || e.dataTransfer.getData('Text');
    if (!text) return;

    // Check if the text resembles a relative media path pattern (contains a / and folder/extension matches)
    const isMediaPath = text.includes('/') && (
        text.startsWith('images/') || 
        text.startsWith('audio/') || 
        text.startsWith('videos/') || 
        text.startsWith('fonts/') ||
        /\.(png|jpg|jpeg|gif|webp|svg|mp3|wav|ogg|mp4|webm|m4a)$/i.test(text)
    );

    if (!isMediaPath) return;

    const target = e.target;
    if (!target) return;

    if (target.tagName === 'INPUT' || target.tagName === 'TEXTAREA') {
        e.preventDefault();
        target.value = text;
        target.dispatchEvent(new Event('input', { bubbles: true }));
        target.dispatchEvent(new Event('change', { bubbles: true }));
    } else if (target.tagName === 'SELECT') {
        e.preventDefault();
        target.value = text;
        target.dispatchEvent(new Event('change', { bubbles: true }));
    } else {
        // Look for a nearby input, textarea, or select element
        const formGroup = target.closest('.input-row') || target.closest('.node-body') || target.closest('.node-element');
        if (formGroup) {
            const inputs = formGroup.querySelectorAll('input, select, textarea');
            if (inputs.length === 1) {
                const inp = inputs[0];
                inp.value = text;
                inp.dispatchEvent(new Event('input', { bubbles: true }));
                inp.dispatchEvent(new Event('change', { bubbles: true }));
                e.preventDefault();
            } else if (inputs.length > 1) {
                for (const inp of inputs) {
                    const label = inp.previousElementSibling?.innerText?.toLowerCase() || '';
                    const placeholder = inp.placeholder?.toLowerCase() || '';
                    if (label.includes('path') || label.includes('media') || label.includes('image') || label.includes('sound') || label.includes('audio') || label.includes('file') ||
                        placeholder.includes('path') || placeholder.includes('media') || placeholder.includes('image') || placeholder.includes('sound') || placeholder.includes('audio') || placeholder.includes('file')) {
                        inp.value = text;
                        inp.dispatchEvent(new Event('input', { bubbles: true }));
                        inp.dispatchEvent(new Event('change', { bubbles: true }));
                        e.preventDefault();
                        break;
                    }
                }
            }
        }
    }
});

// AI Action Assistant Frontend Controls & Callback Bridge

window.useAiExample = function(text) {
    const input = document.getElementById("ai-prompt-input");
    if (input) {
        input.value = text;
        input.focus();
    }
};

window.openAiAssistantModal = function() {
    document.getElementById("ai-prompt-input").value = "";
    document.getElementById("ai-modal").classList.remove("hide");
    document.getElementById("ai-prompt-input").focus();
};

window.closeAiAssistantModal = function() {
    document.getElementById("ai-modal").classList.add("hide");
};

window.submitAiPrompt = function() {
    const prompt = document.getElementById("ai-prompt-input").value.trim();
    if (!prompt) return;

    const replace = document.getElementById("ai-replace-checkbox").checked;

    // Save current graph state for revert
    const currentGraph = serializeGraph();
    previousGraphState = {
        actionJson: currentGraph
    };

    const json = JSON.stringify(currentGraph);
    const base64 = btoa(unescape(encodeURIComponent(json)));

    closeAiAssistantModal();

    // Show loading spinner status on the AI button
    const btn = document.querySelector(".ai-btn");
    if (btn) {
        btn.innerText = "✨ Generating...";
        btn.disabled = true;
        btn.classList.add("generating");
    }

    const actionUrl = "graph-ai?prompt=" + encodeURIComponent(prompt) + "&replace=" + replace + "&data=" + base64;
    if (typeof invokeCSharpAction === 'function') {
        invokeCSharpAction(actionUrl);
    } else {
        window.location.href = "rags-action://" + actionUrl;
    }
};

window.copyAiPromptToClipboard = function() {
    const prompt = document.getElementById("ai-prompt-input").value.trim();
    if (!prompt) {
        alert("Please describe your script prompt first before copying.");
        return;
    }
    const currentGraph = serializeGraph();
    const json = JSON.stringify(currentGraph);
    const base64 = btoa(unescape(encodeURIComponent(json)));

    const actionUrl = "copy-ai-prompt?prompt=" + encodeURIComponent(prompt) + "&data=" + base64;
    if (typeof invokeCSharpAction === 'function') {
        invokeCSharpAction(actionUrl);
    } else {
        window.location.href = "rags-action://" + actionUrl;
    }
};

window.applyPastedAiJson = function() {
    const rawText = document.getElementById("ai-paste-json").value.trim();
    if (!rawText) {
        alert("Please paste the JSON response first.");
        return;
    }
    try {
        // Robust fallback JSON block extractor
        let cleaned = rawText;
        if (cleaned.startsWith("```json")) {
            cleaned = cleaned.substring("```json".Length);
        }
        if (cleaned.startsWith("```")) {
            cleaned = cleaned.substring("```".Length);
        }
        if (cleaned.endsWith("```")) {
            cleaned = cleaned.substring(0, cleaned.length - "```".Length);
        }
        cleaned = cleaned.trim();

        const firstBracket = cleaned.indexOf('[');
        const lastBracket = cleaned.lastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket) {
            cleaned = cleaned.substring(firstBracket, lastBracket - firstBracket + 1);
        }

        const nodesList = JSON.parse(cleaned);
        if (!Array.isArray(nodesList)) {
            alert("Pasted JSON must be an array of nodes: [ ... ]");
            return;
        }

        // Save current graph state for revert
        const currentGraph = serializeGraph();
        previousGraphState = {
            actionJson: currentGraph
        };

        const jsonText = JSON.stringify(nodesList);
        const base64 = btoa(unescape(encodeURIComponent(jsonText)));
        
        closeAiAssistantModal();
        updateGraphAIResult(base64);
        
        // Reset the paste input for next time
        document.getElementById("ai-paste-json").value = "";
    } catch(e) {
        alert("Failed to parse JSON. Please make sure the pasted text is a valid JSON array. Error: " + e.message);
    }
};

window.updateGraphAIResult = function(newNodesJsonBase64) {
    try {
        const btn = document.querySelector(".ai-btn");
        if (btn) {
            btn.innerText = "✨ AI Assistant";
            btn.disabled = false;
            btn.classList.remove("generating");
        }

        const jsonText = decodeURIComponent(escape(atob(newNodesJsonBase64)));
        const newNodesList = JSON.parse(jsonText);

        // Show the accept/revert banner
        document.getElementById("ai-preview-banner").classList.remove("hide");

        // If we choose to replace, clear the graph nodes connected to start
        const replace = document.getElementById("ai-replace-checkbox").checked;
        if (replace) {
            // Delete all nodes completely to force full recreation of start node state and DOM
            nodes = [];
            connections = [];
            document.getElementById("nodes-layer").innerHTML = "";
            createStartNode();
        }

        // Parse and render the new nodes list
        let startX = 430;
        let startY = 150;

        if (!replace && nodes.length > 1) {
            // Find rightmost node position to prevent overlap
            let maxX = 100;
            nodes.forEach(n => {
                if (n.x > maxX) maxX = n.x;
            });
            startX = maxX + 360;
        }

        const firstNewNode = parseFlatSequence(newNodesList, startX, startY);

        if (firstNewNode) {
            // Mark all newly created nodes with the 'preview-node' class to style them with dashed borders
            const allNewNodeIds = new Set();

            function collectIds(list) {
                if (!list) return;
                list.forEach(item => {
                    const id = item.id || item.dialogueId || item.functionId;
                    if (id) allNewNodeIds.add(id);
                    if (item.trueBranch) collectIds(item.trueBranch);
                    if (item.falseBranch) collectIds(item.falseBranch);
                    if (item.choices) {
                        item.choices.forEach(ch => {
                            if (ch.destinationNodeId) allNewNodeIds.add(ch.destinationNodeId);
                            if (ch.commands) collectIds(ch.commands);
                        });
                    }
                });
            }
            collectIds(newNodesList);

            // Loop through nodes array and add class to newly created node elements
            nodes.forEach(n => {
                if (allNewNodeIds.has(n.id)) {
                    const el = document.getElementById(n.id);
                    if (el) el.classList.add("preview-node");
                }
            });

            if (replace) {
                connections.push({
                    fromPinId: "start_out",
                    toPinId: `${firstNewNode.id}_in`,
                    type: 'exec'
                });
            } else {
                const startConn = connections.find(c => c.fromPinId === "start_out");
                if (!startConn) {
                    connections.push({
                        fromPinId: "start_out",
                        toPinId: `${firstNewNode.id}_in`,
                        type: 'exec'
                    });
                }
            }
        }

        redrawConnections();
        updateTransform();
    } catch (e) {
        console.error("AI node rendering failed: ", e);
        alert("Failed to render AI nodes: " + e.message);
    }
};

window.acceptAiChanges = function() {
    // Remove dashed border class from all preview nodes
    document.querySelectorAll(".preview-node").forEach(el => {
        el.classList.remove("preview-node");
    });

    document.getElementById("ai-preview-banner").classList.add("hide");
    previousGraphState = null;

    // Trigger auto-save
    saveAndSyncCsharp(true);
};

window.revertAiChanges = function() {
    if (!previousGraphState) return;

    // Call loadActionGraph with the original graph representation
    window.loadActionGraph(previousGraphState.actionJson, lastCommandsDb, lastConditionsDb, lastCatalogsDb, lastTypesMap);

    document.getElementById("ai-preview-banner").classList.add("hide");
    previousGraphState = null;
};


// Speech Recognition Manager for native Browser Web Speech API (WebView2)
let recognition = null;
let activeSpeechTarget = null; // Can be a textarea/input element, or 'csharp'
let activeSpeechButton = null;

function initSpeechRecognition() {
    const SpeechRecognition = window.SpeechRecognition || window.webkitSpeechRecognition;
    if (!SpeechRecognition) {
        console.warn("Speech recognition not supported in this browser/webview.");
        return null;
    }
    const rec = new SpeechRecognition();
    rec.continuous = false;
    rec.interimResults = false;
    rec.lang = 'en-US';

    rec.onstart = () => {
        console.log("Speech recognition started");
        updateSpeechButtonVisuals(true);
    };

    rec.onend = () => {
        console.log("Speech recognition ended");
        updateSpeechButtonVisuals(false);
        activeSpeechTarget = null;
    };

    rec.onerror = (e) => {
        console.error("Speech recognition error", e);
        updateSpeechButtonVisuals(false);
        activeSpeechTarget = null;
    };

    rec.onresult = (event) => {
        const transcript = event.results[0][0].transcript;
        console.log("Speech recognition result:", transcript);
        
        if (activeSpeechTarget === 'csharp') {
            const resultUrl = "speech-result?text=" + encodeURIComponent(transcript);
            if (typeof invokeCSharpAction === 'function') {
                invokeCSharpAction(resultUrl);
            } else {
                window.location.href = "rags-action://" + resultUrl;
            }
        } else if (activeSpeechTarget && activeSpeechTarget.tagName) {
            const start = activeSpeechTarget.selectionStart || 0;
            const end = activeSpeechTarget.selectionEnd || 0;
            const text = activeSpeechTarget.value;
            const spaceBefore = (start > 0 && text[start - 1] !== ' ' && text[start - 1] !== '\n') ? ' ' : '';
            const spaceAfter = (end < text.length && text[end] !== ' ' && text[end] !== '\n') ? ' ' : '';
            activeSpeechTarget.value = text.substring(0, start) + spaceBefore + transcript + spaceAfter + text.substring(end);
            
            const ev = new Event('input', { bubbles: true });
            activeSpeechTarget.dispatchEvent(ev);
            
            activeSpeechTarget.focus();
            const newCursorPos = start + spaceBefore.length + transcript.length;
            activeSpeechTarget.setSelectionRange(newCursorPos, newCursorPos);
        }
    };

    return rec;
}

function toggleSpeechRecognition(target, buttonEl) {
    if (!recognition) {
        recognition = initSpeechRecognition();
    }
    if (!recognition) {
        alert("Speech Recognition is not supported or permitted in this environment.");
        return;
    }

    if (activeSpeechTarget) {
        recognition.stop();
        if (activeSpeechTarget === target) {
            return;
        }
    }

    activeSpeechTarget = target;
    activeSpeechButton = buttonEl;
    try {
        recognition.start();
    } catch (err) {
        console.error("Failed to start speech recognition:", err);
    }
}

function updateSpeechButtonVisuals(isRecording) {
    if (activeSpeechButton) {
        if (isRecording) {
            activeSpeechButton.innerHTML = '🔴 Listening...';
            activeSpeechButton.classList.add('recording');
        } else {
            activeSpeechButton.innerHTML = activeSpeechButton.getAttribute('data-original-html') || '🎙️';
            activeSpeechButton.classList.remove('recording');
            activeSpeechButton = null;
        }
    }
    
    if (activeSpeechTarget === 'csharp') {
        const statusUrl = "speech-status?running=" + isRecording;
        if (typeof invokeCSharpAction === 'function') {
            invokeCSharpAction(statusUrl);
        } else {
            window.location.href = "rags-action://" + statusUrl;
        }
    }
}

// Global functions for C# WebView integration
window.startSpeechRecognitionForCsharp = function() {
    const dummyBtn = document.createElement('button');
    dummyBtn.setAttribute('data-original-html', '🎙️');
    toggleSpeechRecognition('csharp', dummyBtn);
};

// AI Modal speech helper
window.toggleAiPromptDictation = function(e) {
    if (e) e.preventDefault();
    const txt = document.getElementById('ai-prompt-input');
    const btn = document.getElementById('ai-modal-dictate-btn');
    if (txt && btn) {
        toggleSpeechRecognition(txt, btn);
    }
};

window.stopSpeechRecognitionForCsharp = function() {
    if (activeSpeechTarget === 'csharp' && recognition) {
        recognition.stop();
    }
};

// Quick Add Element Dialog Modal Logic
let currentAddElementFieldCtx = null; // { dataType: string, node: object, select: element, inputSchema: object }

window.openAddElementModal = function(dataType, node, select, inputSchema) {
    currentAddElementFieldCtx = { dataType, node, select, inputSchema };
    
    document.getElementById("new-element-name").value = "";
    document.getElementById("add-element-title").innerHTML = `<span style="color: #a855f7; font-weight: bold; margin-right: 4px;">+</span> Add New ${dataType}`;
    
    const varTypeWrapper = document.getElementById("new-variable-type-wrapper");
    if (dataType === "Variable") {
        varTypeWrapper.style.display = "flex";
        document.getElementById("new-variable-type").value = "string";
    } else {
        varTypeWrapper.style.display = "none";
    }
    
    document.getElementById("add-element-modal").classList.remove("hide");
    document.getElementById("new-element-name").focus();
};

window.closeAddElementModal = function() {
    document.getElementById("add-element-modal").classList.add("hide");
    if (currentAddElementFieldCtx && currentAddElementFieldCtx.select) {
        // Reset the dropdown so it doesn't stay stuck on "+ Add New..."
        const val = currentAddElementFieldCtx.node.data[currentAddElementFieldCtx.inputSchema.label] || "";
        currentAddElementFieldCtx.select.value = val;
    }
    currentAddElementFieldCtx = null;
};

window.submitAddElement = function() {
    if (!currentAddElementFieldCtx) return;
    const name = document.getElementById("new-element-name").value.trim();
    if (!name) {
        alert("Please enter a name.");
        return;
    }
    
    const dataType = currentAddElementFieldCtx.dataType;
    const varType = dataType === "Variable" ? document.getElementById("new-variable-type").value : "";
    
    // Remember which node & field we are adding this for so we can set its value after reload
    window.lastAddedElementContext = {
        nodeId: currentAddElementFieldCtx.node.id,
        fieldLabel: currentAddElementFieldCtx.inputSchema.label,
        name: name,
        dataType: dataType
    };
    
    document.getElementById("add-element-modal").classList.add("hide");
    
    const actionUrl = "add-element?type=" + encodeURIComponent(dataType) + "&name=" + encodeURIComponent(name) + "&varType=" + encodeURIComponent(varType);
    if (typeof invokeCSharpAction === 'function') {
        invokeCSharpAction(actionUrl);
    } else {
        window.location.href = "rags-action://" + actionUrl;
    }
    currentAddElementFieldCtx = null;
};

// Attribute Creation Modal Logic
let currentAddAttributeCtx = null;

window.openAddAttributeModal = function(node, select, inputSchema) {
    let targetType = "";
    let targetId = "";
    const cmdType = (node.data.commandType || node.data.conditionType || "").toLowerCase();

    if (cmdType.startsWith("char.") || cmdType.startsWith("character.") || cmdType.includes("character")) {
        targetType = "Character";
        targetId = getPropertyValue(node.data, "Character");
    } else if (cmdType.startsWith("item.") || cmdType.includes("item") || cmdType.includes("object")) {
        targetType = "GameObject";
        targetId = getPropertyValue(node.data, "Item") || getPropertyValue(node.data, "Object");
    } else if (cmdType.startsWith("room.") || cmdType.includes("room")) {
        targetType = "Room";
        targetId = getPropertyValue(node.data, "Room");
    } else if (cmdType.startsWith("player.") || cmdType.includes("player")) {
        targetType = "Player";
        targetId = "Player";
    }

    if (targetType !== "Player" && !targetId) {
        alert(`Please select a ${targetType || "target"} first.`);
        const prevVal = node.data[inputSchema.label] || "";
        select.value = prevVal;
        return;
    }

    currentAddAttributeCtx = { node, select, inputSchema, targetType, targetId };

    document.getElementById("new-attribute-name").value = "";
    document.getElementById("new-attribute-value").value = "";
    document.getElementById("add-attribute-title").innerHTML = `<span style="color: #a855f7; font-weight: bold; margin-right: 4px;">+</span> Add Attribute to ${targetType}`;

    document.getElementById("add-attribute-modal").classList.remove("hide");
    document.getElementById("new-attribute-name").focus();
};

window.closeAddAttributeModal = function() {
    document.getElementById("add-attribute-modal").classList.add("hide");
    if (currentAddAttributeCtx && currentAddAttributeCtx.select) {
        const val = currentAddAttributeCtx.node.data[currentAddAttributeCtx.inputSchema.label] || "";
        currentAddAttributeCtx.select.value = val;
    }
    currentAddAttributeCtx = null;
};

window.submitAddAttribute = function() {
    if (!currentAddAttributeCtx) return;
    const name = document.getElementById("new-attribute-name").value.trim();
    const val = document.getElementById("new-attribute-value").value.trim();
    if (!name) {
        alert("Please enter an attribute name.");
        return;
    }

    const targetType = currentAddAttributeCtx.targetType;
    const targetId = currentAddAttributeCtx.targetId;

    window.lastAddedElementContext = {
        nodeId: currentAddAttributeCtx.node.id,
        fieldLabel: currentAddAttributeCtx.inputSchema.label,
        name: name,
        dataType: "Attribute"
    };

    document.getElementById("add-attribute-modal").classList.add("hide");

    const actionUrl = `add-attribute?targetType=${encodeURIComponent(targetType)}&targetId=${encodeURIComponent(targetId)}&name=${encodeURIComponent(name)}&value=${encodeURIComponent(val)}`;
    if (typeof invokeCSharpAction === 'function') {
        invokeCSharpAction(actionUrl);
    } else {
        window.location.href = "rags-action://" + actionUrl;
    }
    currentAddAttributeCtx = null;
};

// Global Keyboard Shortcuts (Copy, Paste, Delete)
document.addEventListener('keydown', (e) => {
    // Ignore keyboard shortcuts when typing inside form inputs
    if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.isContentEditable) {
        return;
    }

    const isCmdOrCtrl = e.metaKey || e.ctrlKey;

    if (isCmdOrCtrl && (e.key === 'c' || e.key === 'C')) {
        e.preventDefault();
        copyNodeAtCursor();
    } else if (isCmdOrCtrl && (e.key === 'v' || e.key === 'V')) {
        e.preventDefault();
        const center = getViewportCenterCoordinates();
        contextCursorX = center.x;
        contextCursorY = center.y;
        pasteNodeAtCursor();
    } else if (e.key === 'Delete' || e.key === 'Backspace') {
        e.preventDefault();
        if (selectedNodes && selectedNodes.length > 0) {
            selectedNodes.forEach(node => deleteNode(node.id));
            selectedNodes = [];
            selectedNode = null;
        } else if (selectedNode) {
            deleteNode(selectedNode.id);
            selectedNode = null;
        }
    }
});

// Collapsible Help Sidebar Controllers
function toggleHelpSidebar() {
    const sidebar = document.getElementById('help-sidebar');
    if (!sidebar) return;
    sidebar.classList.toggle('hide');
    if (!sidebar.classList.contains('hide')) {
        renderHelpNodesList();
    }
}

function switchHelpTab(tabName) {
    const tabSyntax = document.getElementById('tab-pane-syntax');
    const tabNodes = document.getElementById('tab-pane-nodes');
    const btnSyntax = document.getElementById('tab-btn-syntax');
    const btnNodes = document.getElementById('tab-btn-nodes');
    
    if (tabName === 'syntax') {
        tabSyntax.classList.remove('hide');
        tabNodes.classList.add('hide');
        btnSyntax.classList.add('active');
        btnNodes.classList.remove('active');
    } else {
        tabSyntax.classList.add('hide');
        tabNodes.classList.remove('hide');
        btnSyntax.classList.remove('active');
        btnNodes.classList.add('active');
        renderHelpNodesList();
    }
}

function renderHelpNodesList() {
    const listContainer = document.getElementById('help-nodes-list');
    if (!listContainer) return;
    if (listContainer.children.length > 0) return; // Render once

    const all = [];
    
    // Add structural Dialogue & Switch nodes
    all.push({
        type: "dialogue",
        label: "Dialogue Node",
        category: "Structure",
        isCondition: false,
        inputs: []
    });
    all.push({
        type: "switch",
        label: "Switch Node",
        category: "Structure",
        isCondition: false,
        inputs: []
    });

    AVAILABLE_COMMANDS.forEach(c => {
        if (c && c.type) {
            all.push({ ...c, isCondition: false });
        }
    });
    
    AVAILABLE_CONDITIONS.forEach(c => {
        if (c && c.type) {
            all.push({ ...c, isCondition: true });
        }
    });
    
    all.sort((a, b) => (a.label || "").localeCompare(b.label || ""));

    all.forEach(item => {
        const card = document.createElement('div');
        card.className = 'help-node-card';
        const typeStr = item.type || "";
        card.id = `help-node-${typeStr.replace(/\./g, '-')}`;
        card.setAttribute('data-type', typeStr);
        card.setAttribute('data-label', (item.label || "").toLowerCase());
        card.setAttribute('data-category', (item.category || "General").toLowerCase());
        
        const title = document.createElement('div');
        title.className = 'help-node-title';
        title.textContent = item.label || "Unnamed Node";
        card.appendChild(title);
        
        const cat = document.createElement('div');
        cat.className = 'help-node-category';
        cat.textContent = (item.isCondition ? 'Condition | ' : 'Command | ') + (item.category || "General");
        card.appendChild(cat);
        
        const desc = document.createElement('div');
        desc.className = 'help-node-desc';
        
        const theoryText = nodeDescriptions[item.type] || "Executes a scripting action command or conditional check.";
        let htmlContent = `<p style="margin-bottom: 6px; color: var(--text-color); font-size: 11.5px; line-height: 1.4; opacity: 0.95;">${theoryText}</p>`;
        
        if (item.inputs && item.inputs.length > 0) {
            const inputsStr = item.inputs.map(inp => `${inp.label} (${inp.dataType || 'String'})`).join(', ');
            htmlContent += `<div style="margin-top: 6px; font-size: 11px; color: var(--text-muted);"><strong>Inputs:</strong> ${inputsStr}</div>`;
        } else if (item.type !== 'dialogue' && item.type !== 'switch') {
            htmlContent += `<div style="margin-top: 6px; font-size: 11px; color: var(--text-muted);"><em>No inputs required.</em></div>`;
        }
        desc.innerHTML = htmlContent;
        card.appendChild(desc);
        
        listContainer.appendChild(card);
    });
}

function filterHelpNodes() {
    const q = document.getElementById('help-search').value.toLowerCase();
    const cards = document.querySelectorAll('.help-node-card');
    cards.forEach(card => {
        const label = card.getAttribute('data-label') || '';
        const category = card.getAttribute('data-category') || '';
        const type = (card.getAttribute('data-type') || '').toLowerCase();
        if (label.includes(q) || category.includes(q) || type.includes(q)) {
            card.style.display = 'block';
        } else {
            card.style.display = 'none';
        }
    });
}

function showNodeHelp(type) {
    if (!type) return;
    const sidebar = document.getElementById('help-sidebar');
    if (sidebar) {
        sidebar.classList.remove('hide');
    }
    
    switchHelpTab('nodes');
    
    const searchInput = document.getElementById('help-search');
    if (searchInput) {
        searchInput.value = '';
        filterHelpNodes();
    }
    
    document.querySelectorAll('.help-node-card').forEach(c => c.classList.remove('highlighted'));
    
    const cardId = `help-node-${type.replace(/\./g, '-')}`;
    const card = document.getElementById(cardId);
    if (card) {
        card.classList.add('highlighted');
        card.scrollIntoView({ behavior: 'smooth', block: 'center' });
    }
}

// Theory explanations for each polymorphic Rags command, condition, and structure type
const nodeDescriptions = {
    // Variable Commands
    "var.set": "Sets the value of a game variable to a specific string, number, or expression.",
    "var.evaluate": "Evaluates a mathematical or logical formula and stores the result in a variable.",
    "var.inc": "Increments the value of a numeric variable by a specified amount.",
    "var.dec": "Decrements the value of a numeric variable by a specified amount.",
    "var.setRandom": "Sets a variable to a random integer within a specified range.",
    "variable.forEachLoop": "Iterates over a collection (like character inventory) and executes the loop body for each item.",
    "variable.breakLoop": "Terminates the current loop execution immediately.",
    "variable.setArrayElement": "Sets the value of a specific column and row inside a 2D array variable.",
    "variable.addArrayRow": "Appends a new empty row to a 2D array variable.",
    "variable.removeArrayRow": "Deletes a specific row index from a 2D array variable.",
    "variable.appendText": "Appends a text string to the end of a variable's existing text value.",
    "variable.appendLine": "Appends a text string followed by a new line to a variable.",

    // General Commands
    "general.addCustomChoice": "Dynamically adds a custom selection button to the player's choice list.",
    "general.clearCustomChoice": "Clears a custom choice from the player's choice list.",
    "general.addComment": "Inserts a designer note or documentation comment in the script flow.",
    "general.callFunction": "Invokes a reusable global function script.",
    "general.debugText": "Prints a message to the designer's output console for testing.",
    "general.displayText": "Displays a story block or narrative text to the player's screen.",
    "general.promptInput": "Prompts the player with a text box input and stores their response in a variable.",
    "general.endGame": "Ends the game and returns the player to the main menu.",
    "general.openContainer": "Allows an object container to receive items.",
    "general.closeContainer": "Closes an object container to prevent item interactions.",
    "general.waitForContinue": "Suspends script execution and displays a custom Continue button prompt to the player.",

    // Media Commands
    "media.displayMultimedia": "Displays an image or background picture on the screen.",
    "media.setBackgroundMusic": "Plays a looping audio track as the background music.",
    "media.stopBackgroundMusic": "Stops the currently playing background music.",
    "media.playSound": "Plays a one-shot sound effect.",
    "media.stopSound": "Stops a currently playing sound effect.",
    "media.playVideo": "Plays a video file full-screen or in a media frame.",

    // Character Commands
    "char.displayDescription": "Outputs the current description of a character to the player's screen.",
    "char.moveToRoom": "Moves a character to a specific room.",
    "char.moveToRandomAdjacent": "Moves a character to a random connected room.",
    "char.moveAlongPatrolPath": "Moves a character to their next patrol room.",
    "char.moveInventoryToPlayer": "Transfers all items from a character's inventory to the player.",
    "char.moveToObject": "Places a character inside a container object.",
    "char.setPortraitMedia": "Changes the current portrait image of a character.",
    "char.setActionActive": "Enables or disables a custom action command on a character.",
    "char.setAttribute": "Sets a custom attribute or skill value on a character.",
    "char.setDescription": "Updates a character's narrative description text.",
    "char.setGender": "Changes a character's gender classification.",
    "char.setDisplayName": "Changes the displayed name of a character.",

    // Object/Item Commands
    "object.displayDescription": "Displays the narrative description of an item to the player.",
    "object.moveToCharacter": "Places an item into a character's inventory.",
    "object.moveToInventory": "Places an item directly into the player's inventory.",
    "object.moveInsideObject": "Places an item inside another container item.",
    "room.addObject": "Drops an item into a specific room.",
    "item.setAttribute": "Sets a custom attribute value on an item.",
    "item.wear": "Forces the player or a character to wear an item.",
    "item.remove": "Forces the player or a character to un-wear an item.",

    // Player Commands
    "player.displayDescription": "Displays the protagonist's description text.",
    "player.moveInventoryToChar": "Transfers an item from the player's inventory to a character.",
    "player.moveInventoryToRoom": "Drops an item from the player's inventory into the room.",
    "player.moveTo": "Moves the player to a specific room.",
    "player.screenShake": "Shakes the screen camera with specified intensity and duration.",
    "player.moveToChar": "Moves the player to the room where a specific character is located.",
    "player.moveToObject": "Places the player inside a container object.",
    "player.setActionActive": "Enables or disables a custom action command on the player.",
    "player.setAttribute": "Sets a custom attribute or skill value on the protagonist.",
    "player.setDescription": "Updates the protagonist's description text.",
    "player.setName": "Sets the protagonist's name.",
    "player.setGender": "Sets the protagonist's gender.",
    "player.setPortraitMedia": "Sets the protagonist's portrait image.",
    "player.swapCharacter": "Swaps the active protagonist character with another character, moving inventory and properties.",

    // Room Commands
    "room.displayDescription": "Outputs a room's description text.",
    "room.displayPicture": "Displays the room's main image.",
    "room.moveItemsToPlayer": "Transfers all objects lying in a room to the player's inventory.",
    "room.setDescription": "Updates a room's description text.",
    "room.setPicture": "Changes a room's main background image.",
    "room.setAttribute": "Sets a custom attribute value on a room.",
    "room.lockExit": "Locks a specific direction exit in a room.",
    "room.unlockExit": "Unlocks a locked direction exit in a room.",
    "room.setActionActive": "Enables or disables a custom action command on a room.",

    // UI & Status Elements
    "ui.setStatusBarVisible": "Shows or hides the status bar display.",
    "ui.setHotspotActive": "Enables or disables an interactive screen hotspot.",
    "ui.setCloseButtonVisible": "Shows or hides the close button on the active interactive screen overlay.",
    "item.closeInteractiveScreen": "Closes the currently active item interactive screen.",
    "ui.showSplashScreen": "Triggers showing a named splash screen in-game.",
    "status.show": "Displays a status bar element.",
    "status.hide": "Hides a status bar element.",
    "status.setText": "Updates the display text of a status bar element.",
    "status.setImage": "Updates the icon image of a status bar element.",

    // Timer Commands
    "timer.setAttribute": "Sets a custom attribute value on a timer.",
    "timer.setTimerActive": "Enables or disables a timer's execution.",

    // Conditions
    "char.attributeCheck": "Checks if a character's attribute meets a comparison value.",
    "char.gender": "Checks if a character is Male, Female, or other.",
    "char.inRoom": "Checks if a character is currently in a specific room.",
    "item.attributeCheck": "Checks if an item's attribute meets a comparison value.",
    "item.heldByChar": "Checks if a specific character is holding an item.",
    "item.heldByPlayer": "Checks if the player is holding an item.",
    "item.inObject": "Checks if an item is inside a specific container object.",
    "item.inRoom": "Checks if an item is in a specific room.",
    "item.notHeldByPlayer": "Checks if the player is NOT holding an item.",
    "item.notInObject": "Checks if an item is NOT inside a specific container object.",
    "item.isWorn": "Checks if an item is currently worn by the player/character.",
    "item.canWear": "Checks if an item is configured to be wearable.",
    "player.attributeCheck": "Checks if the player's attribute meets a comparison value.",
    "player.gender": "Checks if the protagonist is Male or Female.",
    "player.inRoom": "Checks if the player is currently in a specific room.",
    "player.sameRoom": "Checks if the player is in the same room as a specific character.",
    "room.attributeCheck": "Checks if a room's attribute meets a comparison value.",
    "room.isExitLocked": "Checks if a specific exit direction is locked in a room.",
    "timer.isActive": "Checks if a timer is currently active.",
    "var.compare": "Compares a variable's value against a static string or number.",
    "date.partCompare": "Compares a specific part (e.g. Hour, Day) of a DateTime variable.",
    "date.isPast": "Checks if a DateTime variable is in the past.",
    "date.isFuture": "Checks if a DateTime variable is in the future.",
    "date.compareVars": "Compares two DateTime variables.",
    "date.diffCompare": "Compares the time difference between two DateTime variables.",
    "date.compareConst": "Compares a DateTime variable against a constant date value.",
    "date.isValid": "Checks if a string is a valid DateTime value.",

    // Core Nodes
    "dialogue": "A Dialogue Node displays text conversations and choice paths. It is the core narrative building block of an action.",
    "switch": "A Switch Node evaluates multiple branches of conditional logic sequentially and splits execution."
};


