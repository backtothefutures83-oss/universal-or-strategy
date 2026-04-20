import re

with open("docs/arena_dashboard.html", "r", encoding="utf-8") as f:
    content = f.read()

# 1. Update :root variables
root_original = """      @import url("https://fonts.googleapis.com/css2?family=IBM+Plex+Mono:wght@400;600;700&family=Syncopate:wght@400;700&display=swap");

      :root {
        --bg: #050a14;
        --card-bg: #0a111e;
        --border: #1e2b45;
        --text-main: #ffffff;
        --text-mid: #b0b8c6;
        --text-dim: #6a7381;
        --text-bright: #ffffff;
        --surface2: #0d1020;

        /* Coinbase Design System (CDS) Inspired Palette */
        --cb-blue: #0052FF;
        --cb-indigo: #5D3FD3;
        --cb-green: #05B169;
        --cb-slate: #8F9BB3;
        --cb-warning: #FF9900;
        --cb-critical: #CF304A;

        /* Anthropic Brand Accents (brand-guidelines skill) */
        --brand-orange: #d97757;
        --brand-blue: #6a9bcc;
        --brand-green: #788c5d;

        /* Legacy Mapping */
        --cyan: var(--cb-blue);
        --purple: var(--cb-indigo);
        --green: var(--cb-green);
        --gold: var(--cb-slate);
        --orange: var(--cb-warning);
        --red: var(--cb-critical);
        --neon-green: #09f592;
        --hazard-orange: var(--cb-warning);
        --tactical-font: "IBM Plex Mono", monospace;
        --display-font: "Syncopate", sans-serif;
      }"""

root_new = """      @import url('https://fonts.googleapis.com/css2?family=Lora:ital,wght@0,400..700;1,400..700&family=Poppins:ital,wght@0,100..900;1,100..900&display=swap');

      :root {
        /* Brand Neutrals */
        --brand-dark: #141413;
        --brand-light: #faf9f5;
        --brand-mid-gray: #b0aea5;
        --brand-light-gray: #e8e6dc;

        /* Brand Accents */
        --brand-orange: #d97757;
        --brand-blue: #6a9bcc;
        --brand-green: #788c5d;

        /* Legacy Application Mappings */
        --bg: var(--brand-dark);
        --card-bg: var(--brand-dark); 
        --border: rgba(250, 249, 245, 0.1); 
        --text-main: var(--brand-light);
        --text-mid: var(--brand-mid-gray);
        --text-dim: var(--brand-mid-gray);
        --text-bright: var(--brand-light);
        --surface2: #1e1e1d;

        /* Override Tactical Colors with Anthropic Brand Cycle */
        --cb-blue: var(--brand-blue);
        --cb-indigo: var(--brand-orange);
        --cb-green: var(--brand-green);
        --cb-slate: var(--brand-mid-gray);
        --cb-warning: var(--brand-orange);
        --cb-critical: var(--brand-blue); 
        
        --cyan: var(--brand-blue);
        --purple: var(--brand-orange);
        --green: var(--brand-green);
        --gold: var(--brand-mid-gray);
        --orange: var(--brand-orange);
        --red: var(--brand-orange);
        --neon-green: var(--brand-green);
        --hazard-orange: var(--brand-orange);

        --font-heading: "Poppins", Arial, sans-serif;
        --font-body: "Lora", Georgia, serif;
        --tactical-font: var(--font-body);
        --display-font: var(--font-heading);
      }"""

content = content.replace(root_original, root_new)

