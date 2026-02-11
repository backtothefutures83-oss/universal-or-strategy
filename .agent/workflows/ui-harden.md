---
description: Implement Fluid UI Hardening to fix clipping and align with NinjaTrader standards.
---

# UI Hardening Workflow

Use this workflow to fix side-panel clipping issues by switching from fixed-width "trays" to a fluid, stretching layout.

## Steps

1. **Set Global Standards**:
   - Update `OnStateChange` (or constructor) to set `PanelWidth = 210`.
   - This aligns the custom panel with the native NinjaTrader Chart Trader width.

2. **Unlock the Shell**:
   - Locate `rootContainer` and `contentBody` initializations.
   - Remove explicit width assignments (`Width = PanelWidth`).
   - Set `Width = double.NaN`.
   - Set `HorizontalAlignment = HorizontalAlignment.Stretch`.

3. **Fluidize Internal Rows**:
   - Audit all `Grid` and `StackPanel` rows (e.g., `row1`, `row3`, `fleetRow`).
   - Remove any hardcoded `.Width = ...` assignments (especially the old 238px trays).
   - Ensure `HorizontalAlignment = HorizontalAlignment.Stretch` is set on all containers.

4. **Adaptive Columns**:
   - Ensure `Grid.ColumnDefinitions` use `Star` (`1*`) or `Auto` instead of fixed pixel values where possible.

5. **Verify with Visual Tree**:
   - Run the built-in `DumpVisualTree()` method.
   - Check the NinjaTrader Output window to confirm `ActualWidth` matches the slot width and no "bleed" occurs on the left.
