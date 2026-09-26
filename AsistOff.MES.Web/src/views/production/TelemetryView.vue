<template>
  <div>
    <AppPageHeader :title="$t('telemetry.title')" :subtitle="$t('telemetry.subtitle')" icon="pi pi-wave-pulse">
      <template #actions>
        <AppBadge :variant="simulatorEnabled === true ? 'success' : 'idle'" dot>
          {{ simulatorEnabled === true ? $t('telemetry.simulator.enabled') : $t('telemetry.simulator.disabled') }}
        </AppBadge>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="refreshAll">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="secondary" icon="pi pi-chart-line" @click="goDashboard">{{ $t('telemetryDashboard.title') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('telemetry.create') }}</AppButton>
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
        v-model="enabledFilter"
        :options="enabledFilterOptions"
        allow-empty
        @change="onEnabledChange"
      />
      <AppInput
        v-model="searchFilter"
        :placeholder="$t('telemetry.filters.search')"
        prefix-icon="pi pi-search"
        clearable
        @update:model-value="onSearch"
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
        {{ machineName(String(value)) }}
      </template>
      <template #cell-dataType="{ value }">
        {{ dataTypeLabel(Number(value)) }}
      </template>
      <template #cell-pollIntervalSeconds="{ value }">
        {{ value }}s
      </template>
      <template #cell-isEnabled="{ value }">
        <AppBadge :variant="value ? 'success' : 'idle'" dot>
          {{ value ? $t('telemetry.enabled') : $t('telemetry.disabled') }}
        </AppBadge>
      </template>
      <template #cell-connection="{ item }">
        <AppBadge :variant="connectionVariant(item.id)" dot>
          {{ connectionLabel(item.id) }}
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

    <AppModal :open="formOpen" :title="editing ? $t('common.edit') : $t('telemetry.create')" @close="closeForm">
      <form id="telemetry-tag-form" class="form-grid" @submit.prevent="onFormSave">
        <AppFormField v-if="!editing" :label="$t('telemetry.machine')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.machineId" :options="machineOptions" :placeholder="$t('telemetry.selectMachine')" />
          </template>
        </AppFormField>
        <AppFormField v-if="!editing" :label="$t('telemetry.nodeId')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.nodeId" required :invalid="invalid" placeholder="ns=2;s=Line1.Oven.Temperature" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('telemetry.displayName')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.displayName" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('telemetry.dataType')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.dataType" :options="dataTypeOptions" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('telemetry.pollInterval')" required class="form-grid__full">
          <template #default="{ id, invalid }">
            <AppNumberInput :id="id" v-model="form.pollIntervalSeconds" :min="1" :max="3600" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('telemetry.description')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.description" :rows="2" />
          </template>
        </AppFormField>
        <AppFormField v-if="editing" :label="$t('telemetry.enabled')" class="form-grid__full">
          <template #default>
            <AppCheckbox v-model="form.isEnabled" :label="$t('telemetry.enabled')" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeForm">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="telemetry-tag-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppModal :open="readingsOpen" :title="readingsTitle" @close="closeReadings">
      <form id="telemetry-reading-form" class="form-grid" @submit.prevent="onReadingSubmit">
        <AppFormField :label="$t('telemetry.readAt')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="readingForm.readAt" type="datetime-local" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('telemetry.value')" required>
          <template #default="{ id, invalid }">
            <AppInput v-if="readingIsText" :id="id" v-model="readingForm.textValue" required :invalid="invalid" />
            <AppNumberInput v-else :id="id" v-model="readingForm.numericValue" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('telemetry.quality')" required class="form-grid__full">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="readingForm.quality" :options="qualityOptions" />
          </template>
        </AppFormField>
      </form>
      <div class="readings-actions">
        <AppButton type="submit" form="telemetry-reading-form" variant="secondary" size="sm" :loading="submitting" icon="pi pi-plus">
          {{ $t('telemetry.submitReading') }}
        </AppButton>
      </div>
      <AppSpinner v-if="readingsLoading" />
      <AppEmptyState v-else-if="readings.length === 0" :title="$t('telemetry.noReadings')" />
      <ul v-else class="readings-list">
        <li v-for="r in readings" :key="r.id" class="readings-list__row">
          <span class="readings-list__value">{{ readingValue(r) }}</span>
          <span class="readings-list__time">{{ formatDate(r.readAt) }}</span>
          <AppBadge :variant="qualityVariant(r.quality)" dot>{{ qualityLabel(r.quality) }}</AppBadge>
        </li>
      </ul>
      <template #footer>
        <AppButton variant="ghost" @click="closeReadings">{{ $t('common.close') }}</AppButton>
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
import { useRouter } from 'vue-router';
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
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import {
  telemetryTagService,
  telemetryReadingService,
  TelemetryDataType,
  TelemetryQuality,
  type MachineTelemetryTagResponse,
  type TelemetryReadingResponse,
  type TelemetryTagStatusEntry
} from '../../services/telemetryService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const router = useRouter();
const toast = useToastStore();

