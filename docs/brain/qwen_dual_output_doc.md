# 🤖 Qwen Code Dual Output Feature Guide

Dual Output is a sidecar mode for the interactive TUI: while Qwen Code keeps rendering normally on stdout, it concurrently emits a structured JSON event stream to a separate channel so an external program — an IDE extension, a web frontend, a CI pipeline, an automation script — can observe and steer the session.

It also provides a reverse channel: an external program can write JSONL commands into a file that the TUI watches, allowing it to submit prompts and respond to tool-permission requests as if a human were at the keyboard.

Dual Output is fully optional. When the flags below are absent the TUI behaves exactly as before with no extra I/O and no behavioral changes.

---

## Use Cases
Dual Output is a low-level plumbing primitive. These are concrete integrations it unlocks:

1. **Terminal + Chat dual-mode real-time sync**
   A web or desktop ChatUI hosts the TUI inside a PTY and renders a parallel conversation view driven by the structured event stream:
   - User can type in either surface — the TUI (for terminal-native power-users) or the web UI. Both views stay in sync because every message flows through the same JSON events.
   - Tool-approval prompts appear in both places; whoever approves first wins.
   - Session history is captured verbatim from `--json-file`, so the server side has a canonical machine-readable transcript without parsing ANSI.

2. **IDE extensions (VS Code / JetBrains / Cursor / Neovim)**
   Embed Qwen Code inside the IDE. The TUI runs in the editor’s integrated terminal panel for users who want it, while the extension consumes `--json-fd` / `--json-file` events to drive:
   - Inline diff overlays when the agent touches files.
   - A webview side panel with formatted markdown, syntax-highlighted tool calls, and clickable citations.
   - Status bar indicators (thinking / responding / awaiting approval).
   - Programmatic `confirmation_response` writes when the user clicks a native IDE approval button.

3. **Browser-based Chat frontends**
   A Node/Bun server spawns the TUI in a PTY for its rendering semantics but exposes a WebSocket channel to the browser. Events on `--json-file` are forwarded to the client; user messages typed in the browser are injected via `--input-file`. No ANSI parsing on either side.

4. **CI / automation observers**
   A CI job runs Qwen Code with a task prompt. The human sees the TUI in the job log; the CI system tails `--json-file` to:
   - Fail the job if a result event reports an error.
   - Push token usage / `duration_ms` / tool_use counts to metrics.
   - Archive the full transcript as a build artifact.

5. **Multi-agent orchestration**
   A supervisor agent spawns multiple TUI workers, each with its own pair of event/input files. It watches progress, injects follow-up prompts, and enforces global budget / safety policies by approving or denying tool calls across all workers.

6. **Session recording, audit, and replay**
   Tee every TUI session to a regular file with `--json-file`. Later:
   - Compliance audits can reconstruct exactly what was executed.
   - Automated regression tests can compare runs across model versions.
   - A replay tool can re-emit events through the same protocol to feed visualization dashboards.

7. **Observability dashboards**
   Stream `--json-file` into Loki / OTEL / any pipeline that accepts JSONL. Extract `usage.input_tokens`, `tool_use.name`, `result.duration_api_ms` as first-class metrics in Grafana without log-parsing regex.

8. **Testing and QA**
   Integration tests spawn Qwen Code headlessly, drive it with `--input-file` scripts, and assert on `--json-file` events. Unlike parsing stdout ANSI, assertions are stable across UI refactors.

---

## Flags

| Flag | Type | Purpose |
|:---|:---|:---|
| `--json-fd <n>` | number, `n >= 3` | Write structured JSON events to file descriptor `n`. The caller must provide this fd via spawn stdio configuration or shell redirection. |
| `--json-file <path>` | path | Write structured JSON events to a file. The path can be a regular file, a FIFO (named pipe), or `/dev/fd/N`. |
| `--input-file <path>` | path | Watch this file for JSONL commands written by an external program. |

*Note: `--json-fd` and `--json-file` are mutually exclusive. fds 0, 1, and 2 are rejected to prevent corrupting the TUI’s own output.*

---

