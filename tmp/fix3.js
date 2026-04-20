const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

c = c.replace(/B1_3 ==> B2_1 B2_2 ==> B3_1 B3_1 ==> B4_1 B2_3 ==> B3_2 B2_4 ==> B3_3 %% TACTICAL STYLING/g, 
`B1_3 ==> B2_1\nB2_2 ==> B3_1\nB3_1 ==> B4_1\nB2_3 ==> B3_2\nB2_4 ==> B3_3\n%% TACTICAL STYLING`);

c = c.replace(/B1_6 ~~~ B4_6 %% FUNCTIONAL FLOWS \(The actual "business" logic\)/g,
`B1_6 ~~~ B4_6\n%% FUNCTIONAL FLOWS (The actual "business" logic)`);

c = c.replace(/Shadow --> AVX Loop\["Audit Loop"\]/g, 
`Shadow --> AVX\nLoop["Audit Loop"]`);

c = c.replace(/MMIO --> NIC_TX MMIO --> TOS_BRIDGE/g, 
`MMIO --> NIC_TX\nMMIO --> TOS_BRIDGE`);

c = c.replace(/AFX --> Shadow AVX --> MMIO %% CROSS-PLANE FEEDBACK Guard -\.-\-> Shadow NIC_TX -\.-\-> Loop TOS_BRIDGE -\.-\-> Loop %% STYLING/g,
`AFX --> Shadow\nAVX --> MMIO\n%% CROSS-PLANE FEEDBACK\nGuard -.-> Shadow\nNIC_TX -.-> Loop\nTOS_BRIDGE -.-> Loop\n%% STYLING`);

c = c.replace(/L2 ==> Agg L3 ==> Agg subgraph/g, 
`L2 ==> Agg\nL3 ==> Agg\nsubgraph`);

c = c.replace(/MC --> HE HE --> R_TX HE --> S_API/g, 
`MC --> HE\nHE --> R_TX\nHE --> S_API`);

fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Fixed multiple line-break problems!');
