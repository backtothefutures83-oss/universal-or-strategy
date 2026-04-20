const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

const replacements = {    
    'B1_3 ==> B2_1 B2_2 ==> B3_1 B3_1 ==> B4_1 B2_3 ==> B3_2 B2_4 ==> B3_3 %% TACTICAL STYLING': 
    'B1_3 ==> B2_1\n  B2_2 ==> B3_1\n  B3_1 ==> B4_1\n  B2_3 ==> B3_2\n  B2_4 ==> B3_3\n  %% TACTICAL STYLING',

    'B1_6 ~~~ B2_6 ~~~ B3_6 ~~~ B4_6 %% FUNCTIONAL FLOWS (The actual "business" logic)':
    'B1_6 ~~~ B2_6 ~~~ B3_6 ~~~ B4_6\n  %% FUNCTIONAL FLOWS (The actual "business" logic)',
    
    'MMIO --> NIC_TX MMIO --> TOS_BRIDGE': 'MMIO --> NIC_TX\n        MMIO --> TOS_BRIDGE',
    
    'L2 ==> Agg L3 ==> Agg subgraph Consumer_Logic ["CONSUMER PLANE -- Sovereign Pump"]': 
    'L2 ==> Agg\n  L3 ==> Agg\n\n  subgraph Consumer_Logic ["CONSUMER PLANE -- Sovereign Pump"]',
    
    'MC --> HE HE --> R_TX HE --> S_API': 
    'MC --> HE\n    HE --> R_TX\n    HE --> S_API',

    'section P5: Engineering Surgical Site Injection :2026-04-22, 7d':
    'section P5: Engineering\n                  Surgical Site Injection :2026-04-22, 7d'  
};

// Replace exact strings safely
for (const [key, val] of Object.entries(replacements)) {
    c = c.replace(key, val);
}

// 3D topology initialization fix (remove broken script block containing window.addEventListener("load") and extraRenderers)
c = c.replace(/\(function \(\) \{\s*window\.addEventListener\("load", function \(\) \{\s*const graphElement = document\.getElementById\("spatial-3d-graph"\);\s*if \(!graphElement \|\| typeof ForceGraph3D === "undefined"\) return;\s*const Graph = ForceGraph3D\(\{ extraRenderers: \[css2DRenderer\] \}\)\s*\(graphElement\)\s*\.graphData\(gData\).*?\}\);\s*\}\)\(\);/gs, "");

fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Fixed natively');
