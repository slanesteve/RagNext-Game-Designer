/**
 * Rags Node Visual Graph Editor Engine
 * Handles dragging, panning, drawing connections, node events, and C# bridging.
 */

let nodes = [];
let connections = [];
let selectedNode = null;
let activeActionName = "Visual Action Node";

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

// Preloaded commands & conditions catalogs for select dropdowns
const AVAILABLE_COMMANDS = [
    { type: "general.displayText", label: "Display Text" },
    { type: "char.damage", label: "Damage / Heal Character" },
    { type: "char.setState", label: "Set Character State" },
    { type: "general.triggerTurnTick", label: "Trigger Turn Tick" },
    { type: "media.playSound", label: "Play Sound Effect" },
    { type: "player.moveTo", label: "Move Player" }
];

const AVAILABLE_CONDITIONS = [
    { type: "var.equals", label: "Variable Equals" },
    { type: "item.heldByPlayer", label: "Item Held by Player" },
    { type: "player.inRoom", label: "Player in Room" }
];

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

    // Custom Right-Click Menu
    window.addEventListener('contextmenu', (e) => {
        e.preventDefault();
        const bounds = container.getBoundingClientRect();
        contextCursorX = (e.clientX - bounds.left - panX) / zoom;
        contextCursorY = (e.clientY - bounds.top - panY) / zoom;

        contextMenu.style.display = 'block';
        contextMenu.style.left = `${e.clientX}px`;
        contextMenu.style.top = `${e.clientY}px`;
    });

    window.addEventListener('click', () => {
        hideContextMenu();
    });
}

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
}

