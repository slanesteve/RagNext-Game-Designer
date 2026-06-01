/**
 * Rags Node Visual Graph Editor Engine
 * Handles dragging, panning, drawing connections, dynamic catalog parsing, parameter inputs, and C# serialization.
 */

let nodes = [];
let connections = [];
let selectedNode = null;
let activeActionName = "Visual Action Node";
let activeActionTrigger = "UserClicked";
let activeActionInitallyActive = true;

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

// Dynamic Database Catalogs and reflection lookup maps
let catalogs = {};
let nameToTypeMap = {};
let typeToNameMap = {};
let typeToInputsMap = {};

let AVAILABLE_COMMANDS = [];
let AVAILABLE_CONDITIONS = [];

// Debounced auto-saving on the fly
let autoSaveTimeout = null;
function triggerAutoSave() {
    if (autoSaveTimeout) clearTimeout(autoSaveTimeout);
    autoSaveTimeout = setTimeout(() => {
        saveAndSyncCsharp();
    }, 400); // 400ms debounce
}

// Comprehensive fallback map of friendly names to C# polymorphic type discriminators
const fallbackDiscriminators = {
    "actionaddcustomchoice": "general.addCustomChoice",
    "actionclearcustomchoice": "general.clearCustomChoice",
    "actionremovecustomchoice": "general.removeCustomChoice",
    "characterdisplaydescription": "char.displayDescription",
    "characterdisplayportrait": "char.displayPortrait",
    "charactermovetoroom": "char.moveToRoom",
    "charactermoveinventorytoplayer": "char.moveInventoryToPlayer",
    "charactermovetoobject": "char.moveToObject",
    "charactersetportraitmedia": "char.setPortraitMedia",
    "charactersetactiontoactiveinactive": "char.setActionActive",
    "charactersetcustomproperty": "char.setCustomProperty",
    "charactersetcustompropertyjavascript": "char.setCustomPropertyJs",
    "charactersetdescription": "char.setDescription",
    "charactersetgender": "char.setGender",
    "charactersetdisplayname": "char.setDisplayName",
    "addacomment": "general.addComment",
    "debugtext": "general.debugText",
    "setragsdatawithjavascript": "general.setRagsDataJs",
    "displaytext": "general.displayText",
    "mediadisplaylayeredpicture": "media.displayLayeredPicture",
    "mediadisplaymultimedia": "media.displayMultimedia",
    "mediasetbackgroundmusic": "media.setBackgroundMusic",
    "mediastopbackgroundmusic": "media.stopBackgroundMusic",
    "mediaplaysoundeffect": "media.playSound",
    "mediasetmaincompasspicture": "media.setMainCompassPicture",
    "mediasetupdowncompasspicture": "media.setUpDownCompassPicture",
    "imageaddlayeredimage": "image.addLayeredImage",
    "imageclearlayeredimages": "image.clearLayeredImages",
    "imageremovelayeredimage": "image.removeLayeredImage",
    "imagereplacelayeredimage": "image.replaceLayeredImage",
    "itemdisplaydescription": "item.displayDescription",
    "itemlayeredremove": "item.layeredRemove",
    "itemlayeredwear": "item.layeredWear",
    "itemmovetocharacter": "item.moveToChar",
    "itemmovetoinventory": "item.moveToInventory",
    "itemmoveinsideobject": "item.moveInsideObject",
    "itemmovetoroom": "item.inRoom",
    "playerdisplaydescription": "player.displayDescription",
    "playersetlayeredportrait": "player.setLayeredPortrait",
    "playermoveinventorytocharacter": "player.moveInventoryToChar",
    "playermoveinventorytoroom": "player.moveInventoryToRoom",
    "playermovetoroom": "player.moveTo",
    "playermovetocharacter": "player.moveToChar",
    "playermovetoobject": "player.moveToObject",
    "playersetcustomproperty": "player.setCustomProperty",
    "playersetdescription": "player.setDescription",
    "playersetname": "player.setName",
    "playersetgender": "player.setGender",
    "playersetportraitmedia": "player.setPortraitMedia",
    "roomdisplaydescription": "room.displayDescription",
    "roomdisplaypicture": "room.displayPicture",
    "roommoveitemstoplayer": "room.moveItemsToPlayer",
    "roomsetdescription": "room.setDescription",
    "roomsetpicture": "room.setPicture",
    "roomlockexit": "room.lockExit",
    "roomunlockexit": "room.unlockExit",
    "statusbarsetvisibleinvisible": "ui.setStatusBarVisible",
    "timerexecutetimer": "timer.executeTimer",
    "timerresettimer": "timer.resetTimer",
    "timersetcustomproperty": "timer.setCustomProperty",
    "timersettimertoactiveinactive": "timer.setTimerActive",
    "variabledisplaydata": "var.displayData",
    "variableset": "var.set",
    "variablesetbyuserinput": "general.promptInput",
    "promptplayerinput": "general.promptInput",
    "variablesetnumericrandomly": "var.setRandom",
    "endthegame": "general.endGame",
    "itemopencontainer": "general.openContainer",
    "itemclosecontainer": "general.closeContainer",
    "additionaldatacheck": "general.additionalDataCheck",
    "charactercustompropertycheck": "char.customPropertyCheck",
    "charactergender": "char.gender",
    "characterinroom": "char.inRoom",
    "characterinroomgroup": "char.inRoomGroup",
    "itemcustompropertycheck": "item.customPropertyCheck",
    "itemingroup": "item.inGroup",
    "itemheldbycharacter": "item.heldByChar",
    "itemheldbyplayer": "item.heldByPlayer",
    "iteminobject": "item.inObject",
    "iteminroom": "item.inRoom",
    "iteminroomgroup": "item.inRoomGroup",
    "itemnotheldbyplayer": "item.notHeldByPlayer",
    "itemnotinobject": "item.notInObject",
    "itemstatecheck": "item.stateCheck",
    "playercustompropertycheck": "player.customPropertyCheck",
    "playergender": "player.gender",
    "playerinroom": "player.inRoom",
    "playerinroomgroup": "player.inRoomGroup",
    "playerinsameroomas": "player.sameRoom",
    "playermovingindirection": "player.movingInDirection",
    "roomcustompropertycheck": "room.customPropertyCheck",
    "roomisexitlocked": "room.isExitLocked",
    "timercustompropertycheck": "timer.customPropertyCheck",
    "variablecomparison": "var.compare",
    "variablecomparisontovariable": "var.compareVar",
    "variablecustompropertycheck": "var.customPropertyCheck"
};

