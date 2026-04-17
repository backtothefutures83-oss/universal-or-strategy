name: Flag Bug
description: Report a logic flaw, race condition, or "Silent Killer"
labels: ["bug", "audit-fail"]
body:

- type: markdown
  attributes:
  value: |
  Act with **Zero-Trust**. Provide forensic evidence for the suspected risk.
- type: textarea
  id: diagnosis
  attributes:
  label: Forensic Diagnosis
  description: What is the "Silent Killer" or logic breach?
  placeholder: "e.g., Race condition in OnAccountOrderUpdate during high volatility..."
  validations:
  required: true
- type: textarea
  id: logs
  attributes:
  label: Proof of Failure (Logs/Traces)
  description: Attach the log snippet or stress test output.
  validations:
  required: true
