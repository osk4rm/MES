<template>
  <div data-testid="gantt-board">
    <AppPageHeader :title="$t('scheduleGantt.title')" :subtitle="$t('scheduleGantt.subtitle')" icon="pi pi-calendar">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="loading" @click="refresh">
          {{ $t('common.refresh') }}
        </AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppFormField :label="$t('scheduleGantt.from')">
        <template #default="{ id }">
          <AppInput :id="id" v-model="fromInput" type="date" @change="onWindowChange" />
        </template>
      </AppFormField>
      <AppFormField :label="$t('scheduleGantt.to')">
        <template #default="{ id }">
          <AppInput :id="id" v-model="toInput" type="date" @change="onWindowChange" />
        </template>
      </AppFormField>
      <template #actions>
        <AppButton variant="primary" icon="pi pi-check" :loading="loading" @click="refresh">
          {{ $t('scheduleGantt.apply') }}
        </AppButton>
      </template>
    </AppFilterBar>

    <p class="gantt-hint">{{ $t('scheduleGantt.hint') }}</p>

    <AppDataState
      :loading="loading"
      :error="loadError"
      :empty="schedule === null || totalBars === 0"
      empty-icon="pi pi-calendar"
      :empty-title="$t('scheduleGantt.empty')"
      @retry="refresh"
    >
      <template v-if="schedule">
        <div v-if="dayColumns.length > 0" class="gantt-axis" :style="axisStyle" aria-hidden="true">
          <div v-for="day in dayColumns" :key="day" class="gantt-axis__cell">
            {{ formatDay(day) }}
          </div>
        </div>

        <AppEmptyState
          v-if="totalBars === 0"
          icon="pi pi-calendar"
          :title="$t('scheduleGantt.empty')"
        />

        <AppCard v-for="group in schedule.groups" :key="laneKey(group)" class="gantt-lane">
        <template #header>
          <div class="gantt-lane__header">
            <span class="gantt-lane__machine">{{ laneTitle(group) }}</span>
            <span v-if="group.machineName" class="gantt-lane__name">{{ group.machineName }}</span>
            <span class="gantt-lane__count">{{ $t('scheduleGantt.bars', { count: group.bars.length }) }}</span>
          </div>
        </template>
        <AppEmptyState
          v-if="group.bars.length === 0"
          icon="pi pi-calendar"
          :title="$t('scheduleGantt.empty')"
        />
        <div
          v-else
          :ref="(el) => setTrackRef(laneKey(group), el)"
          class="gantt-track"
          :class="{ 'gantt-track--loading': loading }"
          :style="{ minHeight: `${laneHeight(group)}px` }"
        >
          <div class="gantt-grid" :style="axisStyle" aria-hidden="true">
            <div v-for="day in dayColumns" :key="day" class="gantt-grid__cell" />
          </div>
          <div class="gantt-bars">
            <div
              v-for="bar in group.bars"
              :key="bar.operationNodeId"
              class="gantt-bar"
              :class="{
                'gantt-bar--overdue': isGanttBarOverdue(bar),
                'gantt-bar--preview': previews[bar.operationNodeId] !== undefined,
                'gantt-bar--moving': moving[bar.operationNodeId] === true,
                'gantt-bar--locked': group.machineId === null
              }"
              :style="barStyle(group, bar)"
              :title="barTitle(bar)"
              :data-testid="`gantt-bar-${bar.operationNodeId}`"
              :data-overdue="isGanttBarOverdue(bar) ? 'true' : 'false'"
              @pointerdown="onBarPointerDown($event, group, bar)"
            >
              <span class="gantt-bar__label">{{ bar.operationCode }} · {{ bar.productionOrderCode }}</span>
              <AppBadge v-if="isGanttBarOverdue(bar)" variant="danger" dot>
                {{ $t('scheduleGantt.overdue') }}
              </AppBadge>
              <AppBadge v-if="warned[bar.operationNodeId] === true" variant="warning" dot>
                {{ $t('scheduleGantt.noCoverage') }}
              </AppBadge>
              <span
                v-if="group.machineId !== null"
                class="gantt-bar__resize"
                :title="$t('scheduleGantt.hint')"
                @pointerdown.stop="onResizePointerDown($event, group, bar)"
              />
            </div>
          </div>
        </div>
      </AppCard>
      </template>
    </AppDataState>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppDataState from '../../components/ui/AppDataState.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import {
  assignGanttRows,
  clampMoveToWindow,
  dragDeltaToMs,
  isGanttBarOverdue,
  isGanttWindowValid,
  layoutGanttBar,
  rescheduleErrorKey,
  scheduleGanttService,
  type GanttBar,
  type GanttMachineGroup,
  type GanttSchedule,
  type GetGanttScheduleQuery
} from '../../services/scheduleGanttService';
import { currentWeekWindow } from '../../services/scheduleService';
import { productionOrderService } from '../../services/productionOrderService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();
const route = useRoute();
const router = useRouter();