# 2. Keyframes pulse updates
keyframes_old = """      /* --- PULSE-CYAN: Ingress / Input / Dispatch layer --- */
      @keyframes pulseInput {
        0%,100% { stroke: #00ffcc; stroke-width: 1.5px; }
        50%      { stroke: #00e5ff; stroke-width: 3.5px; }
      }
      .mermaid svg [data-role="input"] {
        animation: pulseInput 1.8s ease-in-out infinite !important;
        filter: url(#liquid-glow) drop-shadow(0 0 5px #00ffcc) drop-shadow(0 0 12px rgba(0,255,204,0.35)) !important;
      }

      /* --- PULSE-MAGENTA: Hot path / Brain / Sovereign Core --- */
      @keyframes pulseHot {
        0%,100% { stroke: #bc13fe; stroke-width: 2px; }
        50%      { stroke: #ff00ff; stroke-width: 5px; }
      }
      .mermaid svg [data-role="hot"] {
        animation: pulseHot 0.9s ease-in-out infinite !important;
        filter: url(#liquid-glow) drop-shadow(0 0 8px #ff00ff) drop-shadow(0 0 20px rgba(188,19,254,0.5)) !important;
      }

      /* --- PULSE-GOLD: Hardware / Fabric / NIC / Physical substrate --- */
      @keyframes pulseGold {
        0%,100% { stroke: #FFB800; stroke-width: 1.5px; }
        50%      { stroke: #ff8800; stroke-width: 3.5px; }
      }
      .mermaid svg [data-role="gold"] {
        animation: pulseGold 1.6s ease-in-out 0.4s infinite !important;
        filter: url(#liquid-glow) drop-shadow(0 0 6px #FFB800) drop-shadow(0 0 14px rgba(255,136,0,0.4)) !important;
      }

      /* --- PULSE-GREEN: Output / Egress / Execution success --- */
      @keyframes pulseOutput {
        0%,100% { stroke: #05B169; stroke-width: 1.5px; }
        50%      { stroke: #00ff88; stroke-width: 3.5px; }
      }
      .mermaid svg [data-role="output"] {
        animation: pulseOutput 1.8s ease-in-out 0.6s infinite !important;
        filter: url(#liquid-glow) drop-shadow(0 0 5px #05B169) drop-shadow(0 0 10px rgba(5,177,105,0.35)) !important;
      }

      /* --- PULSE EDGES (DATA FLOW LINES) --- */
      /* 1. CYAN: Fast sparse proton dot */
      @keyframes flowCyan { from { stroke-dashoffset: 160; } to { stroke-dashoffset: -160; } }
      .mermaid svg .edgePath path[data-flow="cyan"], .mermaid svg .edgePaths path[data-flow="cyan"] {
        stroke: #00ffcc !important; stroke-width: 1.5px !important;
        stroke-dasharray: 10 150 !important;
        filter: url(#liquid-glow) drop-shadow(0 0 4px #00ffcc) !important;
        animation: flowCyan 0.9s linear infinite !important;
      }

      /* 2. MAGENTA: Growing dash stream (Fixed anchors, growing length) */
      @keyframes flowMagenta { 0% { stroke-dasharray: 0 20; } 100% { stroke-dasharray: 20 0; } }
      .mermaid svg .edgePath path[data-flow="magenta"], .mermaid svg .edgePaths path[data-flow="magenta"] {
        stroke: #bc13fe !important; stroke-width: 2px !important;
        filter: url(#liquid-glow) drop-shadow(0 0 6px #ff00ff) !important;
        animation: flowMagenta 1.2s linear infinite !important;
      }

      /* 3. GOLD: Dense hardware clock dots */
      @keyframes flowGold { from { stroke-dashoffset: 50; } to { stroke-dashoffset: 0; } }
      .mermaid svg .edgePath path[data-flow="gold"], .mermaid svg .edgePaths path[data-flow="gold"] {
        stroke: #FFB800 !important; stroke-width: 1.5px !important;
        stroke-dasharray: 2 4 !important;
        filter: url(#liquid-glow) drop-shadow(0 0 4px #FFB800) !important;
        animation: flowGold 0.5s linear infinite !important;
      }

      /* 4. GREEN: Long success packets */
      @keyframes flowGreen { from { stroke-dashoffset: 300; } to { stroke-dashoffset: 0; } }
      .mermaid svg .edgePath path[data-flow="green"], .mermaid svg .edgePaths path[data-flow="green"] {
        stroke: #05B169 !important; stroke-width: 1.5px !important;
        stroke-dasharray: 50 100 !important;
        filter: url(#liquid-glow) drop-shadow(0 0 4px #00ff88) !important;
        animation: flowGreen 1.5s linear infinite !important;
      }"""