## Why two output flags? (`--json-fd` vs `--json-file`)
At first glance `--json-fd` looks sufficient — the caller spawns Qwen Code with an extra file descriptor, the TUI writes events to it, done. In practice, fd passing breaks down under the most important embedding scenario: running the TUI inside a pseudo-terminal (PTY). That is why this feature also exposes a path-based alternative.

### When `--json-fd` works
Pure `child_process.spawn` with a stdio array:
```javascript
const child = spawn('qwen', ['--json-fd', '3'], {
  stdio: ['inherit', 'inherit', 'inherit', eventsFd],
});
```
Node’s spawn supports arbitrary stdio entries; fd 3 is inherited by the child, which can write to it directly. Zero-copy, zero-buffer, zero filesystem — the fastest path.

### Why `--json-fd` does not work under PTY
PTY wrappers like `node-pty` and `bun-pty` are how any serious embedder hosts an interactive TUI. They cannot forward extra fds to the child, for three reinforcing reasons:
1. **API surface**: `node-pty.spawn(file, args, options)` accepts cwd, env, cols, rows, encoding, etc. — but no stdio array. There is simply no place in the API to say “also attach this fd as fd 3 in the child”. `bun-pty` exposes the same shape.
2. **`forkpty(3)` semantics**: Under the hood, PTY wrappers call `forkpty(3)`. That syscall allocates a master/slave pseudo-terminal pair and redirects the child’s fds 0/1/2 to the slave side. Any fds above 2 in the parent are closed by `login_tty`, which calls `close(fd)` for `fd >= 3` before `exec`. Extra fds are actively wiped, not inherited.
3. **Controlling-terminal side effect**: Even if you hacked an extra fd through, it would not be a terminal, so the child’s TUI renderer would still need the slave for its output. You would end up with two independent transports anyway.

### `--json-file` fills the gap
A file path is passed as an ordinary CLI argument, so it survives every spawn model:
```javascript
import { spawn } from 'node-pty';
 
const pty = spawn(
  'qwen',
  [
    '--json-file',
    '/tmp/qwen-events.jsonl',
    '--input-file',
    '/tmp/qwen-input.jsonl',
  ],
  { cols: 120, rows: 40 },
);
```
The child opens the file itself and writes events there; the embedder tails the same path with `fs.watch` + incremental reads.
- Regular file, FIFO (named pipe), or `/dev/fd/N` all work. FIFO is the lowest-latency option when both sides are on the same host.
- The bridge opens FIFOs with `O_NONBLOCK` and falls back to blocking mode on `ENXIO` (no reader yet), so PTY startup is never deadlocked waiting for a consumer.
- For multi-session isolation, use per-session paths under `$XDG_RUNTIME_DIR` or a `mkdtemp`’d directory with mode `0700`.

---

## Output Event Schema
Events are emitted as JSON Lines (one object per line). 

### Session Start Event (First Event)
```json
{
  "type": "system",
  "subtype": "session_start",
  "uuid": "...",
  "session_id": "...",
  "data": { "session_id": "...", "cwd": "/path/to/cwd" }
}
```

### Streaming Events (In-progress assistant turn)
```json
{ "type": "stream_event", "event": { "type": "message_start", "message": { "role": "assistant", "content": [] } } }
{ "type": "stream_event", "event": { "type": "content_block_start", "index": 0, "content_block": { "type": "text" } } }
{ "type": "stream_event", "event": { "type": "content_block_delta", "index": 0, "delta": { "type": "text_delta", "text": "Hello" } } }
{ "type": "stream_event", "event": { "type": "content_block_stop", "index": 0 } }
{ "type": "stream_event", "event": { "type": "message_stop" } }
```

### Completed Messages
```json
{ "type": "user", "message": { "role": "user", "content": [...] } }
{ "type": "assistant", "message": { "role": "assistant", "content": [...], "usage": { "input_tokens": 120, "output_tokens": 45 } } }
```

### Permission Control Plane (When tool needs approval)
```json
{
  "type": "control_request",
  "request_id": "...",
  "request": {
    "subtype": "can_use_tool",
    "tool_name": "run_shell_command",
    "tool_use_id": "...",
    "input": { "command": "rm -rf /tmp/x" },
    "permission_suggestions": null,
    "blocked_path": null
  }
}
```