/** Minimum resized bar duration: 5 minutes (avoids zero-length windows). */
const MIN_DURATION_MS = 5 * 60 * 1000;
/** Pointer travel below this is a click (opens the order), not a drag. */
const CLICK_SLOP_PX = 4;
/** Lane sub-row geometry: stacked bars never paint over each other. */
const LANE_TOP_PX = 8;
const ROW_HEIGHT_PX = 40;

const fromInput = ref('');
const toInput = ref('');

const schedule = ref<GanttSchedule | null>(null);
const loading = ref(false);
// A failed window fetch is an error with retry (F-13), never a silent
// stale lane: AppDataState renders it above loading/empty/content.
const loadError = ref<string | null>(null);

/** Drag/resize previews keyed by operation node id (reverted on conflict). */
const previews = ref<Record<string, { startMs: number; endMs: number } | undefined>>({});
/** Segments with a PUT in flight — pointer input is locked meanwhile. */
const moving = ref<Record<string, true | undefined>>({});
/** Segments whose last accepted move reported zero shift coverage. */
const warned = ref<Record<string, true | undefined>>({});

const trackEls = new Map<string, HTMLElement>();

interface DragSession {
  nodeId: string;
  mode: 'move' | 'resize';
  machineId: string;
  productionOrderId: string;
  startX: number;
  trackPx: number;
  origStartMs: number;
  origEndMs: number;
}

const activeDrag = ref<DragSession | null>(null);

const dayColumns = computed<string[]>(() => {
  if (!isGanttWindowValid(fromInput.value, toInput.value)) return [];
  const days: string[] = [];
  const cursor = new Date(`${fromInput.value}T00:00:00`);
  const end = new Date(`${toInput.value}T00:00:00`);
  while (cursor.getTime() <= end.getTime() && days.length < 31) {
    const pad = (n: number): string => String(n).padStart(2, '0');
    days.push(`${cursor.getFullYear()}-${pad(cursor.getMonth() + 1)}-${pad(cursor.getDate())}`);
    cursor.setDate(cursor.getDate() + 1);
  }
  return days;
});

const axisStyle = computed<Record<string, string>>(() => ({
  gridTemplateColumns: `repeat(${Math.max(dayColumns.value.length, 1)}, minmax(0, 1fr))`
}));

const totalBars = computed<number>(() => schedule.value?.groups.reduce((n, g) => n + g.bars.length, 0) ?? 0);

function laneKey(group: GanttMachineGroup): string {
  return group.machineId ?? 'unassigned';
}

function laneTitle(group: GanttMachineGroup): string {
  return group.machineCode ?? t('scheduleGantt.unassignedLane');
}

function setTrackRef(key: string, el: unknown): void {
  if (el instanceof HTMLElement) {
    trackEls.set(key, el);
  } else if (el === null) {
    trackEls.delete(key);
  }
}

function formatDay(date: string): string {
  const d = new Date(`${date}T00:00:00`);
  if (Number.isNaN(d.getTime())) return date;
  return d.toLocaleDateString();
}

function formatTime(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString();
}

function barTitle(bar: GanttBar): string {
  return `${bar.operationCode} ${bar.productionOrderCode} ${formatTime(bar.plannedStart)}–${formatTime(bar.plannedEnd)}`;
}

function barStyle(group: GanttMachineGroup, bar: GanttBar): Record<string, string> {
  const preview = previews.value[bar.operationNodeId];
  const effective: GanttBar =
    preview === undefined
      ? bar
      : {
          ...bar,
          plannedStart: new Date(preview.startMs).toISOString(),
          plannedEnd: new Date(preview.endMs).toISOString()
        };
  const layout = layoutGanttBar(effective, fromInput.value, toInput.value);
  const rows = assignGanttRows(group.bars);
  const row = rows[group.bars.indexOf(bar)] ?? 0;
  return {
    left: `${layout.leftPct}%`,
    width: `${layout.widthPct}%`,
    top: `${LANE_TOP_PX + row * ROW_HEIGHT_PX}px`
  };
}

function laneHeight(group: GanttMachineGroup): number {
  const rows = assignGanttRows(group.bars);
  const count = rows.length === 0 ? 1 : Math.max(...rows) + 1;
  return LANE_TOP_PX * 2 + count * ROW_HEIGHT_PX;
}

function readInputs(): GetGanttScheduleQuery | null {
  if (!isGanttWindowValid(fromInput.value, toInput.value)) {
    toast.error(t('scheduleGantt.invalidWindow'));
    return null;
  }
  return { from: fromInput.value, to: toInput.value };
}

let lastAppliedKey: string | null = null;

