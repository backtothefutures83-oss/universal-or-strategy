const fs = require('fs');
let c = fs.readFileSync('docs/arena_dashboard.html', 'utf8');

// 1. Fonts and Variables
c = c.replace(/@import url\("https:\/\/fonts\.googleapis\.com\/css2.*?swap"\);/, `@import url("https://fonts.googleapis.com/css2?family=Poppins:wght@400;600;700&family=Lora:ital,wght@0,400;0,700;1,400&family=JetBrains+Mono:wght@400;700&display=swap");`);

c = c.replace(/:root\s*\{[\s\S]*?--cb-warning:[^\}]*\}/, `:root {
        --bg: #1e1e1d;
        --card-bg: rgba(255, 255, 255, 0.03);
        --border: rgba(255, 255, 255, 0.08);
        --text-main: #f5f5f5;
        --text-mid: #c0c0c0;
        --text-dim: #8a8a8a;
        --text-bright: #ffffff;
        --surface2: rgba(255, 255, 255, 0.05);

        --font-heading: 'Poppins', sans-serif;
        --font-body: 'Lora', serif;
        --font-mono: 'JetBrains Mono', monospace;

        --cb-blue: #6a9bcc;
        --cb-indigo: #d97757;
        --cb-green: #788c5d;
        --cb-slate: #a5adaa;
        --cb-warning: #d97757;`);

// 2. Change font families in rules
c = c.replace(/font-family:\s*var\(--tactical-font\);/g, 'font-family: var(--font-heading);');
c = c.replace(/font-family:\s*var\(--display-font\);/g, 'font-family: var(--font-heading);');

// 3. Add Glassmorphism to Cards and bubbly styling
c = c.replace(/\.card\s*\{[\s\S]*?\}/, `.card {
        background: var(--card-bg);
        border: 1px solid var(--border);
        padding: 24px;
        backdrop-filter: blur(16px);
        -webkit-backdrop-filter: blur(16px);
        border-radius: 24px;
        box-shadow: 0 8px 32px rgba(0, 0, 0, 0.2);
      }`);

c = c.replace(/\.tab-button\s*\{[\s\S]*?\}/, `.tab-button {
        display: block;
        width: 100%;
        text-align: left;
        background: transparent;
        color: var(--text-mid);
        border: none;
        padding: 14px 20px;
        font-family: var(--font-heading);
        font-size: 13px;
        font-weight: 600;
        cursor: pointer;
        transition: all 0.3s cubic-bezier(0.25, 0.8, 0.25, 1);
        border-radius: 16px;
        margin-bottom: 8px;
      }`);

c = c.replace(/\.tab-button:hover\s*\{[\s\S]*?\}/, `.tab-button:hover {
        background: rgba(255, 255, 255, 0.05);
        color: var(--text-bright);
        transform: translateX(4px);
      }`);

c = c.replace(/\.tab-button\.active\s*\{[\s\S]*?\}/, `.tab-button.active {
        background: var(--brand-blue);
        color: #ffffff;
        box-shadow: 0 4px 15px rgba(106, 155, 204, 0.4);
        transform: translateX(6px);
      }`);

// Also update sidebar styling safely
c = c.replace(/\.sidebar\s*\{[\s\S]*?\}/, `.sidebar {
        width: 260px;
        padding-right: 24px;
        border-right: 1px solid var(--border);
      }`);

fs.writeFileSync('docs/arena_dashboard.html', c);
console.log('Restored brand guidelines and bubbly design.');
