const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

c = c.replace(/<div class="mermaid">\s*([\s\S]*?)\s*<\/div>/g, (match, code) => {
  // First, replace all literal newlines with space, to undo the html wrapper's messing up
  let fixed = code.replace(/\n\s+/g, ' ');
  // Now add explicit newlines around standard mermaid syntax tokens
  fixed = fixed.replace(/;/g, ';\n  ');
  // Handle graph LR, TB etc
  fixed = fixed.replace(/(graph LR|graph TD|graph TB|sequenceDiagram|gantt|pie|classDiagram)/g, '$1\n  ');
  // Handle subgraph
  fixed = fixed.replace(/subgraph (.*?)\s+direction/g, 'subgraph $1\n  direction');
  // Handle end
  fixed = fixed.replace(/ end/g, '\nend\n');
  return '<div class="mermaid">\n' + fixed + '\n</div>';
});

// For Sovereign Big Picture timeline
c = c.replace(/gantt\s+dateFormat\s+YYYY-MM-DD\s+title\s+SOVEREIGN MISSION TIMELINE/g, 
  "gantt\n  dateFormat YYYY-MM-DD\n  title SOVEREIGN MISSION TIMELINE\n");
c = c.replace(/section (.*?)\s+(.*?)\s+:\s+/g, "\nsection $1\n  $2 : ");

// Remove the conflicting IIFE from lines 3176-3260 and duplicate topo init
c = c.replace(/<script>\s*\(\s*function\s*\(\)\s*\{[\s\S]*?\}\s*\)\s*\(\)\s*;?\s*<\/script>/g, ""); // this might be risky, but let's test it first.

fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Fixed mermaid.');
