import { describe, expect, it } from 'vitest';

// Slice (3/3) F-06 rollout evidence: telemetry readings, SPC measurement
// ranges, scrap/downtime/Andon/operator-panel timestamps all use the shared
// touch-friendly date-time field (labelled, 44 px targets, local-time hint
// plus UTC conversion note).

describe('shared date-time rollout (F-06)', () => {
  it.each([
    './ScrapView.vue',
    './DowntimeView.vue',
    './AndonView.vue',
    './OperatorPanelView.vue',
    './ProductionOrderDetailView.vue',
    './TelemetryView.vue',
    './SpcCharacteristicsView.vue'
  ])('%s uses AppDateTimeField', (file) => {
    const text = rolloutSources[file] ?? '';
    expect(text.length).toBeGreaterThan(0);
    expect(text).toContain('AppDateTimeField');
  });

  it('no bare datetime-local AppInput remains in the rolled-out forms', () => {
    // Telemetry reading form, SPC measurement range, Andon resolve/raise,
    // downtime close and operator-panel dialogs must not bypass the field.
    for (const file of [
      './TelemetryView.vue',
      './SpcCharacteristicsView.vue',
      './AndonView.vue',
      './OperatorPanelView.vue'
    ]) {
      const text = rolloutSources[file] ?? '';
      expect(text).not.toContain('type="datetime-local"');
    }
  });
});

const rolloutSources = import.meta.glob<string>(
  [
    './ScrapView.vue',
    './DowntimeView.vue',
    './AndonView.vue',
    './OperatorPanelView.vue',
    './ProductionOrderDetailView.vue',
    './TelemetryView.vue',
    './SpcCharacteristicsView.vue'
  ],
  { eager: true, query: '?raw', import: 'default' }
);
