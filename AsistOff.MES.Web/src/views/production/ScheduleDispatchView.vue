<template>
  <div data-testid="dispatch-board">
    <AppPageHeader :title="$t('scheduleDispatch.title')" :subtitle="$t('scheduleDispatch.subtitle')" icon="pi pi-calendar">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="loading" @click="refresh">
          {{ $t('common.refresh') }}
        </AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppFormField :label="$t('scheduleDispatch.from')">
        <template #default="{ id }">
          <AppInput :id="id" v-model="fromInput" type="date" @change="onWindowChange" />
        </template>
      </AppFormField>
      <AppFormField :label="$t('scheduleDispatch.to')">
        <template #default="{ id }">
          <AppInput :id="id" v-model="toInput" type="date" @change="onWindowChange" />
        </template>
      </AppFormField>
      <template #actions>
        <AppButton variant="primary" icon="pi pi-check" :loading="loading" @click="refresh">
          {{ $t('scheduleDispatch.apply') }}
        </AppButton>
      </template>
    </AppFilterBar>

    <AppSpinner v-if="loading && !loadedOnce" />

    <template v-else-if="board">
      <div class="dispatch-days">
        <AppCard v-for="day in board.days" :key="day.date" class="dispatch-day">
          <template #header>
            <div class="dispatch-day__header">
              <span class="dispatch-day__date">{{ formatDay(day.date) }}</span>
              <span class="dispatch-day__count">{{ day.shifts.length }}</span>
            </div>
          </template>
          <AppEmptyState
            v-if="day.shifts.length === 0"
            icon="pi pi-clock"
            :title="$t('scheduleDispatch.noShifts')"
          />
          <ul v-else class="dispatch-shifts">
            <li v-for="shift in day.shifts" :key="shift.shiftId" class="dispatch-shift">
              <AppBadge :variant="shift.isOvernight ? 'info' : 'primary'" dot>
                {{ shift.code }}
              </AppBadge>
              <span class="dispatch-shift__name">{{ shift.name }}</span>
              <span class="dispatch-shift__time">{{ shiftTime(shift) }}</span>
              <span class="dispatch-shift__headcount">{{
                $t('scheduleDispatch.headcount', { count: shift.headcount })
              }}</span>
              <AppBadge v-if="shift.isUncovered" variant="warning" dot>
                {{ $t('scheduleDispatch.uncovered') }}
              </AppBadge>
            </li>
          </ul>
        </AppCard>
      </div>

      <AppCard class="dispatch-orders" data-testid="dispatch-orders">
        <template #header>
          <h3 class="dispatch-orders__title">{{ $t('scheduleDispatch.ordersTitle') }}</h3>
        </template>
        <AppTable
          :items="orderRows"
          :columns="orderColumns"
          :loading="loading"
          row-key="id"
          :empty-label="$t('scheduleDispatch.ordersEmpty')"
          data-testid="dispatch-orders-table"
          @row-click="openOrder"
        >
          <template #cell-code="{ item }">
            <code>{{ item.code }}</code>
          </template>
          <template #cell-dueDate="{ item }">
            {{ formatDueDate(item.dueDate) }}
          </template>
          <template #cell-overdue="{ item }">
            <AppBadge v-if="isDispatchRowOverdue(item)" variant="danger" dot>
              {{ $t('scheduleDispatch.overdue') }}
            </AppBadge>
            <span v-else class="dispatch-ontime">{{ $t('scheduleDispatch.onTime') }}</span>
          </template>
          <template #cell-status="{ item }">
            <AppBadge :variant="statusVariant(item.status)" dot>
              {{ statusLabel(item.status) }}
            </AppBadge>
          </template>
        </AppTable>
      </AppCard>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import {
  currentWeekWindow,
  isDispatchRowOverdue,
  scheduleService,
  type DispatchBoard,
  type DispatchOrderRow,
  type DispatchShift,
  type GetDispatchBoardQuery
} from '../../services/scheduleService';
import { ProductionOrderStatus } from '../../services/productionOrderService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();
const route = useRoute();
const router = useRouter();

const fromInput = ref('');
const toInput = ref('');

const board = ref<DispatchBoard | null>(null);
const loading = ref(false);
const loadedOnce = ref(false);

// Rows render in the backend ordering contract (overdue first, then due
// date ascending with nulls last, then priority, then code) — the board
// never re-sorts, it only badges overdue rows.
const orderRows = computed<DispatchOrderRow[]>(() => board.value?.orders ?? []);