const propertyMappings = {
    "Character": ["CharacterId", "characterId", "Character"],
    "Destination Room": ["RoomId", "roomId", "DestinationRoom", "destinationRoom"],
    "Room": ["RoomId", "roomId", "Room"],
    "Media File": ["MediaId", "mediaId", "MediaFile", "mediaFile"],
    "Portrait Media": ["PortraitId", "portraitId", "PortraitMedia", "portraitMedia", "MediaId"],
    "Object": ["ObjectId", "objectId", "Object"],
    "Choice Text": ["ChoiceText", "choiceText", "Text", "text"],
    "Target Variable": ["VariableName", "variableName", "Name", "name", "TargetVariable", "targetVariable"],
    "Variable": ["VariableName", "variableName", "Name", "name", "Variable", "variable"],
    "Text": ["Text", "text"],
    "Amount": ["Amount", "amount"],
    "Direction": ["Direction", "direction"]
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

function copyNodeAtCursor() {
    if (selectedNode) {
        const nodeJson = buildNodeJsonWithoutNext(selectedNode);
        jsActionClipboard = JSON.parse(JSON.stringify(nodeJson));
    }
    hideContextMenu();
}

function pasteNodeAtCursor() {
    if (!jsActionClipboard) return;
    const data = JSON.parse(JSON.stringify(jsActionClipboard));
    
    // Position pasted element at right-clicked cursor coordinate
    data.X = contextCursorX;
    data.Y = contextCursorY;
    
    // Assign clean unique IDs recursively so pasting doesn't share instance mappings
    if (data["dialogueId"]) {
        data.dialogueId = 'dialogue_' + Date.now() + '_' + Math.random().toString(36).substr(2, 5);
    }
    
    parseAndCreateNode(data, contextCursorX, contextCursorY);
    redrawConnections();
    triggerAutoSave();
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
            deselectAllNodes();
            clickedNode.element.classList.add('selected');
            selectedNode = clickedNode;
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

    let colorRegex = /&lt;color=(#[a-f0-9]{6})&gt;(.*?)&lt;\/color&gt;/gi;
    html = html.replace(colorRegex, '<span style="color: $1;">$2</span>');

    let markRegex = /&lt;mark=(#[a-f0-9]{8}|#[a-f0-9]{6})&gt;(.*?)&lt;\/mark&gt;/gi;
    html = html.replace(markRegex, '<span style="background-color: $2; padding: 2px 4px; border-radius: 4px;">$3</span>');
    html = html.replace(/&lt;mark=(#[a-f0-9]{6}|#[a-f0-9]{8})&gt;(.*?)&lt;\/mark&gt;/gi, '<span style="background-color: $1; padding: 2px 4px; border-radius: 4px;">$2</span>');

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
    btnB.onclick = (e) => { e.preventDefault(); wrapSelection(textarea, '<b>', '</b>', previewElement); };
    toolbar.appendChild(btnB);

    const btnI = document.createElement('button');
    btnI.innerText = 'I';
    btnI.className = 'btn-format';
    btnI.style.fontStyle = 'italic';
    btnI.onclick = (e) => { e.preventDefault(); wrapSelection(textarea, '<i>', '</i>', previewElement); };
    toolbar.appendChild(btnI);

    const btnU = document.createElement('button');
    btnU.innerText = 'U';
    btnU.className = 'btn-format';
    btnU.style.textDecoration = 'underline';
    btnU.onclick = (e) => { e.preventDefault(); wrapSelection(textarea, '<u>', '</u>', previewElement); };
    toolbar.appendChild(btnU);

    const btnColor = document.createElement('button');
    btnColor.innerHTML = '🎨 Color';
    btnColor.className = 'btn-format';
    btnColor.style.fontSize = '10px';
    btnColor.onclick = (e) => {
        e.preventDefault();
        showColorDropdown(btnColor, textarea, previewElement);
    };
    toolbar.appendChild(btnColor);

    const btnHighlight = document.createElement('button');
    btnHighlight.innerHTML = '🖊️ Highlight';
    btnHighlight.className = 'btn-format';
    btnHighlight.style.fontSize = '10px';
    btnHighlight.onclick = (e) => { 
        e.preventDefault(); 
        showHighlightDropdown(btnHighlight, textarea, previewElement);
    };
    toolbar.appendChild(btnHighlight);

    const btnClear = document.createElement('button');
    btnClear.innerHTML = '✕ Clear';
    btnClear.className = 'btn-format';
    btnClear.style.fontSize = '10px';
    btnClear.onclick = (e) => { 
        e.preventDefault(); 
        clearSelectionFormatting(textarea, previewElement); 
    };
    toolbar.appendChild(btnClear);

    // Glowing ✨ AI dialogue trigger calling native C# DI chat service co-author bridge
    const btnAI = document.createElement('button');
    btnAI.innerHTML = '✨ AI dialogue';
    btnAI.className = 'btn-format ai-glow';
    btnAI.style.marginLeft = 'auto';
    btnAI.onclick = (e) => {
        e.preventDefault();
        const currentText = textarea.value;
        window.location.href = "rags-action://ai?nodeId=" + node.id + "&fieldName=" + fieldName + "&currentText=" + encodeURIComponent(currentText);
    };
    toolbar.appendChild(btnAI);

    return toolbar;
}

function createLivePreviewContainer(textarea) {
    const container = document.createElement('div');
    container.className = 'live-preview-container';

    const header = document.createElement('div');
    header.className = 'live-preview-header';
    header.innerHTML = '<span>👁️ Live Preview</span>';
    container.appendChild(header);

    const body = document.createElement('div');
    body.className = 'live-preview-body';
    body.innerHTML = renderRichTextPreview(textarea.value);
    container.appendChild(body);

    textarea.addEventListener('input', () => updateLivePreview(textarea, body));
    textarea.addEventListener('change', () => updateLivePreview(textarea, body));

    return { container, body };
}

// Node Engine Creation Methods
function createBaseNode(id, type, title, x, y) {
    const el = document.createElement('div');
    el.className = `node ${type}`;
    el.style.left = `${x}px`;
    el.style.top = `${y}px`;
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

function makeDraggable(el) {
    let offsetOffsetX = 0;
    let offsetOffsetY = 0;

    el.addEventListener('mousedown', (e) => {
        if (e.target.tagName === 'INPUT' || e.target.tagName === 'TEXTAREA' || e.target.tagName === 'SELECT' || e.target.classList.contains('pin') || e.target.classList.contains('node-delete') || e.target.classList.contains('btn-format')) {
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

// Create the permanently fixed Start node
function createStartNode() {
    let startNode = nodes.find(n => n.id === 'start');
    if (startNode) return startNode;

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
        { val: "OnRoomTick", label: "On Room Tick" },
        { val: "OnInteract", label: "On Interact" },
        { val: "OnCharacterTick", label: "On Character Tick" },
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

    triggerSelect.addEventListener('change', () => {
        activeActionTrigger = triggerSelect.value;
        triggerAutoSave();
    });
    node.bodyElement.appendChild(triggerSelect);

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

    const preview = createLivePreviewContainer(txt);
    node.bodyElement.appendChild(createFormattingToolbar(txt, preview.body, 'characterLines', node));
    node.bodyElement.appendChild(txt);
    node.bodyElement.appendChild(preview.container);

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
        refreshCommandFields(node); 
        triggerAutoSave();
    });
    node.bodyElement.appendChild(select);

    const fieldContainer = document.createElement('div');
    fieldContainer.id = `${id}_fields`;
    node.bodyElement.appendChild(fieldContainer);

    if (AVAILABLE_COMMANDS.length > 0) {
        node.data.commandType = AVAILABLE_COMMANDS[0].type;
        refreshCommandFields(node);
    }

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
    select.addEventListener('change', () => { 
        node.data.conditionType = select.value; 
        refreshCommandFields(node); 
        triggerAutoSave();
    });
    node.bodyElement.appendChild(select);

    const fieldContainer = document.createElement('div');
    fieldContainer.id = `${id}_fields`;
    node.bodyElement.appendChild(fieldContainer);

    if (AVAILABLE_CONDITIONS.length > 0) {
        node.data.conditionType = AVAILABLE_CONDITIONS[0].type;
        refreshCommandFields(node);
    }

    return node;
}

function refreshCommandFields(node) {
    const fieldsContainer = document.getElementById(`${node.id}_fields`);
    if (!fieldsContainer) return;
    fieldsContainer.innerHTML = "";

    const type = node.type === 'command' ? node.data.commandType : node.data.conditionType;
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
        const row = document.createElement('div');
        row.className = 'field-row';
        row.style.marginBottom = '6px';
        row.style.display = 'flex';
        row.style.flexDirection = 'column';
        row.style.gap = '2px';

        const label = document.createElement('label');
        label.innerText = inputSchema.label + ":";
        label.style.fontSize = '10px';
        label.style.color = 'var(--text-muted)';
        row.appendChild(label);

        let inputElement;
        const initialVal = getPropertyValue(node.data, inputSchema.label);

        if (inputSchema.controlType === 'ComboBox' || inputSchema.dataType === 'Room' || inputSchema.dataType === 'GameObject' || inputSchema.dataType === 'Character' || inputSchema.dataType === 'Variable' || inputSchema.dataType === 'Media' || inputSchema.dataType === 'Function' || inputSchema.dataType === 'Timer' || inputSchema.dataType === 'Item') {
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
            else if (inputSchema.dataType === 'GameObject' || inputSchema.dataType === 'Item') optionsList = catalogs.GameObjects || [];
            else if (inputSchema.dataType === 'Character') optionsList = catalogs.Characters || [];
            else if (inputSchema.dataType === 'Variable') optionsList = catalogs.Variables || [];
            else if (inputSchema.dataType === 'Media') optionsList = catalogs.Media || [];
            else if (inputSchema.dataType === 'Function') optionsList = catalogs.Functions || [];
            else if (inputSchema.dataType === 'Timer') optionsList = catalogs.Timers || [];

            optionsList.forEach(opt => {
                const o = document.createElement('option');
                if (inputSchema.dataType === 'Variable') {
                    o.value = opt.Name;
                    o.innerText = opt.Name;
                } else {
                    o.value = opt.Id;
                    o.innerText = opt.Name;
                }
                pickerSelect.appendChild(o);
            });

            const textInput = document.createElement('input');
            textInput.type = 'text';
            textInput.placeholder = `Enter expression / {this.name}...`;
            textInput.style.width = "100%";

            const existsInOptions = optionsList.some(opt => 
                inputSchema.dataType === 'Variable' ? opt.Name === initialVal : opt.Id === initialVal
            );
            let isExprMode = (initialVal && (initialVal.includes('{') || initialVal.includes('}') || !existsInOptions));

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
                    textInput.value = pickerSelect.value;
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
        } else if (inputSchema.controlType === 'TextArea' || inputSchema.label.toLowerCase().includes('text') || inputSchema.label.toLowerCase().includes('lines') || inputSchema.label.toLowerCase().includes('description') || inputSchema.label.toLowerCase().includes('dialogue')) {
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

            const preview = createLivePreviewContainer(inputElement);
            row.appendChild(createFormattingToolbar(inputElement, preview.body, inputSchema.label, node));
            row.appendChild(inputElement);
            row.appendChild(preview.container);
        } else {
            // Standard Text / Input field
            inputElement = document.createElement('input');
            inputElement.type = inputSchema.dataType === 'Integer' || inputSchema.dataType === 'Number' ? 'number' : 'text';
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
            row.appendChild(inputElement);
        }

        if (inputSchema.controlType !== 'TextArea' && !inputSchema.label.toLowerCase().includes('text') && !inputSchema.label.toLowerCase().includes('lines') && !inputSchema.label.toLowerCase().includes('description') && !inputSchema.label.toLowerCase().includes('dialogue')) {
            row.appendChild(inputElement);
        }

        fieldsContainer.appendChild(row);
        node.inputs.push({ label: inputSchema.label, element: inputElement });
    });
}

// Node Position Context shortcuts
function addNewDialogueNodeAtCursor() { addNewDialogueNode(contextCursorX, contextCursorY); triggerAutoSave(); hideContextMenu(); }
function addNewCommandNodeAtCursor() { addNewCommandNode(contextCursorX, contextCursorY); triggerAutoSave(); hideContextMenu(); }
function addNewConditionNodeAtCursor() { addNewConditionNode(contextCursorX, contextCursorY); triggerAutoSave(); hideContextMenu(); }

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

// Bidirectional Sync back to C#
function saveAndSyncCsharp() {
    const actionDto = serializeGraph();
    const json = JSON.stringify(actionDto);
    const base64 = btoa(unescape(encodeURIComponent(json)));
    window.location.href = "rags-action://sync?data=" + base64;
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
            "X": node.x,
            "Y": node.y,
            "Width": node.width || null,
            "Height": node.height || null
        };
    } else if (node.type === 'command') {
        const commandJson = {
            "$type": node.data.commandType,
            "Label": node.data.label || "",
            "X": node.x,
            "Y": node.y,
            "Width": node.width || null,
            "Height": node.height || null
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
                    primaryCsharpProp = 'ObjectId';
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
            "Label": node.data.label || "",
            "trueBranch": trueNode ? buildFlatSequence(trueNode) : [],
            "falseBranch": falseNode ? buildFlatSequence(falseNode) : [],
            "X": node.x,
            "Y": node.y,
            "Width": node.width || null,
            "Height": node.height || null
        };

        if (node.inputs) {
            node.inputs.forEach(inp => {
                let val = node.data[inp.label];
                if (val === undefined) val = "";
                
                // Map to primary C# property name
                const aliases = propertyMappings[inp.label] || [];
                const primaryCsharpProp = aliases[0] || inp.label;
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
        typesMap.forEach(tm => {
            nameToTypeMap[tm.TypeName] = tm.Discriminator;
            nameToTypeMap[normalize(tm.TypeName)] = tm.Discriminator;
            typeToNameMap[tm.Discriminator] = tm.TypeName;
        });
    }

    // Map Inputs Schema
    if (commandsDb && commandsDb.commands) {
        commandsDb.commands.forEach(cmd => {
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
        conditionsDb.conditions.forEach(cond => {
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
        commandsDb.commands.forEach(cmd => {
            let type = nameToTypeMap[cmd.name] || nameToTypeMap[normalize(cmd.name)] || fallbackDiscriminators[normalize(cmd.name)];
            if (!type) {
                const combined = cmd.category + ": " + cmd.name;
                type = nameToTypeMap[combined] || nameToTypeMap[normalize(combined)] || fallbackDiscriminators[normalize(combined)];
            }
            if (type) {
                AVAILABLE_COMMANDS.push({ type: type, label: cmd.name, category: cmd.category });
            }
        });
    }
    AVAILABLE_COMMANDS.sort((a, b) => a.label.localeCompare(b.label));

    AVAILABLE_CONDITIONS = [];
    if (conditionsDb && conditionsDb.conditions) {
        conditionsDb.conditions.forEach(cond => {
            let type = nameToTypeMap[cond.name] || nameToTypeMap[normalize(cond.name)] || fallbackDiscriminators[normalize(cond.name)];
            if (!type) {
                const combined = cond.category + ": " + cond.name;
                type = nameToTypeMap[combined] || nameToTypeMap[normalize(combined)] || fallbackDiscriminators[normalize(combined)];
            }
            if (type) {
                AVAILABLE_CONDITIONS.push({ type: type, label: cond.name, category: cond.category });
            }
        });
    }
    AVAILABLE_CONDITIONS.sort((a, b) => a.label.localeCompare(b.label));

    // Dynamic header title update
    activeActionName = actionJson?.Name || "Visual Action Node";
    activeActionTrigger = actionJson?.Trigger || "UserClicked";
    activeActionInitallyActive = (actionJson?.InitallyActive !== undefined) ? actionJson.InitallyActive : true;

    const titleEl = document.getElementById("editor-title");
    if (titleEl) {
        titleEl.innerText = "Editing Action: " + activeActionName;
    }

    // Always create the permanent Start Node at (50, 150)
    createStartNode();

    if (!actionJson || !actionJson.Nodes || actionJson.Nodes.length === 0) {
        updateTransform();
        return;
    }

    // Render the sequential node-graph connected list starting from Start Node
    const firstNode = parseFlatSequence(actionJson.Nodes, 250, 150);
    if (firstNode) {
        connections.push({
            fromPinId: "start_out",
            toPinId: `${firstNode.id}_in`,
            type: 'exec'
        });
    }

    updateTransform();
};

// Generate a sequence of nodes drawn connected sequentially
function parseFlatSequence(nodeList, x, y) {
    if (!nodeList || nodeList.length === 0) return null;

    let firstNode = null;
    let prevNode = null;

    nodeList.forEach((stepData, idx) => {
        const currNode = parseAndCreateNode(stepData, stepData.X || x + idx * 360, stepData.Y || y);
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

    if (data["$type"] === "general.startDialogue") {
        const node = addNewDialogueNode(x, y);
        node.data.characterLines = data.CharacterLines !== undefined ? data.CharacterLines : (data.characterLines || "");
        const textarea = node.element.querySelector('textarea');
        if (textarea) textarea.value = node.data.characterLines;

        const previewBody = node.element.querySelector('.live-preview-body');
        updateLivePreview(textarea, previewBody);

        const dialogueChoices = data.Choices || data.choices;
        if (dialogueChoices) {
            dialogueChoices.forEach((choice, idx) => {
                const choiceId = Date.now() + idx;
                const container = document.getElementById(`${node.id}_choices_container`);
                const choiceText = choice.Text !== undefined ? choice.Text : (choice.text || "");
                addDialogueChoiceRow(node, container, choiceText, choiceId);
                
                const choiceCmds = choice.Commands || choice.commands;
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
            if (data.Width) {
                node.width = data.Width;
                node.element.style.width = `${data.Width}px`;
            }
            if (data.Height) {
                node.height = data.Height;
                node.element.style.height = `${data.Height}px`;
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
                if (key !== "trueBranch" && key !== "falseBranch") {
                    node.data[key] = data[key];
                }
            });

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

            refreshCommandFields(node);

            if (data.trueBranch && data.trueBranch.length > 0) {
                const child = parseFlatSequence(data.trueBranch, x + 350, y - 120);
                if (child) {
                    connections.push({
                        fromPinId: `${node.id}_true`,
                        toPinId: `${child.id}_in`,
                        type: 'true'
                    });
                }
            }

            if (data.falseBranch && data.falseBranch.length > 0) {
                const child = parseFlatSequence(data.falseBranch, x + 350, y + 120);
                if (child) {
                    connections.push({
                        fromPinId: `${node.id}_false`,
                        toPinId: `${child.id}_in`,
                        type: 'false'
                    });
                }
            }
            if (node) {
                if (data.Width) {
                    node.width = data.Width;
                    node.element.style.width = `${data.Width}px`;
                }
                if (data.Height) {
                    node.height = data.Height;
                    node.element.style.height = `${data.Height}px`;
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
                }
            });

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

            refreshCommandFields(node);
            if (node) {
                if (data.Width) {
                    node.width = data.Width;
                    node.element.style.width = `${data.Width}px`;
                }
                if (data.Height) {
                    node.height = data.Height;
                    node.element.style.height = `${data.Height}px`;
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
        triggerAutoSave();
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

function getAutocompleteSuggestions(triggerChar) {
    const list = [];
    if (triggerChar === '{') {
        // Current Object Property (this.*)
        list.push({ token: "this.Name", typeName: "Current Object Property", desc: "Name of this object." });
        list.push({ token: "this.Description", typeName: "Current Object Property", desc: "Description of this object." });
        list.push({ token: "this.portrait", typeName: "Current Object Property", desc: "Portrait or image path." });
        
        if (catalogs.GameObjects && catalogs.GameObjects.length > 0) {
            list.push({ token: "this.attributes.health", typeName: "Current Object Attribute", desc: "Custom health attribute." });
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
                list.push({ token: `variables.${v.Name}`, typeName: "Global Variable", desc: `State variable. Current: ${v.Value || '0'}` });
            });
        }

        // Characters
        if (catalogs.Characters) {
            catalogs.Characters.forEach(c => {
                const nameClean = c.Name.replace(/\s+/g, "");
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
                list.push({ token: `objects.${nameClean}.Name`, typeName: "Object Property", desc: `Name of object '${o.Name}'.` });
                list.push({ token: `objects.${nameClean}.Description`, typeName: "Object Property", desc: `Description of object '${o.Name}'.` });
                list.push({ token: `objects.${nameClean}.portrait`, typeName: "Object Property", desc: `Portrait of object '${o.Name}'.` });
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
            position: absolute;
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

    const rect = activeAutocomplete.targetInput.getBoundingClientRect();
    popup.style.left = `${rect.left + window.scrollX}px`;
    
    const popupHeight = Math.min(220, filtered.length * 48 + 8);
    if (rect.bottom + popupHeight > window.innerHeight) {
        popup.style.top = `${rect.top + window.scrollY - popupHeight - 4}px`;
    } else {
        popup.style.top = `${rect.bottom + window.scrollY + 4}px`;
    }
    
    popup.style.display = 'block';
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

