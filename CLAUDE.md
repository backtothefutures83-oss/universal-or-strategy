# CLAUDE.md - BMad Project Standards & Safety Guide

## 🚩 Project Overview
**Universal OR Strategy (V12)**: A high-integrity institutional fleet trading strategy for NinjaTrader 8.

## 🛡️ Zero-Trust Protocols (MANDATORY)
1. **IPC Security**: All listeners must bind to Loopback (`127.0.0.1`). Malformed input must be rejected with `V12 IPC REJECT` logs.
2. **Input Validation**: Never trust incoming network payloads. Use strict UTF-8 decoding and bounded command lengths.
3. **Fleet Privacy**: Obscure sensitive account names using BMad aliases (`F01`, `F02`, etc.) in all external-facing responses.

## 🦍 Logic Integrity (FLEET SAFETY)
1. **SIMA Synchronicity**: All fleet dispatches must use the `_dispatchSyncPendingExpKeys` barrier.
2. **Ghost-Order Prevention**: Use **Signed Delta Rollbacks** for expected position cleanup; never use blanket zeroing.
3. **REAPER Bounds**: Repairs must be capped by both ATR-volatility and hard tick fences.
4. **Symmetry Gating**: Follower brackets must wait for the master "Anchor" price before submission.

## 🏷️ Naming Conventions
- **Build Tags**: Must be incremented in `V12_002.Properties.cs` for every production delivery.
- **Prefixes**: All files and primary classes use `V12_001` (Panel) or `V12_002` (Strategy).


## CRITICAL: ASCII-Only in All C# String Literals
- NEVER use emoji, curly quotes, em-dashes, Unicode arrows, or box-drawing in Print() or any string literal.
- Non-ASCII inside C# strings breaks the NinjaTrader compiler with 300+ cascading errors (Build 936 incident).
- Allowed substitutions: (!) not emoji, -- not em-dash, -> not arrow, straight " not curly " "
- See .agent/standards_manifesto.md Section 7 for the full rule table and emergency fix sequence.