---

## Input Command Schema
Two command shapes are accepted on `--input-file`:

### Submit Prompt
```json
{ "type": "submit", "text": "What does this function do?" }
```

### Reply to control_request (Tool Approval)
```json
{ "type": "confirmation_response", "request_id": "...", "allowed": true }
```

### Latency Notes
The input file is observed with `fs.watchFile` at a **500 ms polling interval**, so worst-case round-trip latency for a remote submit is about half a second. This is intentional: polling is portable across platforms and filesystems (including macOS / network mounts). The output channel has no polling — events are written synchronously as the TUI emits them.

---

## Settings-based Configuration
The same channels can be configured in `settings.json` under the top-level `dualOutput` key:
```json
{
  "dualOutput": {
    "jsonFile": "/tmp/qwen-events.jsonl",
    "inputFile": "/tmp/qwen-input.jsonl"
  }
}
```
*Note: CLI flags override settings. Changing settings requires a restart to take effect.*

---

## Runnable Demos

### POC 1 — Observe the Event Stream
```bash
# Terminal A
mkfifo /tmp/qwen-events.jsonl
cat /tmp/qwen-events.jsonl | jq -c 'select(.type != "stream_event") | {type, subtype}'
 
# Terminal B
qwen --json-file /tmp/qwen-events.jsonl
```

### POC 2 — Inject Prompts from Outside
```bash
# Terminal A
touch /tmp/qwen-in.jsonl
qwen --input-file /tmp/qwen-in.jsonl
 
# Terminal B
echo '{"type":"submit","text":"list files in the current directory"}' >> /tmp/qwen-in.jsonl
```

### POC 3 — Remote Tool-Permission Bridge
```bash
# Terminal A — observe control_requests
mkfifo /tmp/qwen-out.jsonl
touch /tmp/qwen-in.jsonl
(cat /tmp/qwen-out.jsonl | jq -c 'select(.type == "control_request")') &
 
# Terminal B
qwen --json-file /tmp/qwen-out.jsonl --input-file /tmp/qwen-in.jsonl

# In Terminal C, copy the request_id and respond:
echo '{"type":"confirmation_response","request_id":"<paste-id>","allowed":true}' >> /tmp/qwen-in.jsonl
```

---

### POC 4 — Node Embedder (IDE-like)
```typescript
// demo-embedder.ts
import { spawn } from 'node:child_process';
import { appendFileSync, createReadStream, writeFileSync } from 'node:fs';
import { createInterface } from 'node:readline';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
 
const events = join(tmpdir(), `qwen-events-${process.pid}.jsonl`);
const input = join(tmpdir(), `qwen-input-${process.pid}.jsonl`);
writeFileSync(events, '');
writeFileSync(input, '');
 
const child = spawn('qwen', ['--json-file', events, '--input-file', input], {
  stdio: 'inherit',
});
 
const rl = createInterface({
  input: createReadStream(events, { encoding: 'utf8' }),
});

rl.on('line', (line) => {
  if (!line.trim()) return;
  const ev = JSON.parse(line);
  if (ev.type === 'system' && ev.subtype === 'session_start') {
    console.log('[embedder] handshake:', {
      protocol_version: ev.data.protocol_version,
      version: ev.data.version,
      supported_events: ev.data.supported_events,
    });
    if (ev.data.supported_events.includes('control_request')) {
      console.log('[embedder] permission control-plane available');
    }
  }
  if (ev.type === 'assistant') {
    console.log('[embedder] assistant turn ended, tokens =', ev.message.usage?.output_tokens);
  }
  if (ev.type === 'system' && ev.subtype === 'session_end') {
    console.log('[embedder] session ended cleanly');
  }
});
 
setTimeout(() => {
  appendFileSync(input, JSON.stringify({ type: 'submit', text: 'hello from embedder' }) + '\n');
}, 2000);
 
child.on('exit', () => process.exit(0));
```
Run with:
```bash
npx tsx demo-embedder.ts
```
