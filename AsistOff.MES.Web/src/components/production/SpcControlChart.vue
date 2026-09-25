<template>
  <div class="spc-chart" data-testid="spc-chart">
    <svg
      v-if="hasPoints"
      class="spc-chart__svg"
      viewBox="0 0 600 260"
      role="img"
      :aria-label="ariaLabel"
      data-testid="spc-chart-svg"
    >
      <line
        v-for="limit in limitLines"
        :key="limit.key"
        :x1="padLeft"
        :x2="width - padRight"
        :y1="limit.y"
        :y2="limit.y"
        :class="['spc-chart__limit', `spc-chart__limit--${limit.key}`]"
        :data-testid="`spc-limit-${limit.key}`"
        :data-value="limit.value"
      />
      <polyline
        :points="seriesPoints"
        class="spc-chart__series"
        data-testid="spc-chart-series"
        fill="none"
      />
      <circle
        v-for="p in plotted"
        :key="p.point.id"
        :cx="p.x"
        :cy="p.y"
        r="4.5"
        :class="pointClasses(p.point)"
        :data-testid="`spc-point-${p.point.id}`"
        :data-out-of-control="p.point.isOutOfControl"
        :data-out-of-spec="p.point.isOutOfSpec"
      >
        <title>{{ pointTitle(p.point) }}</title>
      </circle>
    </svg>
    <p v-else class="spc-chart__empty" data-testid="spc-chart-empty">{{ emptyLabel }}</p>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { SpcMeasurementChartPoint } from '../../services/spcMeasurementService';

const props = withDefaults(defineProps<{
  points: SpcMeasurementChartPoint[];
  lowerControlLimit?: number | null;
  upperControlLimit?: number | null;
  lowerSpecLimit?: number | null;
  upperSpecLimit?: number | null;
  nominalValue?: number | null;
  ariaLabel?: string;
  emptyLabel?: string;
}>(), {
  lowerControlLimit: null,
  upperControlLimit: null,
  lowerSpecLimit: null,
  upperSpecLimit: null,
  nominalValue: null,
  ariaLabel: 'SPC control chart',
  emptyLabel: 'No measurements'
});

const width = 600;
const height = 260;
const padLeft = 48;
const padRight = 16;
const padTop = 16;
const padBottom = 32;

const hasPoints = computed(() => props.points.length > 0);

interface Domain { min: number; max: number }

const domain = computed<Domain>(() => {
  const candidates: number[] = props.points.map((p) => p.value);
  for (const v of [props.lowerControlLimit, props.upperControlLimit, props.lowerSpecLimit, props.upperSpecLimit, props.nominalValue]) {
    if (v !== null && v !== undefined && Number.isFinite(v)) candidates.push(v);
  }
  if (candidates.length === 0) return { min: 0, max: 1 };
  let min = Math.min(...candidates);
  let max = Math.max(...candidates);
  if (min === max) {
    const delta = Math.abs(min) > 0 ? Math.abs(min) * 0.1 : 1;
    min -= delta;
    max += delta;
  } else {
    const span = max - min;
    min -= span * 0.08;
    max += span * 0.08;
  }
  return { min, max };
});

function yOf(value: number): number {
  const { min, max } = domain.value;
  const inner = height - padTop - padBottom;
  const ratio = (value - min) / (max - min || 1);
  return padTop + inner * (1 - ratio);
}

function xOf(index: number, total: number): number {
  const inner = width - padLeft - padRight;
  if (total <= 1) return padLeft + inner / 2;
  return padLeft + (inner * index) / (total - 1);
}

interface PlottedPoint { point: SpcMeasurementChartPoint; x: number; y: number }

// The chart X axis is time: sort a copy by measuredAt so an unsorted API
// payload still renders in ascending time order instead of prop order.
const orderedPoints = computed<SpcMeasurementChartPoint[]>(() =>
  [...props.points].sort((a, b) => {
    const ta = Date.parse(a.measuredAt);
    const tb = Date.parse(b.measuredAt);
    if (Number.isNaN(ta) || Number.isNaN(tb)) return 0;
    return ta - tb;
  })
);

const plotted = computed<PlottedPoint[]>(() =>
  orderedPoints.value.map((point, index) => ({
    point,
    x: xOf(index, orderedPoints.value.length),
    y: yOf(point.value)
  }))
);

const seriesPoints = computed(() =>
  plotted.value.map((p) => `${p.x.toFixed(2)},${p.y.toFixed(2)}`).join(' ')
);

interface LimitLine { key: string; value: number; y: number }

const limitLines = computed<LimitLine[]>(() => {
  const entries: Array<{ key: string; value: number | null | undefined }> = [
    { key: 'ucl', value: props.upperControlLimit },
    { key: 'lcl', value: props.lowerControlLimit },
    { key: 'usl', value: props.upperSpecLimit },
    { key: 'lsl', value: props.lowerSpecLimit },
    { key: 'nominal', value: props.nominalValue }
  ];
  return entries
    .filter((e): e is { key: string; value: number } => e.value !== null && e.value !== undefined && Number.isFinite(e.value))
    .map((e) => ({ key: e.key, value: e.value, y: yOf(e.value) }));
});

function pointClasses(point: SpcMeasurementChartPoint): string[] {
  const classes = ['spc-chart__point'];
  if (point.isOutOfControl) classes.push('spc-chart__point--out-of-control');
  if (point.isOutOfSpec) classes.push('spc-chart__point--out-of-spec');
  return classes;
}

function pointTitle(point: SpcMeasurementChartPoint): string {
  const flags: string[] = [];
  if (point.isOutOfControl) flags.push('out of control');
  if (point.isOutOfSpec) flags.push('out of spec');
  return flags.length > 0 ? `${point.value} (${flags.join(', ')})` : String(point.value);
}
</script>

<style scoped>
.spc-chart__svg {
  width: 100%;
  height: auto;
  display: block;
  background: var(--color-bg, transparent);
}
.spc-chart__series {
  stroke: var(--color-info);
  stroke-width: 2;
}
.spc-chart__point {
  fill: var(--color-info);
  stroke: var(--color-bg, #fff);
  stroke-width: 1.5;
}
.spc-chart__point--out-of-control {
  fill: var(--color-danger);
  stroke-width: 2;
}
.spc-chart__point--out-of-spec {
  stroke: var(--color-warning);
  stroke-width: 2.5;
}
.spc-chart__point--out-of-control.spc-chart__point--out-of-spec {
  fill: var(--color-danger);
  stroke: var(--color-warning);
}
.spc-chart__limit {
  stroke-width: 1.5;
  stroke-dasharray: 6 4;
}
.spc-chart__limit--ucl,
.spc-chart__limit--lcl {
  stroke: var(--color-danger);
}
.spc-chart__limit--usl,
.spc-chart__limit--lsl {
  stroke: var(--color-warning);
  stroke-dasharray: 2 3;
}
.spc-chart__limit--nominal {
  stroke: var(--color-text-subtle);
  stroke-dasharray: 8 4;
}
.spc-chart__empty {
  color: var(--color-text-muted);
  text-align: center;
  padding: var(--space-4);
}
</style>
