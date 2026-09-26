<template>
  <div>
    <AppPageHeader :title="$t('maintenance.title')" :subtitle="$t('maintenance.subtitle')" icon="pi pi-wrench">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('maintenance.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppSelect
        v-model="machineFilter"
        :options="machineFilterOptions"
        allow-empty
        @change="onMachineChange"
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
      <template #cell-machineId="{ item }">
        {{ machineLabel(item) }}
      </template>
      <template #cell-priority="{ value }">
        {{ priorityLabel(Number(value)) }}
      </template>
      <template #cell-status="{ value }">
        <AppBadge :variant="statusVariant(Number(value))" dot>
          {{ statusLabel(Number(value)) }}
        </AppBadge>
      </template>
      <template #cell-source="{ item }">
        <AppBadge v-if="item.planId" variant="info" dot>
          {{ $t('maintenance.preventive') }}
        </AppBadge>
        <span v-else>—</span>
      </template>
      <template #cell-reportedAt="{ value }">
        {{ formatDate(String(value)) }}
      </template>
      <template #cell-startedAt="{ value }">
        {{ value ? formatDate(String(value)) : '—' }}
      </template>
      <template #cell-completedAt="{ value }">
        {{ value ? formatDate(String(value)) : '—' }}
      </template>
      <template #cell-resolutionNotes="{ value }">
        {{ value ? String(value) : '—' }}
      </template>
      <template #cell-actions="{ item }">
        <AppRowActions
          :actions="rowActions(item)"
          @action="(k) => onRowAction(k, item)"
        />
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

    <AppModal :open="createOpen" :title="$t('maintenance.create')" @close="closeCreate">
      <form id="maintenance-create-form" class="form-grid" @submit.prevent="onCreateSave">
        <AppFormField :label="$t('maintenance.code')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="createForm.code" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('maintenance.orderTitle')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="createForm.title" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('maintenance.machine')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="createForm.machineId" :options="machineOptions" :placeholder="$t('maintenance.selectMachine')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('maintenance.priority')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="createForm.priority" :options="priorityOptions" :placeholder="$t('maintenance.selectPriority')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('maintenance.description')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="createForm.description" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeCreate">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="maintenance-create-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppModal :open="completeOpen" :title="$t('maintenance.completeTitle')" @close="cancelComplete">
      <form id="maintenance-complete-form" class="form-grid" @submit.prevent="confirmComplete">
        <AppFormField :label="$t('maintenance.resolutionNotes')" required class="form-grid__full">
          <template #default="{ id, invalid }">
            <AppTextarea :id="id" v-model="completeForm.resolutionNotes" :rows="3" required :invalid="invalid" :placeholder="$t('maintenance.resolutionNotesPlaceholder')" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="completing" @click="cancelComplete">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="maintenance-complete-form" variant="primary" :loading="completing">{{ $t('maintenance.complete') }}</AppButton>
      </template>
    </AppModal>

    <AppConfirmDialog
      :open="cancelOpen"
      :title="$t('maintenance.cancelOrder')"
      :message="cancelMessage"
      :loading="cancelling"
      @confirm="doCancel"
      @cancel="closeCancel"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppSelect, { type SelectOption } from '../../components/ui/AppSelect.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions, { type RowAction } from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import {
  maintenanceWorkOrderService,
  MaintenanceWorkOrderPriority,
  MaintenanceWorkOrderStatus,
  type MaintenanceWorkOrderResponse
} from '../../services/maintenanceWorkOrderService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

interface Filters {
  machineId?: string;
  status?: MaintenanceWorkOrderStatus;
}

const table = useCrudPage<MaintenanceWorkOrderResponse, Filters>({
  fetch: (req) => maintenanceWorkOrderService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('maintenance.code'), sortable: true },
  { key: 'title', label: t('maintenance.orderTitle'), sortable: true },
  { key: 'machineId', label: t('maintenance.machine'), sortable: false },
  { key: 'priority', label: t('maintenance.priority'), sortable: true },
  { key: 'status', label: t('common.status'), sortable: true, width: '130px' },
  { key: 'source', label: t('maintenance.source'), sortable: false, width: '130px' },
  { key: 'reportedAt', label: t('maintenance.reportedAt'), sortable: true },
  { key: 'startedAt', label: t('maintenance.startedAt'), sortable: false },
  { key: 'completedAt', label: t('maintenance.completedAt'), sortable: false },
  { key: 'resolutionNotes', label: t('maintenance.resolutionNotes'), sortable: false },
  { key: 'actions', label: t('common.actions'), width: '110px' }
]);