// Draw/Redraw SVG Connections
function redrawConnections() {
    // Clear old lines
    while (svgLayer.lastChild && svgLayer.lastChild.tagName === 'path') {
        svgLayer.removeChild(svgLayer.lastChild);
    }

    connections.forEach(conn => {
        const fromPin = document.getElementById(conn.fromPinId);
        const toPin = document.getElementById(conn.toPinId);
        if (!fromPin || !toPin) return;

        const path = drawBezierCurve(fromPin, toPin, conn.type);
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
    // Remove old active temp line
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

// Node Engine Creation Methods
function createBaseNode(id, type, title, x, y) {
    const el = document.createElement('div');
    el.className = 'node';
    el.style.left = `${x}px`;
    el.style.top = `${y}px`;
    el.id = id;

    const header = document.createElement('div');
    header.className = `node-header ${type}`;
    header.innerHTML = `<span>${title}</span><span class="node-delete" onclick="deleteNode('${id}')">✕</span>`;
    el.appendChild(header);

    const body = document.createElement('div');
    body.className = 'node-body';
    el.appendChild(body);

    nodesLayer.appendChild(el);

    // Make node draggable
    makeDraggable(el);

    const nodeObj = {
        id,
        type,
        x,
        y,
        element: el,
        bodyElement: body,
        choices: [],
        data: {}
    };
    nodes.push(nodeObj);
    return nodeObj;
}

function makeDraggable(el) {
    let offsetOffsetX = 0;
    let offsetOffsetY = 0;

    el.addEventListener('mousedown', (e) => {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT' || e.target.classList.contains('pin') || e.target.classList.contains('node-delete')) {
            return;
        }
        e.stopPropagation();
        deselectAllNodes();
        selectedNode = nodes.find(n => n.id === el.id);
        el.classList.add('selected');

        offsetOffsetX = e.clientX - el.getBoundingClientRect().left;
        offsetOffsetY = e.clientY - el.getBoundingClientRect().top;

        const onMouseMove = (ev) => {
            const bounds = container.getBoundingClientRect();
            const x = (ev.clientX - bounds.left - offsetOffsetX - panX) / zoom;
            const y = (ev.clientY - bounds.top - offsetOffsetY - panY) / zoom;

            el.style.left = `${x}px`;
            el.style.top = `${y}px`;

            const node = nodes.find(n => n.id === el.id);
            if (node) {
                node.x = x;
                node.y = y;
            }
            redrawConnections();
        };

        const onMouseUp = () => {
            window.removeEventListener('mousemove', onMouseMove);
            window.removeEventListener('mouseup', onMouseUp);
        };

        window.addEventListener('mousemove', onMouseMove);
        window.addEventListener('mouseup', onMouseUp);
    });
}

// Port Configuration Helper
function addPin(node, direction, type, name, pinId) {
    const row = document.createElement('div');
    row.className = 'port-row';
    row.style.textAlign = direction === 'input' ? 'left' : 'right';
    row.innerText = name;

    const pin = document.createElement('div');
    pin.className = `pin ${direction} ${type}`;
    pin.id = pinId;
    
    // Wire pin connection events
    pin.addEventListener('mousedown', (e) => {
        e.stopPropagation();
        activeDrawingPin = { id: pinId, direction, type, node };
    });

    pin.addEventListener('mouseup', (e) => {
        e.stopPropagation();
        if (activeDrawingPin && activeDrawingPin.id !== pinId && activeDrawingPin.direction !== direction) {
            // Establish Connection
            const from = direction === 'input' ? activeDrawingPin.id : pinId;
            const to = direction === 'input' ? pinId : activeDrawingPin.id;
            
            // Avoid duplicate links
            if (!connections.some(c => c.fromPinId === from && c.toPinId === to)) {
                connections.push({
                    fromPinId: from,
                    toPinId: to,
                    type: activeDrawingPin.type
                });
            }
        }
        activeDrawingPin = null;
        redrawConnections();
    });

    row.appendChild(pin);
    node.bodyElement.appendChild(row);
}

// Custom Dialogue Nodes
function addNewDialogueNode(x = 100, y = 100) {
    const id = 'dialogue_' + Date.now();
    const node = createBaseNode(id, 'dialogue', '💬 NPC Dialogue', x, y);

    addPin(node, 'input', 'exec', 'Entry', `${id}_in`);

    // Dialogue Prompt Text Area
    const promptLabel = document.createElement('label');
    promptLabel.innerText = "Character Lines:";
    promptLabel.style.fontSize = "10px";
    promptLabel.style.color = "var(--text-muted)";
    node.bodyElement.appendChild(promptLabel);

    const txt = document.createElement('textarea');
    txt.placeholder = "\"What the character says...\"";
    txt.addEventListener('change', () => { node.data.characterLines = txt.value; });
    node.bodyElement.appendChild(txt);

    // Dynamic Choice List Container
    const choicesList = document.createElement('div');
    choicesList.id = `${id}_choices_container`;
    node.bodyElement.appendChild(choicesList);

    const btn = document.createElement('button');
    btn.className = 'add-choice-btn';
    btn.innerText = "+ Add Choice";
    btn.onclick = () => addDialogueChoiceRow(node, choicesList, "", Date.now());
    node.bodyElement.appendChild(btn);

    return node;
}

function addDialogueChoiceRow(node, container, initialText, choiceId) {
    const rowId = `choice_${choiceId}`;
    const row = document.createElement('div');
    row.style.display = 'flex';
    row.style.gap = '4px';
    row.style.alignItems = 'center';
    row.id = rowId;

    const inp = document.createElement('input');
    inp.value = initialText || "";
    inp.placeholder = "\"Player choice...\"";
    inp.style.flex = "1";
    row.appendChild(inp);

    const del = document.createElement('span');
    del.innerHTML = "✕";
    del.style.cursor = "pointer";
    del.style.fontSize = "12px";
    del.style.color = "var(--pin-false)";
    del.onclick = () => {
        row.remove();
        // Clean connections from this choice pin
        connections = connections.filter(c => c.fromPinId !== `${rowId}_out`);
        node.choices = node.choices.filter(c => c.id !== choiceId);
        redrawConnections();
    };
    row.appendChild(del);

    // Choice output execution pin
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
            connections.push({ fromPinId: pin.id, toPinId: activeDrawingPin.id, type: 'dialogue-choice' });
        }
        activeDrawingPin = null;
        redrawConnections();
    });
    row.appendChild(pin);

    container.appendChild(row);

    const choiceObj = { id: choiceId, textElement: inp, rowId };
    node.choices.push(choiceObj);
}

