const fs = require('fs');
const c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

// Find all elements with mermaid class
const regex = /<div[^>]*class="[^"]*mermaid[^"]*"[^>]*>([\s\S]*?)<\/div>/g;
let match;
let count = 0;
while ((match = regex.exec(c)) !== null) {
    console.log(`\n\n--- BLOCK ${count} ---`);
    console.log(`Starts at index: ${match.index}`);
    console.log(match[1]);
    count++;
}
console.log(`\nTotal found: ${count}`);