keyframes_new = """      /* --- PULSE-CYAN: Ingress / Input / Dispatch layer --- */
      @keyframes pulseInput {
        0%,100% { stroke: var(--brand-blue); stroke-width: 1.5px; }
        50%      { stroke: #88b1db; stroke-width: 3.5px; }
      }
      .mermaid svg [data-role="input"] {
        animation: pulseInput 1.8s ease-in-out infinite !important;
        filter: url(#liquid-glow) drop-shadow(0 0 5px var(--brand-blue)) drop-shadow(0 0 12px rgba(106,155,204,0.35)) !important;
      }

      /* --- PULSE-MAGENTA: Hot path / Brain / Sovereign Core --- */
      @keyframes pulseHot {
        0%,100% { stroke: var(--brand-orange); stroke-width: 2px; }
        50%      { stroke: #e39379; stroke-width: 5px; }
      }
      .mermaid svg [data-role="hot"] {
        animation: pulseHot 0.9s ease-in-out infinite !important;
        filter: url(#liquid-glow) drop-shadow(0 0 8px var(--brand-orange)) drop-shadow(0 0 20px rgba(217,119,87,0.5)) !important;
      }

      /* --- PULSE-GOLD: Hardware / Fabric / NIC / Physical substrate --- */
      @keyframes pulseGold {
        0%,100% { stroke: var(--brand-mid-gray); stroke-width: 1.5px; }
        50%      { stroke: var(--brand-light-gray); stroke-width: 3.5px; }
      }
      .mermaid svg [data-role="gold"] {
        animation: pulseGold 1.6s ease-in-out 0.4s infinite !important;
        filter: url(#liquid-glow) drop-shadow(0 0 6px var(--brand-mid-gray)) drop-shadow(0 0 14px rgba(176,174,165,0.4)) !important;
      }

      /* --- PULSE-GREEN: Output / Egress / Execution success --- */
      @keyframes pulseOutput {
        0%,100% { stroke: var(--brand-green); stroke-width: 1.5px; }
        50%      { stroke: #95a67c; stroke-width: 3.5px; }
      }
      .mermaid svg [data-role="output"] {
        animation: pulseOutput 1.8s ease-in-out 0.6s infinite !important;
        filter: url(#liquid-glow) drop-shadow(0 0 5px var(--brand-green)) drop-shadow(0 0 10px rgba(120,140,93,0.35)) !important;
      }

      /* --- PULSE EDGES (DATA FLOW LINES) --- */
      /* 1. CYAN: Fast sparse proton dot */
      @keyframes flowCyan { from { stroke-dashoffset: 160; } to { stroke-dashoffset: -160; } }
      .mermaid svg .edgePath path[data-flow="cyan"], .mermaid svg .edgePaths path[data-flow="cyan"] {
        stroke: var(--brand-blue) !important; stroke-width: 1.5px !important;
        stroke-dasharray: 10 150 !important;
        filter: url(#liquid-glow) drop-shadow(0 0 4px var(--brand-blue)) !important;
        animation: flowCyan 0.9s linear infinite !important;
      }

      /* 2. MAGENTA: Growing dash stream (Fixed anchors, growing length) */
      @keyframes flowMagenta { 0% { stroke-dasharray: 0 20; } 100% { stroke-dasharray: 20 0; } }
      .mermaid svg .edgePath path[data-flow="magenta"], .mermaid svg .edgePaths path[data-flow="magenta"] {
        stroke: var(--brand-orange) !important; stroke-width: 2px !important;
        filter: url(#liquid-glow) drop-shadow(0 0 6px var(--brand-orange)) !important;
        animation: flowMagenta 1.2s linear infinite !important;
      }

      /* 3. GOLD: Dense hardware clock dots */
      @keyframes flowGold { from { stroke-dashoffset: 50; } to { stroke-dashoffset: 0; } }
      .mermaid svg .edgePath path[data-flow="gold"], .mermaid svg .edgePaths path[data-flow="gold"] {
        stroke: var(--brand-mid-gray) !important; stroke-width: 1.5px !important;
        stroke-dasharray: 2 4 !important;
        filter: url(#liquid-glow) drop-shadow(0 0 4px var(--brand-mid-gray)) !important;
        animation: flowGold 0.5s linear infinite !important;
      }

      /* 4. GREEN: Long success packets */
      @keyframes flowGreen { from { stroke-dashoffset: 300; } to { stroke-dashoffset: 0; } }
      .mermaid svg .edgePath path[data-flow="green"], .mermaid svg .edgePaths path[data-flow="green"] {
        stroke: var(--brand-green) !important; stroke-width: 1.5px !important;
        stroke-dasharray: 50 100 !important;
        filter: url(#liquid-glow) drop-shadow(0 0 4px var(--brand-green)) !important;
        animation: flowGreen 1.5s linear infinite !important;
      }"""

