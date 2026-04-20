const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

c = c.replace(/graph TD Director\(\(USER\)\)[\s\S]*?fill:#1e1e1d,stroke:#a5adaa,stroke-width:2px/g, `graph TD
    Director((USER)) -- Auth --> P1[Antigravity Orchestrator]
    P1 -- Delegate --> P2[Forensics Codex]
    P1 -- Design --> P3[Claude Architect]
    P3 -- Plan --> P4[Adjudicator Arena]
    P4 -- Verified Surgery --> P5[Codex Engineer]
    P5 -- Post-Audit --> P6[Validator Rider]
    style Director fill:#d97757,stroke:#fff,stroke-width:2px,color:#fff
    style P1 fill:#1e1e1d,stroke:#6a9bcc,stroke-width:2px
    style P3 fill:#1e1e1d,stroke:#d97757,stroke-width:2px
    style P5 fill:#1e1e1d,stroke:#a5adaa,stroke-width:2px`);

c = c.replace(/graph TD Core\["SOVEREIGN SUBSTRATE \(V12.15\)"\]:::hot[\s\S]*?rgba\(120,140,93,0.1\),color:var\(--brand-green\)/g, `graph TD
    Core["SOVEREIGN SUBSTRATE (V12.15)"]:::hot
    MCP["UNIFIED CONTEXT (MCP)"]:::input
    Harnesses{"AGENT HARNESSES"}:::input
    Goose["Goose (Desktop)"]:::output
    Droid["Droid (Factory AI)"]:::output
    Codex["Codex (Surgical)"]:::output
    Core --> MCP
    MCP --> Harnesses
    Harnesses --> Goose
    Harnesses --> Droid
    Harnesses --> Codex
    classDef input stroke:var(--brand-blue),stroke-width:2px,fill:rgba(106,155,204,0.1),color:var(--brand-blue)
    classDef hot stroke:var(--brand-orange),stroke-width:3px,fill:rgba(217,119,87,0.1),color:var(--brand-orange)
    classDef output stroke:var(--brand-green),stroke-width:2px,fill:rgba(120,140,93,0.1),color:var(--brand-green)`);

fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Fixed specific blocks.');