const machines = ref<MachineResponse[]>([]);

const machineOptions = computed<SelectOption[]>(() =>
  machines.value.map(m => ({ value: m.id, label: `${m.code} — ${m.name}` })));
const priorityOptions = computed<SelectOption[]>(() => [
  { value: MaintenanceWorkOrderPriority.Low, label: priorityLabel(MaintenanceWorkOrderPriority.Low) },
  { value: MaintenanceWorkOrderPriority.Medium, label: priorityLabel(MaintenanceWorkOrderPriority.Medium) },
  { value: MaintenanceWorkOrderPriority.High, label: priorityLabel(MaintenanceWorkOrderPriority.High) },
  { value: MaintenanceWorkOrderPriority.Critical, label: priorityLabel(MaintenanceWorkOrderPriority.Critical) }
]);

const machineFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('maintenance.filters.machine') },
  ...machineOptions.value
]);
const statusFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('common.status') },
  { value: MaintenanceWorkOrderStatus.Open, label: statusLabel(MaintenanceWorkOrderStatus.Open) },
  { value: MaintenanceWorkOrderStatus.InProgress, label: statusLabel(MaintenanceWorkOrderStatus.InProgress) },
  { value: MaintenanceWorkOrderStatus.Done, label: statusLabel(MaintenanceWorkOrderStatus.Done) },
  { value: MaintenanceWorkOrderStatus.Cancelled, label: statusLabel(MaintenanceWorkOrderStatus.Cancelled) }
]);

function machineLabel(item: MaintenanceWorkOrderResponse): string {
  if (item.machineCode) return item.machineCode;
  return machines.value.find(m => m.id === item.machineId)?.name ?? item.machineId;
}
function statusLabel(v: number): string {
  switch (v) {
    case MaintenanceWorkOrderStatus.Open: return t('maintenance.statuses.open');
    case MaintenanceWorkOrderStatus.InProgress: return t('maintenance.statuses.inProgress');
    case MaintenanceWorkOrderStatus.Done: return t('maintenance.statuses.done');
    case MaintenanceWorkOrderStatus.Cancelled: return t('maintenance.statuses.cancelled');
    default: return String(v);
  }
}
function statusVariant(v: number): 'warning' | 'info' | 'success' | 'idle' {
  switch (v) {
    case MaintenanceWorkOrderStatus.Open: return 'warning';
    case MaintenanceWorkOrderStatus.InProgress: return 'info';
    case MaintenanceWorkOrderStatus.Done: return 'success';
    default: return 'idle';
  }
}
function priorityLabel(v: number): string {
  switch (v) {
    case MaintenanceWorkOrderPriority.Low: return t('maintenance.priorities.low');
    case MaintenanceWorkOrderPriority.Medium: return t('maintenance.priorities.medium');
    case MaintenanceWorkOrderPriority.High: return t('maintenance.priorities.high');
    case MaintenanceWorkOrderPriority.Critical: return t('maintenance.priorities.critical');
    default: return String(v);
  }
}
function formatDate(d: string): string {
  return new Date(d).toLocaleString();
}

const machineFilter = ref<string | number | null>(null);
const statusFilter = ref<string | number | null>(null);

function onMachineChange(v: string | number | null): void {
  table.setFilter('machineId', v === null ? undefined : String(v));
}
function onStatusChange(v: string | number | null): void {
  table.setFilter('status', v === null ? undefined : Number(v) as MaintenanceWorkOrderStatus);
}
function clearFilters(): void {
  machineFilter.value = null;
  statusFilter.value = null;
  table.resetFilters();
}

