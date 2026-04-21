const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

c = c.replace(/gantt title Platinum Hardening Sequence dateFormat YYYY-MM-DD[\s\S]*?Injection :2026-04-22, 7d/g, `gantt
    title Platinum Hardening Sequence
    dateFormat  YYYY-MM-DD
    section P1: Research
    Forensic Audit :done, 2026-04-15, 2d
    Orphan Site Mapping :done, 2026-04-17, 1d
    section P3: Design
    ADR-19 Structural Plan :active, 2026-04-18, 3d
    Security Pulse Logic :2026-04-19, 2d
    section P4: Adjudication
    Arena Red Team Battle :2026-04-20, 5d
    section P5: Engineering
    Surgical Site Injection :2026-04-22, 7d`);

fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Fixed gantt.');
