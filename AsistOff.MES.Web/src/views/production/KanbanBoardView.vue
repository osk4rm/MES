<template>
  <div>
    <AppPageHeader :title="$t('kanban.title')" :subtitle="$t('kanban.subtitle')" icon="pi pi-th-large">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="refreshing" @click="refreshAll">
          {{ $t('common.refresh') }}
        </AppButton>
      </template>
    </AppPageHeader>

    <AppCard class="loop-card">
      <AppFormField :label="$t('kanban.loop')">
        <template #default="{ id }">
          <AppSelect
            :id="id"
            :model-value="selectedLoopId"
            :options="loopOptions"
            :placeholder="$t('kanban.selectLoop')"
            :disabled="loopsLoading || loops.length === 0"
            @change="onLoopChange"
          />
        </template>
      </AppFormField>
      <div v-if="loopsLoading" class="loading"><AppSpinner /></div>
      <AppEmptyState
        v-else-if="loopsLoaded && loops.length === 0"
        icon="pi pi-th-large"
        :title="$t('kanban.noLoops')"
      />
      <AppEmptyState
        v-else-if="loopMissing"
        icon="pi pi-exclamation-circle"
        :title="$t('kanban.notFound')"
        :description="$t('kanban.notFoundHint')"
      />
    </AppCard>

    <template v-if="selectedLoop && !loopMissing">
      <AppEmptyState
        v-if="boardEmpty"
        icon="pi pi-inbox"
        :title="$t('kanban.loopEmpty')"
      />
      <div v-else class="board">
        <AppCard v-for="col in board" :key="col.status">
          <template #header>
            <div class="column-header">
              <AppBadge :variant="statusVariant(col.status)" dot>
                {{ statusLabel(col.status) }}
              </AppBadge>
              <span class="column-count">{{ col.totalCount }}</span>
            </div>
          </template>
          <AppTable
            :items="col.items"
            :columns="cardColumns"
            :loading="col.loading"
            row-key="id"
            :empty-label="$t('kanban.columnEmpty')"
          >
            <template #cell-cardNumber="{ item }">
              <code>{{ item.cardNumber }}</code>
            </template>
            <template #cell-product>
              {{ selectedLoop.productId }}
            </template>
            <template #cell-workCenter>
              {{ selectedLoop.consumingMachineId }}
            </template>
            <template #cell-warehouse>
              {{ selectedLoop.supplyingWarehouseId }}
            </template>
            <template #cell-cardQuantity>
              {{ formatQuantity(selectedLoop.cardQuantity) }}
            </template>
            <template #cell-actions="{ item }">
              <AppButton
                v-if="col.status === KanbanCardStatus.Full"
                size="sm"
                variant="secondary"
                :loading="col.actingId === item.id"
                @click="onConsume(item)"
              >
                {{ $t('kanban.consume') }}
              </AppButton>
              <AppButton
                v-else-if="col.status === KanbanCardStatus.Empty"
                size="sm"
                variant="secondary"
                :loading="col.actingId === item.id"
                @click="onOrder(item)"
              >
                {{ $t('kanban.order') }}
              </AppButton>
              <AppButton
                v-else
                size="sm"
                variant="primary"
                :loading="col.actingId === item.id"
                @click="onReplenish(item)"
              >
                {{ $t('kanban.replenish') }}
              </AppButton>
            </template>
          </AppTable>
          <AppPagination
            :current-page="col.page"
            :page-size="col.pageSize"
            :total-count="col.totalCount"
            :total-pages="col.totalPages"
            @page-change="(p) => onPageChange(col, p)"
            @page-size-change="(s) => onPageSizeChange(col, s)"
          />
        </AppCard>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import {
  KanbanCardStatus,
  kanbanService,
  type KanbanCardResponse,
  type KanbanLoopResponse
} from '../../services/kanbanService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();
const route = useRoute();
const router = useRouter();

const BOARD_PAGE_SIZE = 25;
const LOOP_PAGE_SIZE = 100;

interface BoardColumn {
  status: KanbanCardStatus;
  items: KanbanCardResponse[];
  loading: boolean;
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  actingId: string | null;
}

function createColumn(status: KanbanCardStatus): BoardColumn {
  return { status, items: [], loading: false, page: 1, pageSize: BOARD_PAGE_SIZE, totalCount: 0, totalPages: 0, actingId: null };
}

const board = ref<BoardColumn[]>([
  createColumn(KanbanCardStatus.Full),
  createColumn(KanbanCardStatus.Empty),
  createColumn(KanbanCardStatus.Ordered)
]);

const loops = ref<KanbanLoopResponse[]>([]);
const loopsLoading = ref(false);
const loopsLoaded = ref(false);
const refreshing = ref(false);
const selectedLoopId = ref<string | null>(null);

const selectedLoop = computed(() => loops.value.find((l) => l.id === selectedLoopId.value) ?? null);
const loopMissing = computed(() => loopsLoaded.value && selectedLoopId.value !== null && selectedLoop.value === null);

const anyColumnLoading = computed(() => board.value.some((c) => c.loading));
const boardEmpty = computed(
  () => selectedLoop.value !== null && !anyColumnLoading.value && board.value.every((c) => c.totalCount === 0)
);

const loopOptions = computed(() => loops.value.map((l) => ({
  value: l.id,
  label: l.isActive ? l.code : `${l.code} — ${t('common.inactive')}`
})));

const cardColumns = computed(() => [
  { key: 'cardNumber', label: t('kanban.cardNumber'), sortable: true },
  { key: 'product', label: t('kanban.product') },
  { key: 'workCenter', label: t('kanban.workCenter') },
  { key: 'warehouse', label: t('kanban.warehouse') },
  { key: 'cardQuantity', label: t('kanban.cardQuantity'), align: 'right' as const },
  { key: 'actions', label: t('common.actions'), width: '130px' }
]);

