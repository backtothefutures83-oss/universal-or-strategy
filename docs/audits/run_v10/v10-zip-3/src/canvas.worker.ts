let canvas: OffscreenCanvas | null = null;
let ctx: OffscreenCanvasRenderingContext2D | null = null;

interface WorkerData {
  id: number;
  latency: number;
  state: 'ACTIVE' | 'STALLED' | 'RECOVERING';
  partner: number;
}

let workers: WorkerData[] = [];
let dpr = 1;

self.onmessage = (e) => {
  if (e.data.type === 'INIT') {
    canvas = e.data.canvas;
    dpr = e.data.dpr || 1;
    if (canvas) {
      ctx = canvas.getContext('2d');
      if (ctx) {
        ctx.scale(dpr, dpr);
      }
      requestAnimationFrame(render);
    }
  } else if (e.data.type === 'UPDATE') {
    workers = e.data.workers;
  }
};

const THEME = {
  bg: '#0F172A', // slate-900
  card: '#1E293B', // slate-800
  text: '#F8FAFC', // slate-50
  muted: '#94A3B8', // slate-400
  active: '#10B981', // emerald-500
  stalled: '#EF4444', // red-500
  recovering: '#F59E0B', // amber-500
  border: '#334155' // slate-700
};

function render(time: number) {
  if (!ctx || !canvas || workers.length === 0) {
    requestAnimationFrame(render);
    return;
  }

  const width = canvas.width / dpr;
  const height = canvas.height / dpr;

  // Clear background
  ctx.fillStyle = THEME.bg;
  ctx.fillRect(0, 0, width, height);

  // Calculate grid (4 columns, 3 rows)
  const cols = 4;
  const rows = 3;
  const gap = 16;
  const padding = 16;
  
  const availableWidth = width - (padding * 2) - (gap * (cols - 1));
  const availableHeight = height - (padding * 2) - (gap * (rows - 1));
  
  const cellWidth = availableWidth / cols;
  const cellHeight = availableHeight / rows;

  workers.forEach((w, i) => {
    const col = i % cols;
    const row = Math.floor(i / cols);
    
    const x = padding + (col * (cellWidth + gap));
    const y = padding + (row * (cellHeight + gap));

    // Draw card background
    ctx!.fillStyle = THEME.card;
    ctx!.beginPath();
    ctx!.roundRect(x, y, cellWidth, cellHeight, 8);
    ctx!.fill();
    ctx!.lineWidth = 1;
    ctx!.strokeStyle = THEME.border;
    ctx!.stroke();

    // Draw status indicator
    let statusColor = THEME.active;
    if (w.state === 'STALLED') statusColor = THEME.stalled;
    if (w.state === 'RECOVERING') statusColor = THEME.recovering;

    ctx!.fillStyle = statusColor;
    ctx!.beginPath();
    ctx!.arc(x + cellWidth - 20, y + 20, 6, 0, Math.PI * 2);
    ctx!.fill();
    
    // Add glow effect for active/recovering
    if (w.state !== 'STALLED') {
      ctx!.shadowBlur = 10;
      ctx!.shadowColor = statusColor;
      ctx!.fill();
      ctx!.shadowBlur = 0; // reset
    }

    // Draw Worker ID
    ctx!.fillStyle = THEME.text;
    ctx!.font = 'bold 16px "Inter", sans-serif';
    ctx!.textAlign = 'left';
    ctx!.textBaseline = 'top';
    ctx!.fillText(`Worker ${w.id}`, x + 16, y + 16);

    // Draw Watchdog link
    ctx!.fillStyle = THEME.muted;
    ctx!.font = '12px "Inter", sans-serif';
    ctx!.fillText(`Monitors: W${w.partner}`, x + 16, y + 36);

    // Draw Latency (The core metric)
    ctx!.fillStyle = w.state === 'STALLED' ? THEME.stalled : THEME.active;
    ctx!.font = 'bold 32px "JetBrains Mono", monospace';
    ctx!.fillText(`${w.latency.toFixed(1)}`, x + 16, y + cellHeight - 48);
    
    // Draw unit
    ctx!.fillStyle = THEME.muted;
    ctx!.font = '14px "JetBrains Mono", monospace';
    ctx!.fillText('ns', x + 16 + ctx!.measureText(`${w.latency.toFixed(1)}`).width + 4, y + cellHeight - 40);
  });

  requestAnimationFrame(render);
}
