const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

const colorReplacements = {
    '0052FF': '6a9bcc', // Blue
    '5D3FD3': 'd97757', // Indigo -> Orange
    '05B169': '788c5d', // Green -> Green
    'bc13fe': 'd97757', // Light Purple -> Orange
    '00e5ff': '6a9bcc', // Cyan -> Blue
    'ffd700': 'a5adaa', // Gold -> Gray/Slate
    '39ff14': '788c5d', // Neon Green -> Bronze/Green
    'CF304A': 'd97757', // Red -> Orange
    'FF9900': 'd97757', // Orange -> Orange
};

for (const [oldColor, newColor] of Object.entries(colorReplacements)) {
    c = c.replace(new RegExp(oldColor, 'gi'), newColor);
}

fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Fixed hardcoded colors inside mermaid.');
