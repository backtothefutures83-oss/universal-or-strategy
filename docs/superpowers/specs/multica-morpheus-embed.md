# Multica -- Morpheus OS Shell Embed Spec (VS-4)

**Phase**: VS-4 (Electron Shell)  
**Author**: Antigravity (P1)  
**Status**: PLANNED — implement when VS-3 L1 Bridge is complete

---

## Goal

Embed the Multica agent board natively inside the Morpheus OS Shell as the **Agent Manager tab**.  
Agents appear as living teammates inside the OS — aligned with the Metamorphosis "Agent Presence" pillar.

---

## Integration Approach: Option A (iframe, fast) vs Option B (native Svelte, premium)

### Option A -- iframe embed (VS-4 MVP)

Embed `http://localhost:3000` directly in the Electron shell via `<webview>` tag:

```svelte
<!-- src/panels/AgentManager.svelte -->
<script>
  const MULTICA_URL = 'http://localhost:3000';
</script>

<webview
  src={MULTICA_URL}
  style="width:100%;height:100%;border:none;"
  nodeintegration="false"
  partition="persist:multica"
/>
```

**Pros**: Zero implementation cost, always up to date with upstream Multica.  
**Cons**: No Morpheus visual theming, not a native 3D experience.

### Option B -- Native Svelte via WebSocket API (VS-4 Premium, recommended)

Subscribe to Multica's Go backend WebSocket (`ws://localhost:8080/ws`) and render agent activity natively in Svelte with Morpheus's liquid-glow aesthetic and 3D particle effects:

```typescript
// src/lib/multica-client.ts
export class MulticaClient {
  private ws: WebSocket;

  connect(serverUrl = "ws://localhost:8080") {
    this.ws = new WebSocket(`${serverUrl}/ws`);
    this.ws.onmessage = (event) => {
      const msg = JSON.parse(event.data);
      // Dispatch to Svelte store
      multicaStore.update(msg);
    };
  }

  async createIssue(title: string, agentName: string): Promise<string> {
    const res = await fetch("http://localhost:8080/api/issues", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: `Bearer ${getToken()}`,
      },
      body: JSON.stringify({ title, assignee: agentName }),
    });
    return (await res.json()).id;
  }
}
```

**Pros**: Full Metamorphosis visual treatment — agents "appear" with particle effects when picked up.  
**Cons**: Requires Multica REST/WS API mapping (needs CONTRIBUTING.md study first).

---

## Agent Presence Visual Design (Metamorphosis pillar)

When an agent picks up a task in Multica:

1. **Agent Avatar appears** in the OS Shell HUD with a liquid-glow pulse (GSAP animation)
2. **Progress bar** streams in real-time via WebSocket (replaces polling)
3. **Agent "goes dormant"** — avatar dissolves into a particle swarm and fades when task completes or agent idles

This is implemented in the Morpheus `AgentHUD.svelte` component (VS-4).

---

## Recommended Tab Structure (VS-4 OS Shell)

```
Morpheus OS Shell Tabs
├── Fleet Dashboard     (telemetry, P&L)
├── Strategy Editor     (Monaco + C# syntax)
├── Telemetry Stream    (fill-by-fill log)
├── Agent Manager       <- Multica board (this spec)
├── Config Panel        (strategy params)
└── The Forge           (Zed + Warp embed)
```

---

## API Surface (Go backend -- key endpoints)

Based on `CLI_AND_DAEMON.md`:

| Action              | Method | Endpoint                  |
| ------------------- | ------ | ------------------------- |
| List issues         | GET    | `/api/issues`             |
| Create issue        | POST   | `/api/issues`             |
| Assign agent        | PATCH  | `/api/issues/{id}/assign` |
| Issue status stream | WS     | `/ws`                     |
| List agents         | GET    | `/api/agents`             |
| Daemon runtimes     | GET    | `/api/runtimes`           |

Full API schema: browse `http://localhost:8080/swagger` once server is running.

---

## Prerequisites for VS-4

- [ ] Multica self-hosted server running on Morpheus machine (see `multica-setup.md`)
- [ ] Multica daemon running with claude/codex/gemini registered
- [ ] VS-3 (L1 Bridge + Schwab adapter) complete
- [ ] Electron shell scaffolded (VS-4 start)
- [ ] Decision on Option A vs B made by Director
