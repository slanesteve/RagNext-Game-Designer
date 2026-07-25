const fs = require('fs');
const path = require('path');
const vm = require('vm');

const baseName = process.argv[2] || 'TheBet';
const inputPath = `C:\\Users\\steve\\source\\repos\\RagNext\\${baseName}.js`;
const outputPath = `C:\\Users\\steve\\source\\repos\\RagNext\\${baseName}_raw.json`;

console.log(`Extracting raw data from: ${inputPath}`);

if (!fs.existsSync(inputPath)) {
    console.error(`Error: File not found at ${inputPath}`);
    process.exit(1);
}

const jsContent = fs.readFileSync(inputPath, 'utf8');

// Mock all possible global runtime definitions to prevent undefined errors
const sandbox = {
    Array: Array,
    Object: Object,
    String: String,
    Number: Number,
    Boolean: Boolean,
    Math: Math,
    Date: Date,
    RegExp: RegExp,
    console: console,
    TheGame: null,
    game: function() {
        this.Title = "";
        this.OpeningMessage = "";
        this.Rooms = [];
        this.Player = {};
        this.Characters = [];
        this.Objects = [];
        this.Images = [];
        this.Variables = [];
        this.Timers = [];
        this.StatusBarItems = [];
        this.LayeredClothingZones = [];
    },
    player: function() {
        this.Name = "Player";
        this.Actions = [];
        this.Properties = [];
    },
    SetupObjectData: (d) => d,
    SetupVariableData: (d) => d,
    SetupTimerData: (d) => d,
    SetupStatusBarData: (d) => d,
    SetupRoomData: (d) => d,
    SetupCharacterData: (d) => d,
    SetupImageData: (d) => d,
    SetupPlayerData: (d) => d,
};

vm.createContext(sandbox);

console.log("Modifying JS string to expose local arrays...");
let modifiedJs = jsContent
    .replace(/\bvar imagedata\s*=/g, 'imagedata =')
    .replace(/\bvar roomdata\s*=/g, 'roomdata =')
    .replace(/\bvar chardata\s*=/g, 'chardata =')
    .replace(/\bvar objectdata\s*=/g, 'objectdata =')
    .replace(/\bvar variabledata\s*=/g, 'variabledata =')
    .replace(/\bvar timerdata\s*=/g, 'timerdata =')
    .replace(/\bvar statusbardata\s*=/g, 'statusbardata =')
    .replace(/\bvar layeredclothingdata\s*=/g, 'layeredclothingdata =')
    .replace(/\bvar playerdata\s*=/g, 'playerdata =');

const newSandbox = Object.assign({}, sandbox);
vm.createContext(newSandbox);
vm.runInContext(modifiedJs, newSandbox);

if (newSandbox.SetupGameData) {
    newSandbox.SetupGameData();
}

const extracted = {
    Title: newSandbox.TheGame?.Title,
    Author: newSandbox.TheGame?.AuthorName,
    Version: newSandbox.TheGame?.GameVersion,
    Description: newSandbox.TheGame?.GameInformation,
    imagedata: newSandbox.imagedata,
    roomdata: newSandbox.roomdata,
    chardata: newSandbox.chardata,
    objectdata: newSandbox.objectdata,
    variabledata: newSandbox.variabledata,
    timerdata: newSandbox.timerdata,
    statusbardata: newSandbox.statusbardata,
    layeredclothingdata: newSandbox.layeredclothingdata,
    playerdata: newSandbox.playerdata
};

fs.writeFileSync(outputPath, JSON.stringify(extracted, null, 2), 'utf8');
console.log(`Raw extraction successful! Saved to ${outputPath}`);
