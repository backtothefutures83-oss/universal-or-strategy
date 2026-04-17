# scripts/test_stress.ps1
# WSGTA Readiness: Testing Pillar
# Formalizes the "Forensic Logic Trace" (AMAL Vetting Gate).

Write-Host "--- READINESS: Launching Forensic Logic Trace (Stress Test) ---" -ForegroundColor Yellow

if (Test-Path "scripts/amal_harness.py") {
    python scripts/amal_harness.py
} else {
    Write-Host "ERROR: amal_harness.py not found in scripts/." -ForegroundColor Red
    exit 1
}
