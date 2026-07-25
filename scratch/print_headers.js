const fs = require('fs');
const raw = JSON.parse(fs.readFileSync('C:\\Users\\steve\\source\x2frepos\\RagNext\\TheBet_raw.json', 'utf8'));

if (raw.chardata && raw.chardata.length > 0) {
    console.log("chardata[0][7] (properties):", JSON.stringify(raw.chardata[0][7], null, 2));
    console.log("chardata[0][8] (actions):", JSON.stringify(raw.chardata[0][8], null, 2));
}
if (raw.objectdata && raw.objectdata.length > 0) {
    console.log("objectdata[0][20] (properties):", JSON.stringify(raw.objectdata[0][20], null, 2));
    console.log("objectdata[0][21] (actions):", JSON.stringify(raw.objectdata[0][21], null, 2));
    console.log("objectdata[0][22] (clothing/wear details):", JSON.stringify(raw.objectdata[0][22], null, 2));
}
