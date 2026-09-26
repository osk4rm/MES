<template>
  <div>
    <AppPageHeader :title="$t('downtime.title')" :subtitle="$t('downtime.subtitle')" icon="pi pi-pause-circle">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openStart">{{ $t('downtime.start') }}</AppButton>
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
        v-model="reasonFilter"
        :options="reasonFilterOptions"
        allow-empty
        @change="onReasonChange"
      />
      <AppSelect
        v-model="statusFilter"
        :options="statusFilterOptions"
        allow-empty
        @change="onStatusChange"
      />
      <AppInput v-model="fromFilter" type="datetime-local" @change="onFromChange" />
      <AppInput v-model="toFilter" type="datetime-local" @change="onToChange" />
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
        {{ machineName(String(value)) }}
      </template>
      <template #cell-reasonCodeId="{ value }">
        {{ reasonName(String(value)) }}
      </template>
      <template #cell-startedAt="{ value }">
        {{ formatDate(String(value)) }}
      </template>
      <template #cell-endedAt="{ value }">
        {{ value ? formatDate(String(value)) : '—' }}
      </template>
      <template #cell-durationMinutes="{ value }">
        {{ value === null || value === undefined ? '—' : formatDuration(Number(value)) }}
      </template>
      <template #cell-status="{ value }">
        <AppBadge :variant="Number(value) === DowntimeEventStatus.Open ? 'warning' : 'idle'" dot>
          {{ statusLabel(Number(value)) }}
        </AppBadge>
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

    <AppModal :open="startOpen" :title="$t('downtime.start')" @close="closeStart">
      <form id="downtime-start-form" class="form-grid" @submit.prevent="onStartSave">
        <AppFormField :label="$t('downtime.machine')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="startForm.machineId" :options="machineOptions" :placeholder="$t('downtime.selectMachine')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('downtime.reasonCode')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="startForm.reasonCodeId" :options="reasonOptions" :placeholder="$t('downtime.selectReason')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('downtime.startedAt')" required class="form-grid__full">
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="startForm.startedAt" type="datetime-local" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('downtime.order')" class="form-grid__full">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="startForm.productionOrderId" :options="orderOptions" :placeholder="$t('downtime.selectOrder')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('downtime.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="startForm.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeStart">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="downtime-start-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppModal :open="closeOpen" :title="$t('downtime.closeTitle')" @close="cancelClose">
      <form id="downtime-close-form" class="form-grid" @submit.prevent="confirmClose">
        <AppFormField :label="$t('downtime.endedAt')" required class="form-grid__full">
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="closeForm.endedAt" type="datetime-local" required :invalid="invalid" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="closing" @click="cancelClose">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="downtime-close-form" variant="primary" :loading="closing">{{ $t('downtime.close') }}</AppButton>
      </template>
    </AppModal>

    <AppModal :open="editOpen" :title="$t('common.edit')" @close="closeEdit">
      <form id="downtime-edit-form" class="form-grid" @submit.prevent="onEditSave">
        <AppFormField :label="$t('downtime.reasonCode')" required class="form-grid__full">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="editForm.reasonCodeId" :options="reasonOptions" :placeholder="$t('downtime.selectReason')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('downtime.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="editForm.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeEdit">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="downtime-edit-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppConfirmDialog
      :open="confirmOpen"
      :title="$t('common.delete')"
      :message="deleteMessage"
      :loading="deleting"
      @confirm="doDelete"
      @cancel="cancelDelete"
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
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import {
  downtimeEventService,
  DowntimeEventStatus,
  type DowntimeEventResponse
} from '../../services/downtimeEventService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { reasonCodeService, type ReasonCodeResponse } from '../../services/reasonCodeService';
import { productionOrderService, ProductionOrderStatus, type ProductionOrderResponse } from '../../services/productionOrderService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

interface Filters {
  machineId?: string;
  reasonCodeId?: string;
  status?: DowntimeEventStatus;
  startedFrom?: string;
  startedTo?: string;
}

const table = useCrudPage<DowntimeEventResponse, Filters>({
  fetch: (req) => downtimeEventService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'machineId', label: t('downtime.machine'), sortable: true },
  { key: 'reasonCodeId', label: t('downtime.reasonCode'), sortable: false },
  { key: 'startedAt', label: t('downtime.startedAt'), sortable: true },
  { key: 'endedAt', label: t('downtime.endedAt'), sortable: true },
  { key: 'durationMinutes', label: t('downtime.duration'), sortable: false, align: 'right' as const },
  { key: 'status', label: t('common.status'), sortable: false, width: '120px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const machines = ref<MachineResponse[]>([]);
const reasons = ref<ReasonCodeResponse[]>([]);
const orders = ref<ProductionOrderResponse[]>([]);

const machineOptions = computed<SelectOption[]>(() =>
  machines.value.map(m => ({ value: m.id, label: `${m.code} — ${m.name}` })));
const reasonOptions = computed<SelectOption[]>(() =>
  reasons.value.map(r => ({ value: r.id, label: `${r.code} — ${r.name}` })));
const orderOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('downtime.selectOrder') },
  ...orders.value
    .filter(o => o.status === ProductionOrderStatus.Released || o.status === ProductionOrderStatus.InProgress)
    .map(o => ({ value: o.id, label: o.code }))
]);

const machineFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('downtime.filters.machine') },
  ...machineOptions.value
]);
const reasonFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('downtime.filters.reason') },
  ...reasonOptions.value
]);
const statusFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('common.status') },
  { value: DowntimeEventStatus.Open, label: t('downtime.status.open') },
  { value: DowntimeEventStatus.Closed, label: t('downtime.status.closed') }
]);

function machineName(id: string): string {
  return machines.value.find(m => m.id === id)?.name ?? id;
}
function reasonName(id: string): string {
  return reasons.value.find(r => r.id === id)?.name ?? id;
}
function statusLabel(v: number): string {
  return v === DowntimeEventStatus.Open ? t('downtime.status.open') : t('downtime.status.closed');
}
function formatDate(d: string): string {
  return new Date(d).toLocaleString();
}
function formatDuration(minutes: number): string {
  if (minutes < 60) return `${Math.round(minutes)} ${t('downtime.minutes')}`;
  const h = Math.floor(minutes / 60);
  const m = Math.round(minutes % 60);
  return `${h}h ${m}m`;
}
function toLocalInput(d: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
function fromLocalInput(v: string): string {
  return new Date(v).toISOString();
}

const machineFilter = ref<string | number | null>(null);
const reasonFilter = ref<string | number | null>(null);
const statusFilter = ref<string | number | null>(null);
const fromFilter = ref<string>('');
const toFilter = ref<string>('');

function onMachineChange(v: string | number | null): void {
  table.setFilter('machineId', v === null ? undefined : String(v));
}
function onReasonChange(v: string | number | null): void {
  table.setFilter('reasonCodeId', v === null ? undefined : String(v));
}
function onStatusChange(v: string | number | null): void {
  table.setFilter('status', v === null ? undefined : Number(v) as DowntimeEventStatus);
}
function onFromChange(): void {
  table.setFilter('startedFrom', fromFilter.value ? fromLocalInput(fromFilter.value) : undefined);
}
function onToChange(): void {
  table.setFilter('startedTo', toFilter.value ? fromLocalInput(toFilter.value) : undefined);
}
function clearFilters(): void {
  machineFilter.value = null;
  reasonFilter.value = null;
  statusFilter.value = null;
  fromFilter.value = '';
  toFilter.value = '';
  table.resetFilters();
}

function rowActions(item: DowntimeEventResponse): Array<{ key: string; label: string; icon: string; variant?: 'danger' }> {
  if (item.status === DowntimeEventStatus.Open) {
    return [
      { key: 'close', label: t('downtime.close'), icon: 'pi-check' },
      { key: 'edit', label: t('common.edit'), icon: 'pi-pencil' },
      { key: 'delete', label: t('common.delete'), icon: 'pi-trash', variant: 'danger' }
    ];
  }
  return [];
}

const startOpen = ref(false);
const saving = ref(false);
const startForm = reactive({
  machineId: null as string | number | null,
  reasonCodeId: null as string | number | null,
  startedAt: '',
  productionOrderId: null as string | number | null,
  notes: ''
});

function openStart(): void {
  Object.assign(startForm, { machineId: null, reasonCodeId: null, startedAt: toLocalInput(new Date()), productionOrderId: null, notes: '' });
  startOpen.value = true;
}
function closeStart(): void {
  if (saving.value) return;
  startOpen.value = false;
}
async function onStartSave(): Promise<void> {
  if (startForm.machineId === null || startForm.reasonCodeId === null || !startForm.startedAt) return;
  saving.value = true;
  try {
    await downtimeEventService.start({
      machineId: String(startForm.machineId),
      reasonCodeId: String(startForm.reasonCodeId),
      startedAt: fromLocalInput(startForm.startedAt),
      notes: startForm.notes || null,
      productionOrderId: startForm.productionOrderId === null ? null : String(startForm.productionOrderId)
    });
    toast.success(t('toasts.created'));
    await table.fetch();
    startOpen.value = false;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

const closeOpen = ref(false);
const closing = ref(false);
const toClose = ref<DowntimeEventResponse | null>(null);
const closeForm = reactive({ endedAt: '' });

function openClose(item: DowntimeEventResponse): void {
  toClose.value = item;
  closeForm.endedAt = toLocalInput(new Date());
  closeOpen.value = true;
}
function cancelClose(): void {
  if (closing.value) return;
  closeOpen.value = false;
  toClose.value = null;
}
async function confirmClose(): Promise<void> {
  if (!toClose.value || !closeForm.endedAt) return;
  closing.value = true;
  try {
    await downtimeEventService.close(toClose.value.id, { endedAt: fromLocalInput(closeForm.endedAt) });
    toast.success(t('toasts.updated'));
    await table.fetch();
    closeOpen.value = false;
    toClose.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    closing.value = false;
  }
}

const editOpen = ref(false);
const toEdit = ref<DowntimeEventResponse | null>(null);
const editForm = reactive({
  reasonCodeId: null as string | number | null,
  notes: ''
});

function openEdit(item: DowntimeEventResponse): void {
  toEdit.value = item;
  Object.assign(editForm, { reasonCodeId: item.reasonCodeId, notes: item.notes ?? '' });
  editOpen.value = true;
}
function closeEdit(): void {
  if (saving.value) return;
  editOpen.value = false;
  toEdit.value = null;
}
async function onEditSave(): Promise<void> {
  if (!toEdit.value || editForm.reasonCodeId === null) return;
  saving.value = true;
  try {
    await downtimeEventService.update(toEdit.value.id, {
      id: toEdit.value.id,
      reasonCodeId: String(editForm.reasonCodeId),
      notes: editForm.notes || null
    });
    toast.success(t('toasts.updated'));
    await table.fetch();
    editOpen.value = false;
    toEdit.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

const confirmOpen = ref(false);
const toDelete = ref<DowntimeEventResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${machineName(toDelete.value.machineId)}` : '');

function onRowAction(key: string, item: DowntimeEventResponse): void {
  if (key === 'close') openClose(item);
  else if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function doDelete(): Promise<void> {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await downtimeEventService.remove(toDelete.value.id);
    toast.success(t('toasts.deleted'));
    await table.fetch();
    confirmOpen.value = false;
    toDelete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally {
    deleting.value = false;
  }
}
function cancelDelete(): void {
  confirmOpen.value = false;
  toDelete.value = null;
}

onMounted(async () => {
  try {
    const [m, r, o] = await Promise.all([
      machineService.browse({ pageNumber: 1, pageSize: 100 }),
      reasonCodeService.browse({ pageNumber: 1, pageSize: 100 }),
      productionOrderService.browse({ pageNumber: 1, pageSize: 100 })
    ]);
    machines.value = m.items;
    reasons.value = r.items;
    orders.value = o.items;
  } catch {
    // lookups stay empty; ids are still rendered raw
  }
  await table.fetch();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