function syncQuery(q: GetGanttScheduleQuery): void {
  const cur = route.query as Record<string, unknown>;
  if (String(cur.from ?? '') === q.from && String(cur.to ?? '') === q.to) return;
  lastAppliedKey = `${q.from}|${q.to}`;
  void router.replace({ query: { ...route.query, from: q.from, to: q.to } });
}

async function loadSchedule(q: GetGanttScheduleQuery): Promise<void> {
  loading.value = true;
  loadError.value = null;
  try {
    schedule.value = await scheduleGanttService.getSchedule(q);
  } catch (err) {
    loadError.value = extractErrorMessage(err, t('errors.loadFailed'));
  } finally {
    loading.value = false;
  }
}

async function refresh(): Promise<void> {
  const q = readInputs();
  if (!q) return;
  syncQuery(q);
  await loadSchedule(q);
}

function onWindowChange(): void {
  void refresh();
}

function clearFilters(): void {
  const window = currentWeekWindow();
  fromInput.value = window.from;
  toInput.value = window.to;
  void refresh();
}

function openOrder(productionOrderId: string): void {
  void router.push({ name: 'production-order-detail', params: { id: productionOrderId } });
}

function beginDrag(e: PointerEvent, group: GanttMachineGroup, bar: GanttBar, mode: 'move' | 'resize'): void {
  if (e.button !== 0 || group.machineId === null || activeDrag.value !== null) return;
  if (moving.value[bar.operationNodeId] === true) return;
  const startMs = new Date(bar.plannedStart).getTime();
  const endMs = new Date(bar.plannedEnd).getTime();
  if (!Number.isFinite(startMs) || !Number.isFinite(endMs) || endMs <= startMs) return;
  const trackPx = trackEls.get(laneKey(group))?.getBoundingClientRect().width ?? 0;
  if (!(trackPx > 0)) return;
  e.preventDefault();
  activeDrag.value = {
    nodeId: bar.operationNodeId,
    mode,
    machineId: group.machineId,
    productionOrderId: bar.productionOrderId,
    startX: e.clientX,
    trackPx,
    origStartMs: startMs,
    origEndMs: endMs
  };
  window.addEventListener('pointermove', onDragPointerMove);
  window.addEventListener('pointerup', onDragPointerUp);
  window.addEventListener('pointercancel', onDragPointerUp);
}

function onBarPointerDown(e: PointerEvent, group: GanttMachineGroup, bar: GanttBar): void {
  beginDrag(e, group, bar, 'move');
}

function onResizePointerDown(e: PointerEvent, group: GanttMachineGroup, bar: GanttBar): void {
  beginDrag(e, group, bar, 'resize');
}

function onDragPointerMove(e: Event): void {
  const session = activeDrag.value;
  if (!session) return;
  const clientX = (e as PointerEvent).clientX;
  if (typeof clientX !== 'number') return;
  const deltaPx = clientX - session.startX;
  if (Math.abs(deltaPx) < CLICK_SLOP_PX) {
    previews.value[session.nodeId] = undefined;
    return;
  }
  const deltaMs = dragDeltaToMs(deltaPx, session.trackPx, fromInput.value, toInput.value);
  if (session.mode === 'move') {
    previews.value[session.nodeId] = clampMoveToWindow(
      session.origStartMs,
      session.origEndMs,
      deltaMs,
      fromInput.value,
      toInput.value
    );
  } else {
    const windowEndMs = new Date(`${toInput.value}T00:00:00`).getTime() + 86400000;
    const endMs = Math.min(
      Math.max(session.origEndMs + deltaMs, session.origStartMs + MIN_DURATION_MS),
      Number.isFinite(windowEndMs) ? windowEndMs : session.origEndMs + deltaMs
    );
    previews.value[session.nodeId] = { startMs: session.origStartMs, endMs };
  }
}

function endDragListeners(): void {
  window.removeEventListener('pointermove', onDragPointerMove);
  window.removeEventListener('pointerup', onDragPointerUp);
  window.removeEventListener('pointercancel', onDragPointerUp);
}

function onDragPointerUp(e: Event): void {
  const session = activeDrag.value;
  activeDrag.value = null;
  endDragListeners();
  if (!session) return;
  const clientX = (e as PointerEvent).clientX;
  const draggedPx = typeof clientX === 'number' ? Math.abs(clientX - session.startX) : CLICK_SLOP_PX;
  const preview = previews.value[session.nodeId];
  previews.value[session.nodeId] = undefined;
  // A press without travel opens the Production Order instead of moving.
  if (draggedPx < CLICK_SLOP_PX || preview === undefined) {
    if (session.mode === 'move') openOrder(session.productionOrderId);
    return;
  }
  void commitMove(session, preview.startMs, preview.endMs);
}

