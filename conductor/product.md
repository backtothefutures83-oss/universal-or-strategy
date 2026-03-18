# Initial Concept
Universal OR Strategy V12

# Universal OR Strategy V12 - Product Guide

## Vision
To provide a hardened, robust, and symmetrical trading fleet architecture (1 Master + 18 Followers) on NinjaTrader 8. The system guarantees 100% bracket symmetry during high-volatility Opening Range (OR) and RMA entries while surviving broker connection flickers without data loss or ghost orders.

## Core Features
- **Actor Mailbox Architecture**: Lock-free concurrent queue processing for broker events.
- **Unbreakable Per-Follower FSMs**: State transitions happen only on broker confirmations.
- **Audit Layer (Safety Hub)**: Asymmetric authority with Ghost Order cleanup, Naked Position protection, and Stale FSM force-syncs.
- **Degraded Mode Protection**: Skip-and-protect resilience against Rithmic connection drops.
- **Robust IPC Protocol**: External command integration with sub-minute timestamp deduplication and MetadataGuard gating.

## Target Environment
- Platform: NinjaTrader 8
- Broker/Routing: Apex / Rithmic
