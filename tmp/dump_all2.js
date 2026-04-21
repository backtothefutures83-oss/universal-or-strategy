const fs = require('fs');
const c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

const regex = /<div[^>]*class="[^"]*mermaid[^"]*"[^>]*>([\s\S]*?)<\/div>/g;
let match;
let count = 0;
let out = '';
while ((match = regex.exec(c)) !== null) {
    out += `\n\n--- BLOCK ${count} ---\n`;
    out += `Starts at index: ${match.index}\n`;
    out += match[1];
    count++;
}
out += `\nTotal found: ${count}\n`;
fs.writeFileSync('tmp/dump_out.txt', out, 'utf8');