async function commitMove(session: DragSession, startMs: number, endMs: number): Promise<void> {
  // Skip no-op drops (e.g. clamped back to the origin by the track edges).
  if (startMs === session.origStartMs && endMs === session.origEndMs) return;
  moving.value[session.nodeId] = true;
  try {
    // The Gantt read-model carries no xmin token, so the order is re-read
    // for a fresh token; a concurrent order change surfaces as a 409 with
    // retry guidance instead of a silent overwrite.
    const order = await productionOrderService.get(session.productionOrderId);
    const result = await scheduleGanttService.rescheduleSegment(session.nodeId, {
      productionOrderId: session.productionOrderId,
      plannedStart: new Date(startMs).toISOString(),
      plannedEnd: new Date(endMs).toISOString(),
      machineId: session.machineId,
      concurrencyToken: order.concurrencyToken
    });
    toast.success(t('scheduleGantt.moved'));
    if (result.shiftCoverageWarning) {
      warned.value[session.nodeId] = true;
      toast.warning(t('scheduleGantt.movedNoCoverage'));
    } else {
      warned.value[session.nodeId] = undefined;
    }
    // Reload so the bar lands on the persisted slot (and a conflicting
    // sibling moved meanwhile becomes visible).
    await loadSchedule({ from: fromInput.value, to: toInput.value });
  } catch (err) {
    // Conflict (overlap or stale token): toast with retry guidance and
    // revert by reloading the persisted schedule.
    toast.error(t(rescheduleErrorKey(err)));
    await loadSchedule({ from: fromInput.value, to: toInput.value });
  } finally {
    moving.value[session.nodeId] = undefined;
  }
}

function readStateFromQuery(): void {
  const q = route.query as Record<string, unknown>;
  const fallback = currentWeekWindow();
  const from = typeof q.from === 'string' && q.from ? q.from : '';
  const to = typeof q.to === 'string' && q.to ? q.to : '';
  // Accept only ISO day strings from the URL; anything else falls back to
  // the current week so a crafted link never breaks the board.
  fromInput.value = /^\d{4}-\d{2}-\d{2}$/.test(from) ? from : fallback.from;
  toInput.value = /^\d{4}-\d{2}-\d{2}$/.test(to) ? to : fallback.to;
}

function queryKey(): string {
  const q = route.query as Record<string, unknown>;
  return [q.from, q.to].map((v) => String(v ?? '')).join('|');
}

watch(queryKey, () => {
  if (lastAppliedKey !== null && queryKey() === lastAppliedKey) {
    lastAppliedKey = null;
    return;
  }
  lastAppliedKey = null;
  readStateFromQuery();
  void refresh();
});

onMounted(() => {
  readStateFromQuery();
  void refresh();
});

onUnmounted(() => {
  endDragListeners();
  activeDrag.value = null;
});
</script>

<style scoped>
.gantt-hint {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin: 0 0 var(--space-3);
}
.gantt-axis {
  display: grid;
  gap: 0;
  margin-bottom: var(--space-2);
  padding: 0 var(--space-3);
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.gantt-axis__cell {
  text-align: left;
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.gantt-lane {
  margin-bottom: var(--space-3);
}
.gantt-lane__header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
}
.gantt-lane__machine {
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-semibold);
}
.gantt-lane__name {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.gantt-lane__count {
  margin-left: auto;
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.gantt-track {
  position: relative;
  min-height: 56px;
}
.gantt-track--loading {
  opacity: 0.6;
}
.gantt-grid {
  position: absolute;
  inset: 0;
  display: grid;
}
.gantt-grid__cell {
  border-left: 1px dashed var(--color-border);
}
.gantt-grid__cell:last-child {
  border-right: 1px dashed var(--color-border);
}
.gantt-bars {
  position: absolute;
  inset: 0;
}
.gantt-bar {
  position: absolute;
  height: 32px;
  min-width: 8px;
  display: flex;
  align-items: center;
  gap: var(--space-1);
  padding: 0 var(--space-2);
  overflow: hidden;
  border-radius: var(--radius-md);
  background: var(--color-primary);
  color: #fff;
  cursor: grab;
  touch-action: none;
  user-select: none;
}
.gantt-bar--overdue {
  background: var(--color-danger);
}
.gantt-bar--preview {
  opacity: 0.85;
  cursor: grabbing;
}
.gantt-bar--moving {
  cursor: progress;
  opacity: 0.6;
}
.gantt-bar--locked {
  cursor: default;
}
.gantt-bar__label {
  font-size: var(--font-size-sm);
  font-weight: var(--font-weight-medium);
  overflow: hidden;
  text-overflow: ellipsis;
  white-space: nowrap;
}
.gantt-bar__resize {
  position: absolute;
  top: 0;
  right: 0;
  width: 12px;
  height: 100%;
  cursor: ew-resize;
}
</style>
