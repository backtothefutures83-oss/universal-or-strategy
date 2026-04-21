name: Hardening Request
description: Propose an architectural improvement or new feature
labels: ["enhancement", "hardening"]
body:

- type: textarea
  id: goal
  attributes:
  label: Hardening Goal
  description: What structural improvement are we implementing?
  validations:
  required: true
- type: textarea
  id: impact
  attributes:
  label: Institutional Impact
  description: How does this improve reliability or performance?
  validations:
  required: true
