# Morpheus: Sovereign Identity Pillars

**Date**: 2026-04-18
**Author**: Antigravity (P1 Orchestrator) + Director
**Status**: ACTIVE -- Core Positioning
**Companion**: [morpheus-design.md](./2026-04-09-morpheus-design.md)

---

## Pillar I: Sovereign Transparency ("Clear Pipes")

### The Core Statement

> Morpheus doesn't decide what agents can see. Everything is wide open.
> You always know exactly what we pass. Nothing is a black box.

### Why This Matters

When a developer installs Claude Agent into JetBrains via the ACP Registry, the IDE decides what context to pass to the agent. The developer cannot know exactly what is or isn't included. The IDE may or may not pass:

- Open file(s)
- Terminal buffer (one terminal, if any)
- Diff state
- Project tree

What the IDE does NOT pass — and cannot, by design:

- All running terminal processes + PIDs
- How long each terminal has been running
- Which terminals house which agent CLIs
- What those agents are currently outputting
- The other agent's mission state, handoff context, or plan progress

**In Morpheus, there is no such ambiguity.**

Every turn between any agent and the Director explicitly includes:

```
Running terminal commands:
- claude   (in c:\WSGTA\universal-or-strategy, running for 1h17m0s)
- gemini   (background, 23m12s)
```

Every agent can call `read_terminal` and receive the live output of any named process. Every agent reads from the same `nexus_a2a.json` blackboard -- the same source of truth for mission phase, plan path, BUILD_TAG, and handoff state.

**There are no hidden pipes. Everything that runs in Morpheus is known, named, readable, and coordinated.**

### What This Unlocks

| Scenario                          | JetBrains + ACP |          Morpheus          |
| :-------------------------------- | :-------------: | :------------------------: |
| A knows B is running              |       No        |            Yes             |
| A can read B's output             |       No        |    Yes (read_terminal)     |
| A knows B's mission phase         |       No        |    Yes (nexus_a2a.json)    |
| A hands off to B with context     |       No        | Yes (structured A2A relay) |
| Director sees all agents at once  |       No        | Yes (metadata every turn)  |
| Session state survives agent swap |       No        | Yes (blackboard persists)  |

### The Positioning Line

> Other platforms decide what their agents can see.
> Morpheus agents see everything -- and so do you.

---

## Pillar II: Infinite Configurability ("Rubix Cube")

### The Core Statement

> Morpheus is a substrate, not a product.
> Like a Rubix Cube, it can be twisted into an endless number of configurations.
> Every configuration resolves to a valid, governed state.

### The Analogy

A Rubix Cube has:

- Fixed pieces (the agents: Claude, Codex, Gemini, Jules, Antigravity, Droid)
- Fixed rules for what moves are legal (Director's Gate, ADR protocol)
- An infinite number of valid configurations from those rules
- A known solved state (100% consensus, P4 execution authorized)

Morpheus is the same:

- **Fixed pieces**: The 6-agent fleet is the permanent substrate
- **Fixed rules**: Director's Gate hierarchy, P1→P3→P4 gate, A2A consensus
- **Infinite configurations**: Any combination of agents, harnesses, missions, and broker adapters
- **Known solved state**: 100% Arena consensus + Director sign-off = authorized execution

### What Can Be Swapped (Without Breaking the Substrate)

```
TRADING LAYER:    NinjaTrader ↔ Schwab TOS ↔ Binance ↔ XRPL ↔ any IBrokerAdapter
INTELLIGENCE:     Claude ↔ Codex ↔ Gemini ↔ Jules ↔ any ACP-registered agent
HARNESS:          P3 Architect ↔ P4 Engineer ↔ P1 Orchestrator ↔ custom role
MISSION:          ADR-019 ↔ ADR-020 ↔ any future mission -- swap in one nexus write
MARKET:           Futures ↔ Equities ↔ Crypto ↔ Prediction markets ↔ DeFi
VISUALIZATION:    Arena Dashboard ↔ 3D Topology ↔ Spatial Computing ↔ any shell
STRATEGY:         OR ↔ FFMA ↔ RMA ↔ MOMO ↔ Trend ↔ any IStrategyProducer
```

**None of these swaps require changing the kernel. The substrate absorbs them.**

### Relationship to Metamorphosis

Metamorphosis (see `morpheus-design.md`) is the **visual expression** of this pillar -- the UI decomposing into smoke and reassembling when you switch context. The Rubix Cube pillar is the **architectural expression**: the system can reach any configuration and every configuration is governed, valid, and observable.

### The Positioning Line

> Every configuration is a solved state.
> Morpheus adapts to you, not the other way around.

---

## How the Pillars Work Together

```
CLEAR PIPES                        RUBIX CUBE
(Sovereign Transparency)           (Infinite Configurability)

"You always know what              "You can arrange it any way
 agents can see and do."            you need."

         |                                  |
         +-----------> Morpheus <-----------+
                      Substrate

Agents see everything.             Any configuration
Nothing is hidden.                 resolves to a governed state.
```

The two pillars reinforce each other: you can configure Morpheus in any shape because the transparent pipes mean you always know the current state of every piece. No hidden dependencies to break when you swap.

---

## Against the Competition

| Platform          | Transparency           | Configurability           | Multi-Agent                            |
| :---------------- | :--------------------- | :------------------------ | :------------------------------------- |
| JetBrains + ACP   | IDE decides            | Plugin-only               | No (one agent, one user)               |
| Cursor / Windsurf | Partial (file context) | Limited                   | No                                     |
| GitHub Copilot    | Black box              | None                      | No                                     |
| Devin             | Black box              | None                      | Simulated (one model)                  |
| **Morpheus**      | **Full (clear pipes)** | **Infinite (Rubix Cube)** | **Yes (named, readable, coordinated)** |

---

_Last Updated: 2026-04-18 17:25 UTC_
_Status: APPROVED as core positioning doctrine_
