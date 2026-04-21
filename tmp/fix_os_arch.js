const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

c = c.replace(/graph LR\s+%%\s+4-COLUMN SPREADSHEET ALIGNMENT.*?B4_2,B4_3,B4_4,B4_5,B4_6 greenNode;/g, `graph LR
    subgraph Box1 ["1. INPUT PLANE"]
      direction TB 
      B1_1["Antigravity Web Dashboard"] 
      B1_2["NinjaTrader Desktop UI"] 
      B1_3["TradingView (Browser)"] 
      B1_4["Tradovate (Mobile App)"] 
      B1_5(("API Bridge")) 
      B1_6["[Ingress Gate]"]
    end
    subgraph Box2 ["2. DECISION PLANE"] 
      direction TB 
      B2_1["Signal Hub"] 
      B2_2["Fast Lane: Order Dispatch"] 
      B2_3["Screen Lane: Charts"] 
      B2_4["Voice Lane: Alerts"] 
      B2_5["Self-Healing"] 
      B2_6["[Brain Core]"]
    end
    subgraph Box3 ["3. SUBSTRATE"]
      direction TB 
      B3_1["15ns NIC (NUMA)"] 
      B3_2["OCR Engine"] 
      B3_3["Voice Engine"] 
      B3_4["Memory-Map (MMIO)"] 
      B3_5["L1 Tick Hub"] 
      B3_6["[Hardware Layer]"]
    end
    subgraph Box4 ["4. EXECUTION PLANE"]
      direction TB 
      B4_1["NinjaTrader / Rithmic"] 
      B4_2["Native Market Driver<br/>[L1 Tick Feed => NIC]"] 
      B4_3["ExecutionId Ring"] 
      B4_4["Order Pool (Pre-Alloc)"] 
      B4_5["Direct Bridge"] 
      B4_6["[Execution Gate]"]
    end
    
    B1_1 ~~~ B2_1
    B2_1 ~~~ B3_1
    B3_1 ~~~ B4_1
    B1_2 ~~~ B2_2
    B2_2 ~~~ B3_2
    B3_2 ~~~ B4_2
    B1_3 ~~~ B2_3
    B2_3 ~~~ B3_3
    B3_3 ~~~ B4_3
    B1_4 ~~~ B2_4
    B2_4 ~~~ B3_4
    B3_4 ~~~ B4_4
    B1_5 ~~~ B2_5
    B2_5 ~~~ B3_5
    B3_5 ~~~ B4_5
    B1_6 ~~~ B2_6
    B2_6 ~~~ B3_6
    B3_6 ~~~ B4_6

    B1_3 ==> B2_1 
    B2_2 ==> B3_1 
    B3_1 ==> B4_1 
    B2_3 ==> B3_2 
    B2_4 ==> B3_3
    
    style Box1 fill:#0a1a1a,stroke:#6a9bcc,stroke-width:2px;
    style Box2 fill:#150525,stroke:#d97757,stroke-width:2px;
    style Box3 fill:#1a1a00,stroke:#b0aea5,stroke-width:2px;
    style Box4 fill:#051505,stroke:#788c5d,stroke-width:2px;
    classDef cyanNode fill:#101010,stroke:#6a9bcc,stroke-width:2px;
    classDef purpleNode fill:#101010,stroke:#d97757,stroke-width:2px;
    classDef purpleNodeThick fill:#101010,stroke:#d97757,stroke-width:3px;
    classDef goldNode fill:#101010,stroke:#b0aea5,stroke-width:2px;
    classDef greenNode fill:#101010,stroke:#788c5d,stroke-width:2px;
    class B1_1,B1_2,B1_3,B1_4,B1_5,B1_6 cyanNode;
    class B2_1 purpleNodeThick;
    class B2_2 cyanNode;
    class B2_3,B2_4,B2_5,B2_6 purpleNode;
    class B3_1,B3_2,B3_3,B3_4,B3_5,B3_6 goldNode;
    class B4_1 cyanNode;
    class B4_2,B4_3,B4_4,B4_5,B4_6 greenNode;`);

fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Fixed OS Architecture graph.');