function statusLabel(v: KanbanCardStatus): string {
  const map = tm('kanban.statuses') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

function statusVariant(v: KanbanCardStatus): 'success' | 'warning' | 'info' {
  if (v === KanbanCardStatus.Empty) return 'warning';
  if (v === KanbanCardStatus.Ordered) return 'info';
  return 'success';
}

function formatQuantity(v: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(v);
}

function isConflictError(err: unknown): boolean {
  if (typeof err !== 'object' || err === null) return false;
  const response = (err as { response?: { status?: unknown } }).response;
  return response?.status === 409;
}

function syncLoopQuery(): void {
  const query = { ...route.query };
  if (selectedLoopId.value) {
    query.loopId = selectedLoopId.value;
  } else {
    delete query.loopId;
  }
  void router.replace({ query });
}

async function loadColumn(col: BoardColumn): Promise<void> {
  const loopId = selectedLoopId.value;
  if (!loopId) {
    col.items = [];
    col.totalCount = 0;
    col.totalPages = 0;
    return;
  }
  col.loading = true;
  try {
    const page = await kanbanService.browseCards(loopId, col.status, {
      pageNumber: col.page,
      pageSize: col.pageSize
    });
    col.items = page.items;
    col.totalCount = page.totalCount;
    col.totalPages = page.totalPages;
  } catch (err) {
    col.items = [];
    col.totalCount = 0;
    col.totalPages = 0;
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    col.loading = false;
  }
}

async function loadBoard(): Promise<void> {
  await Promise.all(board.value.map((col) => loadColumn(col)));
}

async function loadLoops(): Promise<void> {
  loopsLoading.value = true;
  try {
    const page = await kanbanService.browseLoops({ pageNumber: 1, pageSize: LOOP_PAGE_SIZE });
    loops.value = page.items;
    loopsLoaded.value = true;
    if (!selectedLoopId.value && loops.value.length > 0) {
      const first = loops.value[0];
      if (first) {
        selectedLoopId.value = first.id;
        syncLoopQuery();
      }
    }
  } catch (err) {
    loops.value = [];
    loopsLoaded.value = true;
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    loopsLoading.value = false;
  }
}

async function refreshAll(): Promise<void> {
  refreshing.value = true;
  try {
    await loadLoops();
    await loadBoard();
  } finally {
    refreshing.value = false;
  }
}

async function selectLoop(id: string | null, sync: boolean): Promise<void> {
  selectedLoopId.value = id;
  for (const col of board.value) {
    col.page = 1;
  }
  if (sync) syncLoopQuery();
  await loadBoard();
}

function onLoopChange(v: string | number | null): void {
  void selectLoop(v === null ? null : String(v), true);
}

function onPageChange(col: BoardColumn, page: number): void {
  col.page = page;
  void loadColumn(col);
}

function onPageSizeChange(col: BoardColumn, size: number): void {
  col.pageSize = size;
  col.page = 1;
  void loadColumn(col);
}

type TransitionAction = 'consume' | 'order' | 'replenish';

async function runTransition(card: KanbanCardResponse, expected: KanbanCardStatus, action: TransitionAction): Promise<void> {
  if (card.status !== expected) {
    toast.error(t('kanban.illegalTransition'));
    return;
  }
  const col = board.value.find((c) => c.status === expected);
  if (!col) return;
  col.actingId = card.id;
  try {
    if (action === 'consume') {
      await kanbanService.consumeCard(card.id);
      toast.success(t('kanban.consumed'));
    } else if (action === 'order') {
      await kanbanService.orderCard(card.id);
      toast.success(t('kanban.ordered'));
    } else {
      await kanbanService.replenishCard(card.id);
      toast.success(t('kanban.replenished'));
    }
    await loadBoard();
  } catch (err) {
    if (isConflictError(err)) {
      if (action === 'order') {
        toast.error(t('kanban.wipLimitNotice'));
      } else if (action === 'replenish') {
        toast.error(t('kanban.inactiveLoopNotice'));
      } else {
        toast.error(extractErrorMessage(err, t('errors.saveFailed')));
      }
    } else {
      toast.error(extractErrorMessage(err, t('errors.saveFailed')));
    }
  } finally {
    col.actingId = null;
  }
}

function onConsume(card: KanbanCardResponse): void {
  void runTransition(card, KanbanCardStatus.Full, 'consume');
}

function onOrder(card: KanbanCardResponse): void {
  void runTransition(card, KanbanCardStatus.Empty, 'order');
}

function onReplenish(card: KanbanCardResponse): void {
  void runTransition(card, KanbanCardStatus.Ordered, 'replenish');
}

function readLoopIdFromQuery(): string | null {
  const v = route.query.loopId;
  return typeof v === 'string' && v ? v : null;
}

watch(
  () => route.query.loopId,
  (v) => {
    const id = typeof v === 'string' && v ? v : null;
    if (id !== selectedLoopId.value) {
      void selectLoop(id, false);
    }
  }
);

onMounted(async () => {
  selectedLoopId.value = readLoopIdFromQuery();
  await loadLoops();
  await loadBoard();
});
</script>

<style scoped>
.loop-card { margin-bottom: var(--space-3); }
.loading { display: flex; justify-content: center; padding: var(--space-3); }
.board { display: grid; grid-template-columns: repeat(3, minmax(0, 1fr)); gap: var(--space-3); align-items: start; }
.column-header { display: flex; align-items: center; justify-content: space-between; gap: var(--space-2); }
.column-count { font-size: var(--font-size-sm); color: var(--color-text-muted); }
@media (max-width: 1100px) {
  .board { grid-template-columns: 1fr; }
}
</style>
