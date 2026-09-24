<template>
  <div>
    <AppPageHeader :title="$t('andon.title')" :subtitle="$t('andon.subtitle')" icon="pi pi-bell">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="refreshAll">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openRaise">{{ $t('andon.raise') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppCard :title="$t('andon.board')">
      <div v-if="boardLoading" class="board-loading">
        <AppSpinner />
      </div>
      <AppEmptyState v-else-if="boardSignals.length === 0" :title="$t('andon.boardEmpty')" />
      <div v-else class="board">
        <div
          v-for="signal in boardSignals"
          :key="signal.id"
          class="board-card"
          :class="`board-card--${statusKey(signal.status)}`"
        >
          <div class="board-card__header">
            <strong>{{ machineLabel(signal.machineId) }}</strong>
            <AppBadge :variant="statusVariant(signal.status)" dot>
              {{ statusLabel(signal.status) }}
            </AppBadge>
          </div>
          <div class="board-card__category">{{ categoryLabel(signal.category) }}</div>
          <div class="board-card__time">{{ formatDateTime(signal.raisedAt) }}</div>
          <div v-if="signal.notes" class="board-card__notes">{{ signal.notes }}</div>
          <div class="board-card__actions">
            <AppButton
              v-if="signal.status === AndonSignalStatus.Active"
              size="sm"
              variant="secondary"
              @click="onAcknowledge(signal)"
            >
              {{ $t('andon.acknowledge') }}
            </AppButton>
            <AppButton size="sm" variant="primary" @click="onResolve(signal)">
              {{ $t('andon.resolve') }}
            </AppButton>
          </div>
        </div>
      </div>
    </AppCard>

    <AppFilterBar @clear="clearFilters">
      <AppSelect
        v-model="machineFilter"
        :options="machineFilterOptions"
        allow-empty
        @change="onMachineChange"
      />
      <AppSelect
        v-model="categoryFilter"
        :options="categoryFilterOptions"
        allow-empty
        @change="onCategoryChange"
      />
      <AppSelect
        v-model="statusFilter"
        :options="statusFilterOptions"
        allow-empty
        @change="onStatusChange"
      />
    </AppFilterBar>

    <AppTable
      :items="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      @sort-change="table.setSort"
    >
      <template #cell-machineId="{ value }">
        {{ machineLabel(String(value)) }}
      </template>
      <template #cell-category="{ value }">
        {{ categoryLabel(Number(value)) }}
      </template>
      <template #cell-status="{ value }">
        <AppBadge :variant="statusVariant(Number(value))" dot>
          {{ statusLabel(Number(value)) }}
        </AppBadge>
      </template>
      <template #cell-raisedAt="{ value }">
        {{ formatDateTime(String(value)) }}
      </template>
      <template #cell-actions="{ item }">
        <AppRowActions :actions="rowActions(item)" @action="(k) => onRowAction(k, item)" />
      </template>
    </AppTable>

    <AppPagination
      :current-page="table.page.value"
      :page-size="table.pageSize.value"
      :total-count="table.totalCount.value"
      :total-pages="table.totalPages.value"
      @page-change="table.setPage"
      @page-size-change="table.setPageSize"
    />

    <AppModal :open="modalOpen" :title="modalTitle" @close="closeModal">
      <form id="andon-form" class="form-grid" @submit.prevent="onSave">
        <template v-if="resolving">
          <AppFormField :label="$t('andon.resolvedAt')" required class="form-grid__full">
            <template #default="{ id }">
              <AppInput :id="id" v-model="form.resolvedAt" type="datetime-local" required />
            </template>
          </AppFormField>
        </template>
        <template v-else>
          <AppFormField v-if="!editing" :label="$t('andon.machine')" required class="form-grid__full">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.machineId" :options="machineOptions" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('andon.category')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.category" :options="categoryOptions" />
          </template>
        </AppFormField>
        <AppFormField v-if="!editing" :label="$t('andon.raisedAt')" required>
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.raisedAt" type="datetime-local" required />
          </template>
        </AppFormField>
        <AppFormField :label="$t('andon.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.notes" :rows="3" />
          </template>
        </AppFormField>
        </template>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="andon-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppConfirmDialog
      :open="confirmOpen"
      :title="$t('common.delete')"
      :message="deleteMessage"
      :loading="deleting"
      @confirm="confirmDelete"
      @cancel="cancelDelete"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import {
  andonSignalService,
  AndonSignalCategory,
  AndonSignalStatus,
  type AndonSignalResponse
} from '../../services/andonSignalService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();

