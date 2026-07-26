(function() {
  let zoom = 1.0;
  let panX = 0;
  let panY = 0;
  let isDragging = false;
  let startX = 0;
  let startY = 0;

  // Relative offsets for direction names
  const getVector = (dir) => {
    const vectors = {
      north: { dx: 0, dy: -140, dz: 0 },
      south: { dx: 0, dy: 140, dz: 0 },
      east: { dx: 180, dy: 0, dz: 0 },
      west: { dx: -180, dy: 0, dz: 0 },
      northeast: { dx: 130, dy: -100, dz: 0 },
      northwest: { dx: -130, dy: -100, dz: 0 },
      southeast: { dx: 130, dy: 100, dz: 0 },
      southwest: { dx: -130, dy: 100, dz: 0 },
      up: { dx: 0, dy: 0, dz: 1 },
      down: { dx: 0, dy: 0, dz: -1 }
    };
    if (vectors[dir]) return vectors[dir];
    if (dir === "in") return { dx: 375, dy: 0, dz: 0 };
    if (dir === "out") return { dx: -375, dy: 0, dz: 0 };
    return { dx: 120, dy: 120, dz: 0 };
  };

  window.ShowMapOverlay = function(configJson) {
    const config = JSON.parse(configJson);
    const overlay = document.getElementById("map-overlay");
    if (!overlay) return;

    // Apply dynamic map title
    const titleEl = overlay.querySelector(".map-title");
    if (titleEl && config.mapTitle) {
      titleEl.textContent = config.mapTitle.toUpperCase();
    }

    // Apply theme variable styles
    if (config.theme) {
      document.documentElement.style.setProperty('--primary-bg', config.theme.primaryBgColor || '#1e1e24');
      document.documentElement.style.setProperty('--text-color', config.theme.textMainColor || '#ffffff');
      document.documentElement.style.setProperty('--accent-color', config.theme.borderAccentColor || '#4a4a5a');
      document.documentElement.style.setProperty('--font-family', config.theme.fontName || 'Outfit');
    }

    // Apply Map Style Class (Clean, SciFi, Fantasy, Custom)
    overlay.className = ""; // clear previous
    const styleClass = "map-style-" + (config.mapStyle || "clean").toLowerCase();
    overlay.classList.add(styleClass);

    const viewport = overlay.querySelector(".map-viewport");
    if (viewport) {
      viewport.style.backgroundImage = "";
      viewport.style.backgroundSize = "";
      viewport.style.backgroundPosition = "";
      if (config.customBackground && config.customBackground.toLowerCase() !== "none" && config.customBackground.toLowerCase() !== "<none>") {
        let bgPath = config.customBackground;
        if (!bgPath.startsWith("http") && !bgPath.startsWith("/")) {
          if (bgPath.startsWith("Assets/")) {
            bgPath = "StreamingAssets/" + bgPath;
          } else {
            bgPath = "StreamingAssets/Assets/" + bgPath;
          }
        }
        viewport.style.backgroundImage = `url('${bgPath}')`;
        viewport.style.backgroundSize = "cover";
        viewport.style.backgroundPosition = "center";
      }
    }

    // Reset zoom and pan
    zoom = 1.0;
    panX = 0;
    panY = 0;
    updateZoomTransform();

    overlay.style.display = "flex";

    // Build the Visited Graph & Traversal
    const roomsMap = {};
    config.rooms.forEach(r => {
      roomsMap[r.id] = r;
    });

    const activeRoomId = config.activeRoomId;
    const coords = {};
    coords[activeRoomId] = { x: 400, y: 300, z: 0 };

    const queue = [activeRoomId];
    const visited = new Set([activeRoomId]);
    const distances = {};
    distances[activeRoomId] = 0;

    // BFS Traversal to layout coordinates & respect 3-Ring Radius limit
    while (queue.length > 0) {
      const currentId = queue.shift();
      const currentRoom = roomsMap[currentId];
      const currentDist = distances[currentId];

      if (!currentRoom || currentDist >= 3) continue;

      const currentCoord = coords[currentId];

      for (let exitName in currentRoom.exits) {
        const destId = currentRoom.exits[exitName];
        if (!destId || !roomsMap[destId]) continue; // Fog of war check (unvisited rooms not in roomsMap)

        const dirKey = exitName.toLowerCase();
        const vector = DIR_VECTORS[dirKey] || { dx: 120, dy: 120, dz: 0 };

        if (!coords[destId]) {
          coords[destId] = {
            x: currentCoord.x + vector.dx,
            y: currentCoord.y + vector.dy,
            z: currentCoord.z + vector.dz
          };
          distances[destId] = currentDist + 1;
          visited.add(destId);
          queue.push(destId);
        }
      }
    }

    // Group rooms by Floor (Z-index)
    const floors = {};
    for (let id in coords) {
      const c = coords[id];
      if (!floors[c.z]) floors[c.z] = [];
      floors[c.z].push({ id: id, x: c.x, y: c.y, name: roomsMap[id].name, isVisited: roomsMap[id].isVisited !== false });
    }

    // SVG Render inside Map Zoom Container
    const zoomContainer = document.getElementById("map-zoom-container");
    zoomContainer.innerHTML = "";

    // Generate Floor Layers
    for (let z in floors) {
      const floorLayer = document.createElement("div");
      floorLayer.className = "floor-layer";

      // Color-code the floor boundaries (3D grid box outline)
      if (z > 0) {
        floorLayer.style.borderColor = "rgba(6, 182, 212, 0.65)"; // Sky Blue (Cyan)
        floorLayer.style.borderWidth = "3px";
      } else if (z < 0) {
        floorLayer.style.borderColor = "rgba(217, 70, 239, 0.65)"; // Purple/Magenta
        floorLayer.style.borderWidth = "3px";
      } else {
        floorLayer.style.borderColor = "rgba(249, 115, 22, 0.55)"; // Amber/Orange
        floorLayer.style.borderWidth = "3px";
      }

      // 3D vertical stacked offsets for holographic deck layout
      const verticalOffset = z * -150;
      floorLayer.style.transform = `translateZ(${verticalOffset}px) rotateX(60deg) rotateZ(-45deg)`;

      // Build SVG for this floor
      const svg = document.createElementNS("http://www.w3.org/2000/svg", "svg");
      svg.setAttribute("width", "100%");
      svg.setAttribute("height", "100%");
      svg.setAttribute("viewBox", "0 0 800 600");
      svg.style.overflow = "visible";

      // Draw Floor Level label
      const floorLabel = document.createElementNS("http://www.w3.org/2000/svg", "text");
      floorLabel.setAttribute("x", "40");
      floorLabel.setAttribute("y", "50");
      floorLabel.setAttribute("fill", z > 0 ? "rgba(6, 182, 212, 0.7)" : (z < 0 ? "rgba(217, 70, 239, 0.7)" : "rgba(249, 115, 22, 0.6)"));
      floorLabel.setAttribute("font-size", "14px");
      floorLabel.setAttribute("font-weight", "bold");
      floorLabel.textContent = `LEVEL ${parseInt(z) + 1}`;
      svg.appendChild(floorLabel);

      // Draw Connections (Paths)
      const connectionsDrawn = new Set();
      floors[z].forEach(room => {
        const rData = roomsMap[room.id];
        if (!rData) return;

        for (let exitName in rData.exits) {
          const destId = rData.exits[exitName];
          if (!destId || !coords[destId] || coords[destId].z != z) continue; // multi-floor draw is vertical indicators

          const key = [room.id, destId].sort().join("-");
          if (connectionsDrawn.has(key)) continue;
          connectionsDrawn.add(key);

          const destCoord = coords[destId];
          
          // Case-insensitive lock check
          let isLocked = false;
          if (rData.lockedExits) {
            const matchKey = Object.keys(rData.lockedExits).find(k => k.toLowerCase() === exitName.toLowerCase());
            if (matchKey) {
              isLocked = !!rData.lockedExits[matchKey];
            }
          }

          const line = document.createElementNS("http://www.w3.org/2000/svg", "line");
          line.setAttribute("x1", room.x);
          line.setAttribute("y1", room.y);
          line.setAttribute("x2", destCoord.x);
          line.setAttribute("y2", destCoord.y);
          line.setAttribute("stroke", isLocked ? "#ef4444" : "var(--accent-color)");
          line.setAttribute("stroke-width", "3");
          line.className.baseVal = "map-link" + (isLocked ? " locked" : "");
          svg.appendChild(line);

          const midX = (room.x + destCoord.x) / 2;
          const midY = (room.y + destCoord.y) / 2;

          // Draw padlock icon at the midpoint if locked
          if (isLocked) {
            const lockLabel = document.createElementNS("http://www.w3.org/2000/svg", "text");
            lockLabel.setAttribute("x", midX);
            lockLabel.setAttribute("y", midY + 4);
            lockLabel.setAttribute("text-anchor", "middle");
            lockLabel.setAttribute("font-size", "14px");
            lockLabel.textContent = "🔒";
            svg.appendChild(lockLabel);
          }

          // Check if path is one-way
          let isOneWay = true;
          let reverseDir = "";
          const destRoom = roomsMap[destId];
          if (destRoom && destRoom.exits) {
            for (let [dKey, dVal] of Object.entries(destRoom.exits)) {
              if (dVal === room.id) {
                isOneWay = false;
                reverseDir = dKey;
                break;
              }
            }
          }

          // Draw vertical up/down or horizontal in/out tags
          if (room.z !== coords[destId].z) {
            const verticalLabel = document.createElementNS("http://www.w3.org/2000/svg", "text");
            verticalLabel.setAttribute("x", midX);
            verticalLabel.setAttribute("y", midY + 4);
            verticalLabel.setAttribute("text-anchor", "middle");
            verticalLabel.setAttribute("font-size", "12px");
            verticalLabel.setAttribute("fill", "#f97316"); // Matching Amber/Orange
            verticalLabel.setAttribute("font-weight", "bold");
            let tagText = coords[destId].z > room.z ? "▲ UP" : "▼ DOWN";
            if (!isOneWay && reverseDir && (reverseDir.toLowerCase() === "up" || reverseDir.toLowerCase() === "down" || reverseDir.toLowerCase() === "u" || reverseDir.toLowerCase() === "d")) {
              tagText = "▲ UP / ▼ DOWN";
            }
            verticalLabel.textContent = tagText;
            svg.appendChild(verticalLabel);
          } else if (exitName.toLowerCase() === "in" || exitName.toLowerCase() === "out") {
            const inLabel = document.createElementNS("http://www.w3.org/2000/svg", "text");
            inLabel.setAttribute("x", midX);
            inLabel.setAttribute("y", midY + 4);
            inLabel.setAttribute("text-anchor", "middle");
            inLabel.setAttribute("font-size", "12px");
            inLabel.setAttribute("fill", "#f97316"); // Amber/Orange
            inLabel.setAttribute("font-weight", "bold");
            let tagText = exitName.toLowerCase() === "in" ? "IN ▶" : "◀ OUT";
            if (!isOneWay && reverseDir && (reverseDir.toLowerCase() === "in" || reverseDir.toLowerCase() === "out")) {
              tagText = "◀ OUT / IN ▶";
            }
            inLabel.textContent = tagText;
            svg.appendChild(inLabel);
          }

          if (isOneWay) {
            const arrowX = room.x + (destCoord.x - room.x) * 0.65;
            const arrowY = room.y + (destCoord.y - room.y) * 0.65;
            const angle = Math.atan2(destCoord.y - room.y, destCoord.x - room.x) * 180 / Math.PI;

            const arrow = document.createElementNS("http://www.w3.org/2000/svg", "text");
            arrow.setAttribute("x", arrowX);
            arrow.setAttribute("y", arrowY + 4);
            arrow.setAttribute("text-anchor", "middle");
            arrow.setAttribute("font-size", "12px");
            arrow.setAttribute("fill", isLocked ? "#ef4444" : "var(--accent-color)");
            arrow.setAttribute("transform", `rotate(${angle}, ${arrowX}, ${arrowY})`);
            arrow.textContent = "➔";
            svg.appendChild(arrow);
          }
        }
      });

      // Draw Rooms (Nodes)
      floors[z].forEach(room => {
        const isActive = (room.id === activeRoomId);
        
        // Group element for room node
        const g = document.createElementNS("http://www.w3.org/2000/svg", "g");
        g.className.baseVal = "map-node" + (isActive ? " node-pulse" : "");

        const isVisited = (room.isVisited !== false);

        const circle = document.createElementNS("http://www.w3.org/2000/svg", "circle");
        circle.setAttribute("cx", room.x);
        circle.setAttribute("cy", room.y);
        circle.setAttribute("r", isActive ? "26" : "20");
        let floorAccent = "var(--accent-color)";
        if (isVisited) {
          if (room.z > 0) {
            floorAccent = "#06b6d4"; // Sky Blue
          } else if (room.z < 0) {
            floorAccent = "#d946ef"; // Purple/Magenta
          } else {
            floorAccent = "#f97316"; // Amber/Orange
          }
        } else {
          floorAccent = "#3a3d46"; // Unvisited
        }

        circle.setAttribute("fill", isActive ? "#38bdf8" : (isVisited ? "var(--primary-bg)" : "#15171c"));
        circle.setAttribute("stroke", isActive ? "#ffffff" : floorAccent);
        circle.setAttribute("stroke-width", isActive ? "5" : "3");
        if (!isVisited) {
          circle.setAttribute("opacity", "0.6");
        }
        g.appendChild(circle);

        // Name text
        const text = document.createElementNS("http://www.w3.org/2000/svg", "text");
        text.setAttribute("x", room.x);
        text.setAttribute("y", room.y - 30);
        text.setAttribute("text-anchor", "middle");
        text.setAttribute("fill", isVisited ? "var(--text-color)" : "rgba(255,255,255,0.4)");
        text.setAttribute("font-size", "14px");
        text.setAttribute("font-weight", isActive ? "bold" : "normal");
        text.textContent = room.name;
        g.appendChild(text);

        // Vertical floor transition markers (Stairs / Up / Down)
        const rData = roomsMap[room.id];
        let hasVerticalTransition = false;
        if (rData && rData.exits) {
          for (let exitName in rData.exits) {
            if (exitName.toLowerCase() === "up" || exitName.toLowerCase() === "down") {
              hasVerticalTransition = true;
            }
          }
        }
        if (hasVerticalTransition) {
          const stairsText = document.createElementNS("http://www.w3.org/2000/svg", "text");
          stairsText.setAttribute("x", room.x);
          stairsText.setAttribute("y", room.y + 4);
          stairsText.setAttribute("text-anchor", "middle");
          stairsText.setAttribute("fill", "var(--text-color)");
          stairsText.setAttribute("font-size", "10px");
          stairsText.textContent = "📶";
          g.appendChild(stairsText);
        }

        // Padlock symbol overlay for rooms with locks
        let hasLocks = false;
        if (rData && rData.lockedExits) {
          for (let exitName in rData.lockedExits) {
            if (rData.lockedExits[exitName]) hasLocks = true;
          }
        }
        if (hasLocks) {
          const lockText = document.createElementNS("http://www.w3.org/2000/svg", "text");
          lockText.setAttribute("x", room.x + 12);
          lockText.setAttribute("y", room.y + 16);
          lockText.setAttribute("text-anchor", "middle");
          lockText.setAttribute("fill", "#ef4444");
          lockText.setAttribute("font-size", "10px");
          lockText.textContent = "🔒";
          g.appendChild(lockText);
        }

        svg.appendChild(g);
      });

      floorLayer.appendChild(svg);
      zoomContainer.appendChild(floorLayer);
    }
  };

  window.HideMapOverlay = function() {
    const overlay = document.getElementById("map-overlay");
    if (overlay) {
      overlay.style.display = "none";
    }
    // Call Unity to resume
    if (window.unityInstance) {
      window.unityInstance.SendMessage('UIManager', 'OnMapClosed');
    }
  };

  function updateZoomTransform() {
    const zoomContainer = document.getElementById("map-zoom-container");
    if (zoomContainer) {
      zoomContainer.style.transform = `translate(${panX}px, ${panY}px) scale(${zoom})`;
    }
  }

  // Bind Pan & Zoom Event Handlers
  document.addEventListener("DOMContentLoaded", () => {
    const viewport = document.getElementById("map-viewport");
    const closeBtn = document.getElementById("map-close-btn");

    if (closeBtn) {
      closeBtn.addEventListener("click", window.HideMapOverlay);
    }

    if (viewport) {
      viewport.addEventListener("wheel", (e) => {
        e.preventDefault();
        const zoomSpeed = 0.1;
        if (e.deltaY < 0) {
          zoom = Math.min(zoom + zoomSpeed, 2.5);
        } else {
          zoom = Math.max(zoom - zoomSpeed, 0.5);
        }
        updateZoomTransform();
      });

      viewport.addEventListener("mousedown", (e) => {
        isDragging = true;
        startX = e.clientX - panX;
        startY = e.clientY - panY;
      });

      window.addEventListener("mousemove", (e) => {
        if (!isDragging) return;
        panX = e.clientX - startX;
        panY = e.clientY - startY;
        updateZoomTransform();
      });

      window.addEventListener("mouseup", () => {
        isDragging = false;
      });
    }
  });
})();
