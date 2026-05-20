# V12 Strategy Sentry Runtime Setup & Operation Guide

This document defines the procedure for deploying, configuring, and operating Sentry.NET SDK integration in the **V12 Universal OR Strategy** environment.

---

## 1. DSN Configuration

Sentry initialization is completely dynamic and resolves via the system environment. To prevent credential leakage, the DSN must **never** be committed to version control.

### Setting the Environment Variable (Windows)

1. Open **System Properties** > **Environment Variables** (or run `sysdm.cpl`).
2. Under **System Variables** (recommended for headless execution) or **User Variables**, click **New**.
3. Set the Variable Name to: `V12_SENTRY_DSN`
4. Set the Variable Value to your active Sentry DSN URL.
5. Restart **NinjaTrader 8** to ensure the new environment variable is loaded by the NinjaTrader process.

*Alternatively, set it via PowerShell before launching NinjaTrader:*
```powershell
[System.Environment]::SetEnvironmentVariable("V12_SENTRY_DSN", "https://sentry.io/your-project-id", "User")
```

### Rotating the DSN
Because the DSN resolves dynamically at initialization, you can rotate the key at any time by updating the `V12_SENTRY_DSN` environment variable and restarting NinjaTrader. No code compilation or DLL redeployment is required.

---

## 2. Dependency Deployment (NinjaTrader 8 bin Folder)

Because NinjaTrader 8 executes dynamically compiled C# scripts under its own AppDomain, Sentry assemblies must reside in the NinjaTrader 8 directory to compile and load correctly.

### Assembly Placement

Place the following assemblies in your NinjaTrader 8 bin folder (typically `C:\Program Files\NinjaTrader 8\bin\`):

- `Sentry.dll` (v4.13.0)
- `Sentry.Microsoft.Bcl.AsyncInterfaces.dll` (if packaging dependencies)
- `System.Text.Json.dll` (matching the version expected by Sentry)

Once copied, when NinjaTrader 8 compiles the Custom strategies folder (pressing **F5** in the NinjaScript Editor), it will reference the copied assemblies.

---

## 3. Dynamic Build Tags & Telemetry Matching

All telemetry captured by Sentry is automatically tagged with the current active build information:

* **Build Tag**: Captured dynamically from `V12_002.BUILD_TAG` (e.g., `1111.007-readiness-L5`).
* **Environment**: Defaults to `production`.
* **Forensic Tags**: Every captured exception or rejection is tagged with a `forensic.tag` specifying the architectural boundary:
  - `photon.mmio.init`: MMIO ring mirror allocation/mapping failures.
  - `reaper.timer.marshal`: REAPER safety thread timer marshalling and event dispatch failure.
  - `orders.follower.submit`: Follower entry/target submission exception thrown by the NinjaTrader core.
  - `sima.mutate`: Thread-safe state mutation anomalies (e.g. from IPC anchor commands).
  - `ipc.reject.malformed`: Malformed, oversize, or rejected IPC packets rejected before parsing.

---

## 4. Verification & Diagnostics

During strategy load (State.Configure), the strategy will attempt to read the DSN:

* If **configured successfully**: The strategy logs `[V12 SENTRY] Initialized successfully. Release: <BUILD_TAG>` in the NinjaTrader Output window.
* If **missing**: The strategy prints a single informational log: `[V12 SENTRY] V12_SENTRY_DSN not configured. Running without Sentry.` and runs normally.
* If **incorrect/corrupted**: Sentry will gracefully catch its own internal initialization failure and print `[V12 SENTRY] Initialization failed: <Message>` without interrupting trading logic.