interface Filters {
  machineId?: string;
  isEnabled?: boolean;
  search?: string;
}

const table = useCrudPage<MachineTelemetryTagResponse, Filters>({
  fetch: (req) => telemetryTagService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'machineId', label: t('telemetry.machine'), sortable: false },
  { key: 'nodeId', label: t('telemetry.nodeId'), sortable: true },
  { key: 'displayName', label: t('telemetry.displayName'), sortable: true },
  { key: 'dataType', label: t('telemetry.dataType'), sortable: false, width: '110px' },
  { key: 'pollIntervalSeconds', label: t('telemetry.pollInterval'), sortable: false, align: 'right' as const, width: '110px' },
  { key: 'isEnabled', label: t('common.status'), sortable: false, width: '120px' },
  { key: 'connection', label: t('telemetry.connection'), sortable: false, width: '130px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const machines = ref<MachineResponse[]>([]);
const statusByTag = ref<Map<string, TelemetryTagStatusEntry>>(new Map());
const simulatorEnabled = ref<boolean | null>(null);

function connectionEntry(id: string): TelemetryTagStatusEntry | undefined {
  return statusByTag.value.get(id);
}
function connectionVariant(id: string): 'success' | 'warning' | 'idle' {
  const entry = connectionEntry(id);
  if (!entry || !entry.lastReadAt) return 'idle';
  return entry.stale ? 'warning' : 'success';
}
function connectionLabel(id: string): string {
  const entry = connectionEntry(id);
  if (!entry || !entry.lastReadAt) return t('telemetry.status.never');
  return entry.stale ? t('telemetry.status.stale') : t('telemetry.status.recent');
}

async function fetchStatus(): Promise<void> {
  try {
    const status = await telemetryTagService.getStatus();
    simulatorEnabled.value = status.simulatorEnabled;
    statusByTag.value = new Map(status.tags.map((e) => [e.tagId, e]));
  } catch {
    simulatorEnabled.value = null;
    statusByTag.value = new Map();
  }
}

async function refreshAll(): Promise<void> {
  await Promise.all([table.fetch(), fetchStatus()]);
}

function goDashboard(): void {
  void router.push('/production/telemetry-dashboard');
}
const machineOptions = computed<SelectOption[]>(() =>
  machines.value.map(m => ({ value: m.id, label: `${m.code} — ${m.name}` })));
const machineFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('telemetry.filters.machine') },
  ...machineOptions.value
]);
const enabledFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('common.status') },
  { value: 'true', label: t('telemetry.enabled') },
  { value: 'false', label: t('telemetry.disabled') }
]);
const dataTypeOptions = computed<SelectOption[]>(() => ([
  TelemetryDataType.Boolean,
  TelemetryDataType.Double,
  TelemetryDataType.Integer,
  TelemetryDataType.String
] as TelemetryDataType[]).map(v => ({ value: v, label: dataTypeLabel(v) })));
const qualityOptions = computed<SelectOption[]>(() => ([
  TelemetryQuality.Good,
  TelemetryQuality.Bad,
  TelemetryQuality.Uncertain
] as TelemetryQuality[]).map(v => ({ value: v, label: qualityLabel(v) })));