content = content.replace(keyframes_old, keyframes_new)

# 3. Replace inline Mermaid colours
replacements = [
    (r"#0052FF", "#6a9bcc"), (r"#00e5ff", "#6a9bcc"), (r"#00ffcc", "#6a9bcc"), 
    (r"#5D3FD3", "#d97757"), (r"#bc13fe", "#d97757"), (r"#ff00ff", "#d97757"),
    (r"#FFB800", "#b0aea5"), (r"#ff8800", "#b0aea5"), (r"#ffd700", "#b0aea5"),
    (r"#05B169", "#788c5d"), (r"#39ff14", "#788c5d"), (r"#00ff88", "#788c5d"),
    (r"#CF304A", "#d97757"), (r"#ff2244", "#d97757"),
    (r"#0a0a0a", "#141413"), (r"#111111", "#141413"), (r"#111(?![0-9a-fA-F])", "var(--card-bg)"),
    (r"rgba\(0,\s*82,\s*255,\s*(0\.\d+)\)", r"rgba(106, 155, 204, \g<1>)"),
    (r"rgba\(93,\s*63,\s*211,\s*(0\.\d+)\)", r"rgba(217, 119, 87, \g<1>)"),
    (r"rgba\(5,\s*177,\s*105,\s*(0\.\d+)\)", r"rgba(120, 140, 93, \g<1>)"),
    (r"rgba\(255,\s*153,\s*0,\s*(0\.\d+)\)", r"rgba(217, 119, 87, \g<1>)"),
    (r"rgba\(207,\s*48,\s*74,\s*(0\.\d+)\)", r"rgba(217, 119, 87, \g<1>)"),
    (r"rgba\(0,\s*229,\s*255,\s*(0\.\d+)\)", r"rgba(106, 155, 204, \g<1>)"),
    (r"rgba\(188,\s*19,\s*254,\s*(0\.\d+)\)", r"rgba(217, 119, 87, \g<1>)"),
    (r"rgba\(255,\s*215,\s*0,\s*(0\.\d+)\)", r"rgba(176, 174, 165, \g<1>)"),
    (r"rgba\(57,\s*255,\s*20,\s*(0\.\d+)\)", r"rgba(120, 140, 93, \g<1>)"),
    (r"rgba\(255,\s*34,\s*68,\s*(0\.\d+)\)", r"rgba(217, 119, 87, \g<1>)"),
    (r"rgba\(255,\s*107,\s*0,\s*(0\.\d+)\)", r"rgba(217, 119, 87, \g<1>)"),
]

for pat, repl in replacements:
    content = re.sub(pat, repl, content, flags=re.IGNORECASE)
    
# 4. Replace other hardcoded CSS colors like #0a111e, #1e2b45, #080e1a with Anthropic variable refs
background_replacements = [
    (r"(background|background-color):\s*#0a1[0-9a-fA-F]*", r"\1: var(--card-bg)"),
    (r"(background|background-color):\s*#1e2[0-9a-fA-F]*", r"\1: var(--surface2)"),
    (r"(background|background-color):\s*#0d1[0-9a-fA-F]*", r"\1: var(--surface2)"),
    (r"(background|background-color):\s*#080[0-9a-fA-F]*", r"\1: var(--card-bg)"),
    (r"(background|background-color):\s*#1[aA][1[aA][1[aA]", r"\1: var(--surface2)"),
    (r"(background|background-color):\s*#020[0-9a-fA-F]*", r"\1: var(--brand-dark)"),
]
for pat, repl in background_replacements:
    content = re.sub(pat, repl, content, flags=re.IGNORECASE)

with open("docs/arena_dashboard_new.html", "w", encoding="utf-8") as f:
    f.write(content)

print("Replacement complete. You can now use run_command with Rename-Item/Move-Item to replace the old file.")