// Custom Command Nodes
function addNewCommandNode(x = 100, y = 100) {
    const id = 'command_' + Date.now();
    const node = createBaseNode(id, 'command', '➡️ Execute Command', x, y);

    addPin(node, 'input', 'exec', 'In', `${id}_in`);
    addPin(node, 'output', 'exec', 'Out', `${id}_out`);

    const select = document.createElement('select');
    AVAILABLE_COMMANDS.forEach(cmd => {
        const opt = document.createElement('option');
        opt.value = cmd.type;
        opt.innerText = cmd.label;
        select.appendChild(opt);
    });
    select.addEventListener('change', () => { node.data.commandType = select.value; refreshCommandFields(node); });
    node.bodyElement.appendChild(select);

    const fieldContainer = document.createElement('div');
    fieldContainer.id = `${id}_fields`;
    node.bodyElement.appendChild(fieldContainer);

    node.data.commandType = AVAILABLE_COMMANDS[0].type;
    refreshCommandFields(node);

    return node;
}

function refreshCommandFields(node) {
    const container = document.getElementById(`${node.id}_fields`);
    if (!container) return;
    container.innerHTML = "";

    if (node.data.commandType === "general.displayText") {
        const inp = document.createElement('input');
        inp.placeholder = "Lines to display";
        inp.value = node.data.text || "";
        inp.addEventListener('change', () => { node.data.text = inp.value; });
        container.appendChild(inp);
    } else if (node.data.commandType === "char.damage") {
        const cInp = document.createElement('input');
        cInp.placeholder = "Character ID";
        cInp.value = node.data.characterId || "";
        cInp.addEventListener('change', () => { node.data.characterId = cInp.value; });
        container.appendChild(cInp);

        const aInp = document.createElement('input');
        aInp.type = "number";
        aInp.placeholder = "Amount (e.g. -10)";
        aInp.value = node.data.amount || "";
        aInp.addEventListener('change', () => { node.data.amount = parseInt(aInp.value) || 0; });
        container.appendChild(aInp);
    }
}

// Custom Condition Nodes
function addNewConditionNode(x = 100, y = 100) {
    const id = 'cond_' + Date.now();
    const node = createBaseNode(id, 'condition', '🔀 Branch Condition', x, y);

    addPin(node, 'input', 'exec', 'In', `${id}_in`);
    addPin(node, 'output', 'true', 'True', `${id}_true`);
    addPin(node, 'output', 'false', 'False', `${id}_false`);

    const select = document.createElement('select');
    AVAILABLE_CONDITIONS.forEach(c => {
        const opt = document.createElement('option');
        opt.value = c.type;
        opt.innerText = c.label;
        select.appendChild(opt);
    });
    select.addEventListener('change', () => { node.data.conditionType = select.value; });
    node.bodyElement.appendChild(select);

    const valInp = document.createElement('input');
    valInp.placeholder = "Check value / parameters";
    valInp.addEventListener('change', () => { node.data.value = valInp.value; });
    node.bodyElement.appendChild(valInp);

    node.data.conditionType = AVAILABLE_CONDITIONS[0].type;
    return node;
}

// Node Position Context shortcuts
function addNewDialogueNodeAtCursor() { addNewDialogueNode(contextCursorX, contextCursorY); hideContextMenu(); }
function addNewCommandNodeAtCursor() { addNewCommandNode(contextCursorX, contextCursorY); hideContextMenu(); }
function addNewConditionNodeAtCursor() { addNewConditionNode(contextCursorX, contextCursorY); hideContextMenu(); }

function deleteNode(id) {
    const node = nodes.find(n => n.id === id);
    if (!node) return;

    node.element.remove();
    nodes = nodes.filter(n => n.id !== id);

    // Clean connections
    connections = connections.filter(c => !c.fromPinId.startsWith(id) && !c.toPinId.startsWith(id));
    redrawConnections();
}

function clearSelectedNode() {
    if (selectedNode) {
        deleteNode(selectedNode.id);
        selectedNode = null;
    }
}