function machineName(id: string): string {
  return machines.value.find(m => m.id === id)?.name ?? id;
}
function dataTypeLabel(v: number): string {
  return t(`telemetry.dataTypes.${v}`);
}
function qualityLabel(v: number): string {
  return t(`telemetry.qualities.${v}`);
}
function qualityVariant(v: number): 'success' | 'danger' | 'warning' {
  if (v === TelemetryQuality.Good) return 'success';
  if (v === TelemetryQuality.Bad) return 'danger';
  return 'warning';
}
function formatDate(d: string): string {
  return new Date(d).toLocaleString();
}
function readingValue(r: TelemetryReadingResponse): string {
  return r.stringValue ?? (r.doubleValue !== null && r.doubleValue !== undefined ? String(r.doubleValue) : '—');
}
function toLocalInput(d: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}
function fromLocalInput(v: string): string {
  return new Date(v).toISOString();
}

const machineFilter = ref<string | number | null>(null);
const enabledFilter = ref<string | number | null>(null);
const searchFilter = ref('');

function onMachineChange(v: string | number | null): void {
  table.setFilter('machineId', v === null ? undefined : String(v));
}
function onEnabledChange(v: string | number | null): void {
  table.setFilter('isEnabled', v === null ? undefined : v === 'true');
}
let searchTimer = 0;
function onSearch(v: string | number | null | undefined): void {
  window.clearTimeout(searchTimer);
  searchTimer = window.setTimeout(() => table.setFilter('search', v ? String(v) : undefined), 300);
}
function clearFilters(): void {
  machineFilter.value = null;
  enabledFilter.value = null;
  searchFilter.value = '';
  table.resetFilters();
}

function rowActions(item: MachineTelemetryTagResponse): Array<{ key: string; label: string; icon: string; variant?: 'danger' }> {
  return [
    { key: 'readings', label: t('telemetry.readings'), icon: 'pi-wave-pulse' },
    { key: 'toggle', label: item.isEnabled ? t('telemetry.disable') : t('telemetry.enable'), icon: 'pi-power-off' },
    { key: 'edit', label: t('common.edit'), icon: 'pi-pencil' },
    { key: 'delete', label: t('common.delete'), icon: 'pi-trash', variant: 'danger' }
  ];
}

const formOpen = ref(false);
const editing = ref<MachineTelemetryTagResponse | null>(null);
const saving = ref(false);
const form = reactive({
  machineId: null as string | number | null,
  nodeId: '',
  displayName: '',
  dataType: TelemetryDataType.Double as TelemetryDataType,
  pollIntervalSeconds: 30 as number | null,
  description: '',
  isEnabled: true
});

