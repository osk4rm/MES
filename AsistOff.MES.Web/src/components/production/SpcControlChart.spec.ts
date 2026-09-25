import { describe, expect, it } from 'vitest';
import { mount } from '@vue/test-utils';
import SpcControlChart from './SpcControlChart.vue';
import type { SpcMeasurementChartPoint } from '../../services/spcMeasurementService';

function point(overrides: Partial<SpcMeasurementChartPoint> = {}): SpcMeasurementChartPoint {
  return {
    id: 'point-1',
    value: 10,
    measuredAt: new Date('2026-09-24T10:00:00Z').toISOString(),
    isOutOfControl: false,
    isOutOfSpec: false,
    ...overrides
  };
}

function mountChart(props: Record<string, unknown> = {}) {
  return mount(SpcControlChart, {
    props: {
      points: [
        point({ id: 'point-1', value: 10 }),
        point({ id: 'point-2', value: 10.2 }),
        point({ id: 'point-3', value: 9.8 })
      ],
      lowerControlLimit: 9.5,
      upperControlLimit: 10.5,
      lowerSpecLimit: 9,
      upperSpecLimit: 11,
      nominalValue: 10,
      ...props
    }
  });
}

describe('SpcControlChart', () => {
  it('renders one point per measurement in ascending time order', () => {
    const wrapper = mountChart({
      points: [
        point({ id: 'point-1', value: 10, measuredAt: new Date('2026-09-24T10:00:00Z').toISOString() }),
        point({ id: 'point-2', value: 10.2, measuredAt: new Date('2026-09-24T10:01:00Z').toISOString() }),
        point({ id: 'point-3', value: 9.8, measuredAt: new Date('2026-09-24T10:02:00Z').toISOString() })
      ]
    });

    const circles = wrapper.findAll('circle.spc-chart__point');
    expect(circles).toHaveLength(3);

    const xs = circles.map((c) => Number(c.attributes('cx')));
    expect(xs[0]).toBeLessThan(xs[1] as number);
    expect(xs[1]).toBeLessThan(xs[2] as number);
  });

  it('sorts an unsorted payload by measuredAt so X stays in ascending time order', () => {
    const wrapper = mountChart({
      points: [
        point({ id: 'point-late', value: 9.8, measuredAt: new Date('2026-09-24T10:02:00Z').toISOString() }),
        point({ id: 'point-early', value: 10, measuredAt: new Date('2026-09-24T10:00:00Z').toISOString() }),
        point({ id: 'point-mid', value: 10.2, measuredAt: new Date('2026-09-24T10:01:00Z').toISOString() })
      ]
    });

    const circles = wrapper.findAll('circle.spc-chart__point');
    expect(circles).toHaveLength(3);

    const xs = circles.map((c) => Number(c.attributes('cx')));
    expect(xs[0]).toBeLessThan(xs[1] as number);
    expect(xs[1]).toBeLessThan(xs[2] as number);

    // DOM order follows time order, not prop order: earliest first.
    expect(circles[0]?.attributes('data-testid')).toBe('spc-point-point-early');
    expect(circles[1]?.attributes('data-testid')).toBe('spc-point-point-mid');
    expect(circles[2]?.attributes('data-testid')).toBe('spc-point-point-late');
  });

  it('marks points beyond control limits as out of control', () => {
    const wrapper = mountChart({
      points: [
        point({ id: 'point-ok', value: 10 }),
        point({ id: 'point-ooc', value: 12, isOutOfControl: true, isOutOfSpec: true })
      ]
    });

    const ok = wrapper.find('[data-testid="spc-point-point-ok"]');
    const ooc = wrapper.find('[data-testid="spc-point-point-ooc"]');

    expect(ok.classes()).not.toContain('spc-chart__point--out-of-control');
    expect(ooc.classes()).toContain('spc-chart__point--out-of-control');
    expect(ooc.attributes('data-out-of-control')).toBe('true');
  });

  it('marks points beyond spec limits as out of spec without requiring control limits', () => {
    const wrapper = mountChart({
      points: [
        point({ id: 'point-ok', value: 10 }),
        point({ id: 'point-oos', value: 10.8, isOutOfControl: false, isOutOfSpec: true })
      ],
      lowerControlLimit: null,
      upperControlLimit: null
    });

    const oos = wrapper.find('[data-testid="spc-point-point-oos"]');
    expect(oos.classes()).toContain('spc-chart__point--out-of-spec');
    expect(oos.classes()).not.toContain('spc-chart__point--out-of-control');
    // Absent control limits draw no control lines.
    expect(wrapper.find('[data-testid="spc-limit-ucl"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="spc-limit-lcl"]').exists()).toBe(false);
  });

  it('draws limit lines only for limits that exist', () => {
    const wrapper = mountChart({
      lowerControlLimit: null,
      upperControlLimit: 10.5,
      lowerSpecLimit: null,
      upperSpecLimit: 11,
      nominalValue: null
    });

    expect(wrapper.find('[data-testid="spc-limit-ucl"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="spc-limit-usl"]').exists()).toBe(true);
    expect(wrapper.find('[data-testid="spc-limit-lcl"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="spc-limit-lsl"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="spc-limit-nominal"]').exists()).toBe(false);
  });

  it('shows the empty state instead of a broken chart when no measurements exist', () => {
    const wrapper = mountChart({ points: [] });

    expect(wrapper.find('[data-testid="spc-chart-svg"]').exists()).toBe(false);
    expect(wrapper.find('[data-testid="spc-chart-empty"]').exists()).toBe(true);
  });
});