// Bidirectional Sync back to C#
function saveAndSyncCsharp() {
    // Generate unified JSON
    const actionDto = serializeGraph();
    const json = JSON.stringify(actionDto);
    // Base64 encode to prevent URL parsing issues
    const base64 = btoa(unescape(encodeURIComponent(json)));
    window.location.href = "rags-action://sync?data=" + base64;
}

function serializeGraph() {
    // Build root action object
    const rootNodes = [];
    
    // Find entry dialogue/command node (typically with no inputs)
    nodes.forEach(node => {
        const hasInputs = connections.some(c => c.toPinId === `${node.id}_in`);
        if (!hasInputs) {
            rootNodes.push(buildNodeJson(node));
        }
    });

    return {
        Name: activeActionName,
        Trigger: "UserClicked",
        Nodes: rootNodes.filter(n => n !== null)
    };
}

function buildNodeJson(node) {
    if (!node) return null;

    if (node.type === 'dialogue') {
        const choiceDtos = node.choices.map(c => {
            const destPin = connections.find(conn => conn.fromPinId === `${c.rowId}_out`);
            const destNode = destPin ? nodes.find(n => n.id === destPin.toPinId.split('_')[0]) : null;
            return {
                text: c.textElement.value,
                destinationNodeId: destNode ? destNode.id : "",
                // We recursively build child branches!
                commands: destNode ? [buildNodeJson(destNode)].filter(n => n !== null) : []
            };
        });

        return {
            "$type": "general.startDialogue",
            "dialogueId": node.id,
            "characterLines": node.data.characterLines || "",
            "choices": choiceDtos
        };
    } else if (node.type === 'command') {
        const nextPin = connections.find(c => c.fromPinId === `${node.id}_out`);
        const nextNode = nextPin ? nodes.find(n => n.id === nextPin.toPinId.split('_')[0]) : null;

        return {
            "$type": node.data.commandType,
            "text": node.data.text || "",
            "characterId": node.data.characterId || "",
            "amount": node.data.amount || 0,
            "nextStep": nextNode ? buildNodeJson(nextNode) : null
        };
    } else if (node.type === 'condition') {
        const truePin = connections.find(c => c.fromPinId === `${node.id}_true`);
        const falsePin = connections.find(c => c.fromPinId === `${node.id}_false`);

        const trueNode = truePin ? nodes.find(n => n.id === truePin.toPinId.split('_')[0]) : null;
        const falseNode = falsePin ? nodes.find(n => n.id === falsePin.toPinId.split('_')[0]) : null;

        return {
            "$type": node.data.conditionType,
            "value": node.data.value || "",
            "trueBranch": trueNode ? [buildNodeJson(trueNode)].filter(n => n !== null) : [],
            "falseBranch": falseNode ? [buildNodeJson(falseNode)].filter(n => n !== null) : []
        };
    }
    return null;
}

// C# Hook to populate existing JSON action trees
window.loadActionGraph = function(actionJson) {
    // Clear existing canvas
    nodesLayer.innerHTML = "";
    nodes = [];
    connections = [];

    // Dynamically set header title
    activeActionName = actionJson?.Name || "Visual Action Node";
    const titleEl = document.getElementById("editor-title");
    if (titleEl) {
        titleEl.innerText = "Editing Action: " + activeActionName;
    }

    if (!actionJson || !actionJson.Nodes || actionJson.Nodes.length === 0) {
        // Create a default Dialogue Start Node to greet the designer!
        addNewDialogueNode(150, 150);
        updateTransform();
        return;
    }

    // Populate nodes recursively
    let startX = 100;
    actionJson.Nodes.forEach((nodeData, idx) => {
        parseAndCreateNode(nodeData, startX, 150 + idx * 180);
    });

    updateTransform();
};