function rowActions(item: MaintenanceWorkOrderResponse): RowAction[] {
  if (item.status === MaintenanceWorkOrderStatus.Open) {
    return [
      { key: 'start', label: t('maintenance.start'), icon: 'pi-play' },
      { key: 'complete', label: t('maintenance.complete'), icon: 'pi-check' },
      { key: 'cancel', label: t('maintenance.cancelOrder'), icon: 'pi-times', variant: 'danger' }
    ];
  }
  if (item.status === MaintenanceWorkOrderStatus.InProgress) {
    return [
      { key: 'complete', label: t('maintenance.complete'), icon: 'pi-check' },
      { key: 'cancel', label: t('maintenance.cancelOrder'), icon: 'pi-times', variant: 'danger' }
    ];
  }
  return [];
}

const createOpen = ref(false);
const saving = ref(false);
const createForm = reactive({
  code: '',
  title: '',
  machineId: null as string | number | null,
  priority: null as number | null,
  description: ''
});

function openCreate(): void {
  Object.assign(createForm, { code: '', title: '', machineId: null, priority: null, description: '' });
  createOpen.value = true;
}
function closeCreate(): void {
  if (saving.value) return;
  createOpen.value = false;
}
async function onCreateSave(): Promise<void> {
  if (!createForm.code.trim() || !createForm.title.trim() || createForm.machineId === null || createForm.priority === null) return;
  saving.value = true;
  try {
    await maintenanceWorkOrderService.create({
      code: createForm.code.trim(),
      title: createForm.title.trim(),
      description: createForm.description.trim() || null,
      machineId: String(createForm.machineId),
      priority: createForm.priority as MaintenanceWorkOrderPriority
    });
    toast.success(t('toasts.created'));
    await table.fetch();
    createOpen.value = false;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

async function doStart(item: MaintenanceWorkOrderResponse): Promise<void> {
  try {
    await maintenanceWorkOrderService.start(item.id);
    toast.success(t('toasts.updated'));
    await table.fetch();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}

const completeOpen = ref(false);
const completing = ref(false);
const toComplete = ref<MaintenanceWorkOrderResponse | null>(null);
const completeForm = reactive({ resolutionNotes: '' });

function openComplete(item: MaintenanceWorkOrderResponse): void {
  toComplete.value = item;
  completeForm.resolutionNotes = '';
  completeOpen.value = true;
}
function cancelComplete(): void {
  if (completing.value) return;
  completeOpen.value = false;
  toComplete.value = null;
}
async function confirmComplete(): Promise<void> {
  if (!toComplete.value || !completeForm.resolutionNotes.trim()) return;
  completing.value = true;
  try {
    await maintenanceWorkOrderService.complete(toComplete.value.id, {
      resolutionNotes: completeForm.resolutionNotes.trim()
    });
    toast.success(t('toasts.updated'));
    await table.fetch();
    completeOpen.value = false;
    toComplete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    completing.value = false;
  }
}

const cancelOpen = ref(false);
const toCancel = ref<MaintenanceWorkOrderResponse | null>(null);
const cancelling = ref(false);
const cancelMessage = computed(() => toCancel.value ? `${t('maintenance.cancelOrder')}: ${toCancel.value.code}` : '');

function openCancel(item: MaintenanceWorkOrderResponse): void {
  toCancel.value = item;
  cancelOpen.value = true;
}
function closeCancel(): void {
  if (cancelling.value) return;
  cancelOpen.value = false;
  toCancel.value = null;
}
async function doCancel(): Promise<void> {
  if (!toCancel.value) return;
  cancelling.value = true;
  try {
    await maintenanceWorkOrderService.cancel(toCancel.value.id);
    toast.success(t('toasts.updated'));
    await table.fetch();
    cancelOpen.value = false;
    toCancel.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    cancelling.value = false;
  }
}

function onRowAction(key: string, item: MaintenanceWorkOrderResponse): void {
  if (key === 'start') void doStart(item);
  else if (key === 'complete') openComplete(item);
  else if (key === 'cancel') openCancel(item);
}

onMounted(async () => {
  try {
    const m = await machineService.browse({ pageNumber: 1, pageSize: 100 });
    machines.value = m.items;
  } catch {
    // lookups stay empty; machine codes from the API are still rendered
  }
  await table.fetch();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
