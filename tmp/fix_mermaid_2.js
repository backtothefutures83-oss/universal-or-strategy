const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

c = c.replace(/<div class="mermaid">\s*([\s\S]*?)\s*<\/div>/g, (match, code) => {
  let fixed = code.replace(/\n\+/g, ' '); // collapse all existing weirdness
  
  // Re-inject newlines before 'subgraph'
  fixed = fixed.replace(/subgraph /g, '\nsubgraph ');
  
  // Re-inject newlines after 'direction XX'
  fixed = fixed.replace(/direction (TB|LR|RL|BT)\s+/g, '\ndirection $1\n');
  
  // Re-inject newlines before 'style'
  fixed = fixed.replace(/ style /g, '\nstyle ');
  fixed = fixed.replace(/;style /g, ';\nstyle ');

  // Re-inject newlines before 'classDef' and 'class'
  fixed = fixed.replace(/ classDef /g, '\nclassDef ');
  fixed = fixed.replace(/ class /g, '\nclass ');

  // Re-inject newlines before connections
  // Examples: a[Text] b[Text] -> a[Text]\nb[Text]
  // or a[Text] b --> c -> a[Text]\nb --> c
  // Node["] -> Node["]\n NextNode
  fixed = fixed.replace(/\]\s+([A-Z0-9_]+(?:\[|\(|>|\{))/gi, ']\n$1');
  fixed = fixed.replace(/\)\s+([A-Z0-9_]+(?:\[|\(|>|\{))/gi, ')\n$1');
  fixed = fixed.replace(/\}\s+([A-Z0-9_]+(?:\[|\(|>|\{))/gi, '}\n$1');
  
  // New Connection from existing node: e.g. `] Dispatch -->`
  fixed = fixed.replace(/\]\s+([A-Z0-9_]+\s+(?:-->|==>|-.->))/gi, ']\n$1');
  fixed = fixed.replace(/\)\s+([A-Z0-9_]+\s+(?:-->|==>|-.->))/gi, ')\n$1');
  fixed = fixed.replace(/\}\s+([A-Z0-9_]+\s+(?:-->|==>|-.->))/gi, '}\n$1');

  // Matrix ranks: B1_1 ~~~ B2_1
  fixed = fixed.replace(/([A-Z0-9_]+)\s+~~~\s+/gi, '\n$1 ~~~ ');

  // Handle graph type
  fixed = fixed.replace(/(graph LR|graph TD|graph TB|sequenceDiagram|pie|classDiagram)\s+/gi, '$1\n');

  // Any remaining " end "
  fixed = fixed.replace(/\s+end\s+/g, '\nend\n');

  // Remove trailing internal spaces
  fixed = fixed.split('\n').map(l => l.trim()).filter(l => l.length > 0).join('\n');

  return '<div class="mermaid">\n' + fixed + '\n</div>';
});

// Gantt charts are different
c = c.replace(/<div class="mermaid">\s*gantt[\s\S]*?<\/div>/g, (match) => {
  let fixed = match.replace(/\s+/g, ' ');
  fixed = fixed.replace(/gantt /g, 'gantt\n');
  fixed = fixed.replace(/dateFormat ([A-Z-]+) /g, 'dateFormat $1\n');
  fixed = fixed.replace(/title (.*?) section /g, 'title $1\nsection ');
  fixed = fixed.replace(/section (.*?) ([A-Za-z0-9_]+)\s+:/g, '\nsection $1\n$2 :');
  fixed = fixed.replace(/:([^\n]*?), ([A-Za-z0-9_-]+)\s+:/g, ':$1\n$2 :');
  fixed = fixed.replace(/:([^\n]*?), ([0-9]+[dhws]) ([A-Za-z0-9_-]+)\s+:/g, ':$1, $2\n$3 :'); // attempt simple fix
  // Just rewrite the gantt completely
  if (match.includes("SOVEREIGN MISSION TIMELINE")) {
     return `<div class="mermaid">
gantt
dateFormat YYYY-MM-DD
title SOVEREIGN MISSION TIMELINE

section PLATFORM HARDENING
P4 Architectural Audit :done, org_1, 2026-04-18, 1d
P5 Surgical Execution :active, org_2, 2026-04-19, 2d
P6 Validation Gate :org_3, 2026-04-21, 1d

section AGENT DEPLOYMENT
Orphan Sites Repaired :crit, org_4, 2026-04-19, 1d
Rithmic Bridge Live :org_5, 2026-04-22, 2d
Schwab Auth Flow :org_6, 2026-04-24, 2d

section ALPHA RUN
Sovereign Brain Ignition :milestone, org_7, 2026-04-26, 0d
First Pulse Verification :org_8, 2026-04-26, 1d
</div>`;
  }
  return match; // return un-transformed for others
});

// For Sovereign Big Picture specifically, rewrite the whole block since it's unrecoverable easily.
c = c.replace(/<div class="mermaid">\s*graph LR(?:(?!<\/div>)[\s\S])*?Big[ ]?Picture(?:(?!<\/div>)[\s\S])*?<\/div>/i, `<div class="mermaid">
graph LR
    subgraph Nexus[Antigravity Nexus OS]
        direction TB
        OS[Sovereign Core] --> UI[Aion Interface]
        UI --> Droid[Droid CLI]
    end
    subgraph Market[Market Drivers]
        direction TB
        NIC[Direct NIC MMIO]
        Schwab[Schwab API]
        Rithmic[Rithmic Market Data]
    end
    OS ==>|Low Latency| NIC
    OS -->|Execution| Schwab
    OS -->|Data| Rithmic
    classDef nexus fill:#101010,stroke:#d97757,stroke-width:2px;
    classDef market fill:#001a4d,stroke:#6a9bcc,stroke-width:2px;
    class Nexus nexus;
    class Market market;
</div>`);

// Also fix the huge one from OS Architecture (4 columns)
// Let's just fix the HTML formatting to make it clean.
fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Fixed more cases.');