function parseAndCreateNode(data, x, y) {
    if (!data) return null;

    if (data["$type"] === "general.startDialogue") {
        const node = addNewDialogueNode(x, y);
        node.data.characterLines = data.characterLines || "";
        const textarea = node.element.querySelector('textarea');
        if (textarea) textarea.value = node.data.characterLines;

        if (data.choices) {
            data.choices.forEach((choice, idx) => {
                const choiceId = Date.now() + idx;
                const container = document.getElementById(`${node.id}_choices_container`);
                addDialogueChoiceRow(node, container, choice.text, choiceId);
                
                // If this choice connects to a sub-node, parse it!
                if (choice.commands && choice.commands.length > 0) {
                    const child = parseAndCreateNode(choice.commands[0], x + 350, y + idx * 220);
                    if (child) {
                        // Connect choice output to child input
                        connections.push({
                            fromPinId: `choice_${choiceId}_out`,
                            toPinId: `${child.id}_in`,
                            type: 'dialogue-choice'
                        });
                    }
                }
            });
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
                let label = data["$type"].replace('.', ': ');
                AVAILABLE_CONDITIONS.push({ type: data["$type"], label: label });
            }
            const node = addNewConditionNode(x, y);
            node.data.conditionType = data["$type"];
            node.data.value = data.value || "";
            
            const select = node.element.querySelector('select');
            if (select) {
                select.innerHTML = "";
                AVAILABLE_CONDITIONS.forEach(c => {
                    const opt = document.createElement('option');
                    opt.value = c.type;
                    opt.innerText = c.label;
                    select.appendChild(opt);
                });
                select.value = data["$type"];
            }

            const inp = node.element.querySelector('input');
            if (inp) inp.value = data.value || "";

            if (data.trueBranch && data.trueBranch.length > 0) {
                const child = parseAndCreateNode(data.trueBranch[0], x + 300, y - 100);
                if (child) {
                    connections.push({
                        fromPinId: `${node.id}_true`,
                        toPinId: `${child.id}_in`,
                        type: 'true'
                    });
                }
            }

            if (data.falseBranch && data.falseBranch.length > 0) {
                const child = parseAndCreateNode(data.falseBranch[0], x + 300, y + 100);
                if (child) {
                    connections.push({
                        fromPinId: `${node.id}_false`,
                        toPinId: `${child.id}_in`,
                        type: 'false'
                    });
                }
            }
            return node;
        } else {
            // Must be a Command Node
            if (!AVAILABLE_COMMANDS.some(c => c.type === data["$type"])) {
                let label = data["$type"].replace('.', ': ');
                AVAILABLE_COMMANDS.push({ type: data["$type"], label: label });
            }
            const node = addNewCommandNode(x, y);
            node.data.commandType = data["$type"];
            
            const select = node.element.querySelector('select');
            if (select) {
                select.innerHTML = "";
                AVAILABLE_COMMANDS.forEach(cmd => {
                    const opt = document.createElement('option');
                    opt.value = cmd.type;
                    opt.innerText = cmd.label;
                    select.appendChild(opt);
                });
                select.value = data["$type"];
            }

            if (data["$type"] === "general.displayText") {
                node.data.text = data.text || "";
            } else if (data["$type"] === "char.damage") {
                node.data.characterId = data.characterId || "";
                node.data.amount = data.amount || 0;
            } else {
                node.data.text = data.text || data.commentText || data.value || "";
                node.data.characterId = data.characterId || data.roomId || data.objectId || "";
            }

            refreshCommandFields(node);

            // Populate standard custom inputs if not display/damage
            if (data["$type"] !== "general.displayText" && data["$type"] !== "char.damage") {
                const fieldsContainer = document.getElementById(`${node.id}_fields`);
                if (fieldsContainer) {
                    fieldsContainer.innerHTML = "";
                    const inp = document.createElement('input');
                    inp.placeholder = "Parameters / Details";
                    inp.value = node.data.text || "";
                    inp.addEventListener('change', () => { node.data.text = inp.value; });
                    fieldsContainer.appendChild(inp);
                }
            }

            if (data.nextStep) {
                const child = parseAndCreateNode(data.nextStep, x + 300, y);
                if (child) {
                    connections.push({
                        fromPinId: `${node.id}_out`,
                        toPinId: `${child.id}_in`,
                        type: 'exec'
                    });
                }
            }
            return node;
        }
    }
    return null;
}

// Start visual scripting canvas on page load
initGraph();
updateTransform();