function openCreate(): void {
  editing.value = null;
  Object.assign(form, {
    machineId: null, nodeId: '', displayName: '',
    dataType: TelemetryDataType.Double, pollIntervalSeconds: 30,
    description: '', isEnabled: true
  });
  formOpen.value = true;
}
function openEdit(item: MachineTelemetryTagResponse): void {
  editing.value = item;
  Object.assign(form, {
    machineId: item.machineId, nodeId: item.nodeId, displayName: item.displayName,
    dataType: item.dataType, pollIntervalSeconds: item.pollIntervalSeconds,
    description: item.description ?? '', isEnabled: item.isEnabled
  });
  formOpen.value = true;
}
function closeForm(): void {
  if (saving.value) return;
  formOpen.value = false;
  editing.value = null;
}
async function onFormSave(): Promise<void> {
  if (form.pollIntervalSeconds === null) return;
  if (!editing.value && (form.machineId === null || !form.nodeId.trim() || !form.displayName.trim())) return;
  if (editing.value && !form.displayName.trim()) return;
  saving.value = true;
  try {
    if (editing.value) {
      await telemetryTagService.update(editing.value.id, {
        id: editing.value.id,
        displayName: form.displayName.trim(),
        dataType: form.dataType,
        pollIntervalSeconds: form.pollIntervalSeconds,
        isEnabled: form.isEnabled,
        description: form.description || null
      });
      toast.success(t('toasts.updated'));
    } else {
      await telemetryTagService.create({
        machineId: String(form.machineId),
        nodeId: form.nodeId.trim(),
        displayName: form.displayName.trim(),
        dataType: form.dataType,
        pollIntervalSeconds: form.pollIntervalSeconds,
        description: form.description || null
      });
      toast.success(t('toasts.created'));
    }
    await refreshAll();
    formOpen.value = false;
    editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

async function onToggle(item: MachineTelemetryTagResponse): Promise<void> {
  try {
    await telemetryTagService.toggle(item.id);
    toast.success(t('toasts.updated'));
    await refreshAll();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}

const confirmOpen = ref(false);
const toDelete = ref<MachineTelemetryTagResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.displayName}` : '');

function onRowAction(key: string, item: MachineTelemetryTagResponse): void {
  if (key === 'readings') void openReadings(item);
  else if (key === 'toggle') void onToggle(item);
  else if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function doDelete(): Promise<void> {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await telemetryTagService.remove(toDelete.value.id);
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
function cancelDelete(): void {
  confirmOpen.value = false;
  toDelete.value = null;
}

const readingsOpen = ref(false);
const readingsTag = ref<MachineTelemetryTagResponse | null>(null);
const readings = ref<TelemetryReadingResponse[]>([]);
const readingsLoading = ref(false);
const submitting = ref(false);
const readingsTitle = computed(() => readingsTag.value
  ? `${t('telemetry.readings')}: ${readingsTag.value.displayName}`
  : t('telemetry.readings'));
const readingIsText = computed(() => readingsTag.value?.dataType === TelemetryDataType.String);
const readingForm = reactive({
  readAt: '',
  numericValue: null as number | null,
  textValue: '',
  quality: TelemetryQuality.Good as TelemetryQuality
});

async function openReadings(item: MachineTelemetryTagResponse): Promise<void> {
  readingsTag.value = item;
  readingForm.readAt = toLocalInput(new Date());
  readingForm.numericValue = null;
  readingForm.textValue = '';
  readingForm.quality = TelemetryQuality.Good;
  readingsOpen.value = true;
  await fetchReadings();
}
function closeReadings(): void {
  readingsOpen.value = false;
  readingsTag.value = null;
  readings.value = [];
}
async function fetchReadings(): Promise<void> {
  if (!readingsTag.value) return;
  readingsLoading.value = true;
  try {
    const page = await telemetryReadingService.browse({
      tagId: readingsTag.value.id,
      pageNumber: 1,
      pageSize: 10
    });
    readings.value = page.items;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    readingsLoading.value = false;
  }
}
async function onReadingSubmit(): Promise<void> {
  if (!readingsTag.value || !readingForm.readAt) return;
  if (readingIsText.value && !readingForm.textValue) return;
  if (!readingIsText.value && readingForm.numericValue === null) return;
  submitting.value = true;
  try {
    await telemetryReadingService.submit({
      tagId: readingsTag.value.id,
      readAt: fromLocalInput(readingForm.readAt),
      doubleValue: readingIsText.value ? null : readingForm.numericValue,
      stringValue: readingIsText.value ? readingForm.textValue : null,
      quality: readingForm.quality
    });
    toast.success(t('toasts.created'));
    await fetchReadings();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    submitting.value = false;
  }
}

onMounted(async () => {
  try {
    const m = await machineService.browse({ pageNumber: 1, pageSize: 100 });
    machines.value = m.items;
  } catch {
    // lookups stay empty; ids are still rendered raw
  }
  await refreshAll();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.readings-actions { margin: var(--space-3) 0; }
.readings-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-2); }
.readings-list__row { display: flex; align-items: center; gap: var(--space-3); padding: var(--space-2) 0; border-bottom: 1px solid var(--color-border); }
.readings-list__value { font-weight: 600; min-width: 90px; }
.readings-list__time { color: var(--color-text-muted); flex: 1; }
</style>