const orderColumns = computed(() => [
  { key: 'code', label: t('scheduleDispatch.code') },
  { key: 'dueDate', label: t('scheduleDispatch.dueDate') },
  { key: 'overdue', label: t('scheduleDispatch.overdue') },
  { key: 'priority', label: t('scheduleDispatch.priority'), align: 'right' as const },
  { key: 'remainingQuantity', label: t('scheduleDispatch.remaining'), align: 'right' as const },
  { key: 'status', label: t('common.status') }
]);

function statusLabel(v: number): string {
  switch (v) {
    case ProductionOrderStatus.Released: return t('productionOrders.status.released');
    case ProductionOrderStatus.InProgress: return t('productionOrders.status.inProgress');
    case ProductionOrderStatus.Planned: return t('productionOrders.status.planned');
    case ProductionOrderStatus.Completed: return t('productionOrders.status.completed');
    case ProductionOrderStatus.Closed: return t('productionOrders.status.closed');
    default: return String(v);
  }
}

function statusVariant(v: number): 'info' | 'primary' | 'success' | 'warning' | 'idle' {
  switch (v) {
    case ProductionOrderStatus.Released: return 'success';
    case ProductionOrderStatus.InProgress: return 'warning';
    case ProductionOrderStatus.Completed: return 'primary';
    case ProductionOrderStatus.Closed: return 'idle';
    default: return 'info';
  }
}

function formatDay(date: string): string {
  const d = new Date(`${date}T00:00:00`);
  if (Number.isNaN(d.getTime())) return date;
  return d.toLocaleDateString();
}

function formatDueDate(value: string | null): string {
  if (!value) return t('scheduleDispatch.noDueDate');
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleDateString();
}

function shiftTime(shift: DispatchShift): string {
  const short = (v: string): string => v.slice(0, 5);
  return `${short(shift.startTime)}–${short(shift.endTime)}`;
}

function isValidWindow(from: string, to: string): boolean {
  if (!from || !to || from > to) return false;
  const fromMs = new Date(`${from}T00:00:00`).getTime();
  const toMs = new Date(`${to}T00:00:00`).getTime();
  if (!Number.isFinite(fromMs) || !Number.isFinite(toMs)) return false;
  // The backend caps the window at 31 days, so a from..to span above 30
  // days is rejected up front instead of surfacing a 400 toast.
  const spanDays = Math.round((toMs - fromMs) / 86400000);
  return spanDays >= 0 && spanDays <= 30;
}

function readInputs(): GetDispatchBoardQuery | null {
  if (!isValidWindow(fromInput.value, toInput.value)) {
    toast.error(t('scheduleDispatch.invalidWindow'));
    return null;
  }
  return { from: fromInput.value, to: toInput.value };
}

let lastAppliedKey: string | null = null;

function syncQuery(q: GetDispatchBoardQuery): void {
  const cur = route.query as Record<string, unknown>;
  if (String(cur.from ?? '') === q.from && String(cur.to ?? '') === q.to) return;
  lastAppliedKey = `${q.from}|${q.to}`;
  void router.replace({ query: { ...route.query, from: q.from, to: q.to } });
}

async function loadBoard(q: GetDispatchBoardQuery): Promise<void> {
  loading.value = true;
  try {
    board.value = await scheduleService.getDispatch(q);
    loadedOnce.value = true;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    loading.value = false;
  }
}

async function refresh(): Promise<void> {
  const q = readInputs();
  if (!q) return;
  syncQuery(q);
  await loadBoard(q);
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

function openOrder(item: DispatchOrderRow): void {
  void router.push({ name: 'production-order-detail', params: { id: item.id } });
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
</script>

<style scoped>
.dispatch-days {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}
.dispatch-day__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}
.dispatch-day__date {
  font-size: var(--font-size-md);
  font-weight: var(--font-weight-semibold);
}
.dispatch-day__count {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.dispatch-shifts {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}
.dispatch-shift {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}
.dispatch-shift__name {
  font-weight: var(--font-weight-medium);
}
.dispatch-shift__time {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.dispatch-shift__headcount {
  margin-left: auto;
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.dispatch-orders {
  margin-bottom: var(--space-3);
}
.dispatch-orders__title {
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
}
.dispatch-ontime {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
</style>