interface Filters {
  machineId?: string;
  category?: AndonSignalCategory;
  status?: AndonSignalStatus;
}

const table = useCrudPage<AndonSignalResponse, Filters>({
  fetch: (req) => andonSignalService.browse(req),
  initialFilters: {}
});

const machines = ref<MachineResponse[]>([]);
const machineById = computed(() => new Map(machines.value.map((m) => [m.id, m])));

function machineLabel(id: string): string {
  const m = machineById.value.get(id);
  return m ? `${m.code} — ${m.name}` : id;
}

function categoryLabel(v: number): string {
  const map = tm('andon.categories') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

function statusLabel(v: number): string {
  const map = tm('andon.statuses') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

function statusKey(v: number): string {
  if (v === AndonSignalStatus.Acknowledged) return 'acknowledged';
  if (v === AndonSignalStatus.Resolved) return 'resolved';
  return 'active';
}

function statusVariant(v: number): 'danger' | 'warning' | 'success' {
  if (v === AndonSignalStatus.Acknowledged) return 'warning';
  if (v === AndonSignalStatus.Resolved) return 'success';
  return 'danger';
}

function formatDateTime(v: string): string {
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleString();
}

const columns = computed(() => [
  { key: 'machineId', label: t('andon.machine'), sortable: true },
  { key: 'category', label: t('andon.category'), sortable: true },
  { key: 'status', label: t('common.status'), sortable: true },
  { key: 'raisedAt', label: t('andon.raisedAt'), sortable: true },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const machineOptions = computed(() => machines.value.map((m) => ({
  value: m.id,
  label: `${m.code} — ${m.name}`
})));

const machineFilterOptions = computed(() => [
  { value: null, label: t('andon.filters.machine') },
  ...machineOptions.value
]);

const categoryOptions = computed(() => Object.values(AndonSignalCategory)
  .filter((v): v is AndonSignalCategory => typeof v === 'number')
  .map((v) => ({ value: v, label: categoryLabel(v) })));

const categoryFilterOptions = computed(() => [
  { value: null, label: t('andon.filters.category') },
  ...categoryOptions.value
]);

const statusOptions = computed(() => Object.values(AndonSignalStatus)
  .filter((v): v is AndonSignalStatus => typeof v === 'number')
  .map((v) => ({ value: v, label: statusLabel(v) })));

const statusFilterOptions = computed(() => [
  { value: null, label: t('common.status') },
  ...statusOptions.value
]);

const machineFilter = ref<string | null>(null);
const categoryFilter = ref<number | null>(null);
const statusFilter = ref<number | null>(null);

function onMachineChange(v: string | number | null) {
  table.setFilter('machineId', v === null ? undefined : String(v));
}
function onCategoryChange(v: string | number | null) {
  table.setFilter('category', v === null ? undefined : Number(v) as AndonSignalCategory);
}
function onStatusChange(v: string | number | null) {
  table.setFilter('status', v === null ? undefined : Number(v) as AndonSignalStatus);
}
function clearFilters() {
  machineFilter.value = null;
  categoryFilter.value = null;
  statusFilter.value = null;
  table.resetFilters();
}

const boardSignals = ref<AndonSignalResponse[]>([]);
const boardLoading = ref(false);

async function fetchBoard() {
  boardLoading.value = true;
  try {
    const [active, acknowledged] = await Promise.all([
      andonSignalService.browse({ status: AndonSignalStatus.Active, pageSize: 50 }),
      andonSignalService.browse({ status: AndonSignalStatus.Acknowledged, pageSize: 50 })
    ]);
    boardSignals.value = [...active.items, ...acknowledged.items]
      .sort((a, b) => +new Date(b.raisedAt) - +new Date(a.raisedAt));
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    boardLoading.value = false;
  }
}

async function refreshAll() {
  await Promise.all([table.fetch(), fetchBoard()]);
}

function rowActions(item: AndonSignalResponse): Array<{ key: string; label: string; icon: string; variant?: 'danger' }> {
  const actions: Array<{ key: string; label: string; icon: string; variant?: 'danger' }> = [];
  if (item.status === AndonSignalStatus.Active) {
    actions.push({ key: 'acknowledge', label: t('andon.acknowledge'), icon: 'pi-check' });
  }
  if (item.status !== AndonSignalStatus.Resolved) {
    actions.push({ key: 'resolve', label: t('andon.resolve'), icon: 'pi-check-circle' });
    actions.push({ key: 'edit', label: t('common.edit'), icon: 'pi-pencil' });
    actions.push({ key: 'delete', label: t('common.delete'), icon: 'pi-trash', variant: 'danger' });
  }
  return actions;
}

function onRowAction(key: string, item: AndonSignalResponse) {
  if (key === 'acknowledge') void onAcknowledge(item);
  else if (key === 'resolve') onResolve(item);
  else if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}

async function onAcknowledge(item: AndonSignalResponse) {
  try {
    await andonSignalService.acknowledge(item.id);
    toast.success(t('toasts.updated'));
    await refreshAll();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}

function onResolve(item: AndonSignalResponse) {
  openEdit(item, true);
}

const modalOpen = ref(false);
const editing = ref<AndonSignalResponse | null>(null);
const resolving = ref(false);
const saving = ref(false);
const form = reactive({
  machineId: '' as string | null,
  category: AndonSignalCategory.Downtime as number | null,
  raisedAt: '',
  resolvedAt: '',
  notes: ''
});

const modalTitle = computed(() => {
  if (resolving.value) return t('andon.resolveSignal');
  return editing.value ? t('andon.editSignal') : t('andon.raise');
});

function toLocalInput(date: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
}

function openRaise() {
  editing.value = null;
  resolving.value = false;
  Object.assign(form, {
    machineId: machines.value[0]?.id ?? '',
    category: AndonSignalCategory.Downtime,
    raisedAt: toLocalInput(new Date()),
    resolvedAt: toLocalInput(new Date()),
    notes: ''
  });
  modalOpen.value = true;
}

function openEdit(item: AndonSignalResponse, resolve = false) {
  editing.value = item;
  resolving.value = resolve;
  Object.assign(form, {
    machineId: item.machineId,
    category: item.category,
    raisedAt: toLocalInput(new Date(item.raisedAt)),
    resolvedAt: toLocalInput(new Date()),
    notes: item.notes ?? ''
  });
  modalOpen.value = true;
}

function closeModal() {
  if (saving.value) return;
  modalOpen.value = false;
  editing.value = null;
  resolving.value = false;
}

async function onSave() {
  saving.value = true;
  try {
    if (editing.value) {
      if (resolving.value) {
        await andonSignalService.resolve(editing.value.id, {
          resolvedAt: form.resolvedAt ? new Date(form.resolvedAt).toISOString() : null
        });
      } else {
        await andonSignalService.update(editing.value.id, {
          id: editing.value.id,
          category: Number(form.category) as AndonSignalCategory,
          reasonCodeId: editing.value.reasonCodeId ?? null,
          notes: form.notes || null
        });
      }
      toast.success(t('toasts.updated'));
    } else {
      if (!form.machineId) {
        toast.error(t('validation.required'));
        return;
      }
      await andonSignalService.raise({
        machineId: String(form.machineId),
        category: Number(form.category) as AndonSignalCategory,
        reasonCodeId: null,
        raisedAt: new Date(form.raisedAt).toISOString(),
        notes: form.notes || null,
        raisedByOperatorId: null,
        productionOrderId: null
      });
      toast.success(t('toasts.created'));
    }
    await refreshAll();
    modalOpen.value = false;
    editing.value = null;
    resolving.value = false;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

const confirmOpen = ref(false);
const toDelete = ref<AndonSignalResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value
  ? `${t('common.delete')}: ${machineLabel(toDelete.value.machineId)} — ${categoryLabel(toDelete.value.category)}`
  : '');

async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await andonSignalService.remove(toDelete.value.id);
    toast.success(t('toasts.deleted'));
    await refreshAll();
    confirmOpen.value = false;
    toDelete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally {
    deleting.value = false;
  }
}
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }

onMounted(async () => {
  try {
    const page = await machineService.browse({ pageSize: 100 });
    machines.value = page.items;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  }
  await refreshAll();
});
</script>

<style scoped>
.board-loading {
  display: flex;
  justify-content: center;
  padding: var(--space-4);
}
.board {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: var(--space-3);
}
.board-card {
  border: 1px solid var(--color-border);
  border-left-width: 4px;
  border-radius: var(--radius-md);
  padding: var(--space-3);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  background: var(--color-surface);
}
.board-card--active { border-left-color: var(--color-danger); }
.board-card--acknowledged { border-left-color: var(--color-warning); }
.board-card--resolved { border-left-color: var(--color-success); }
.board-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}
.board-card__category { font-weight: 600; }
.board-card__time { font-size: var(--font-size-sm); opacity: 0.8; }
.board-card__notes { font-size: var(--font-size-sm); }
.board-card__actions { display: flex; gap: var(--space-2); }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
