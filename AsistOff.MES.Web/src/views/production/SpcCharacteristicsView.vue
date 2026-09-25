<template>
  <div>
    <AppPageHeader :title="$t('spcCharacteristics.title')" :subtitle="$t('spcCharacteristics.subtitle')" icon="pi pi-chart-line">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('spcCharacteristics.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="searchFilter" :placeholder="$t('spcCharacteristics.filters.search')" prefix-icon="pi pi-search" clearable @update:modelValue="onSearch" />
      <AppSelect
        v-model="productFilter"
        :options="productFilterOptions"
        allow-empty
        @change="onProductChange"
      />
      <AppSelect
        v-model="machineFilter"
        :options="machineFilterOptions"
        allow-empty
        @change="onMachineChange"
      />
      <AppSelect
        v-model="activeFilter"
        :options="activeOptions"
        allow-empty
        @change="onActiveChange"
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
      <template #cell-code="{ item }">
        <code>{{ item.code }}</code>
      </template>
      <template #cell-chartType="{ value }">
        {{ chartTypeLabel(value) }}
      </template>
      <template #cell-isActive="{ value }">
        <AppBadge :variant="value ? 'success' : 'idle'" dot>
          {{ value ? $t('common.active') : $t('common.inactive') }}
        </AppBadge>
      </template>
      <template #cell-actions="{ item }">
        <AppRowActions
          :actions="[
            { key: 'measurements', label: $t('spcCharacteristics.measurements'), icon: 'pi-chart-line' },
            { key: 'edit', label: $t('common.edit'), icon: 'pi-pencil' },
            { key: 'delete', label: $t('common.delete'), icon: 'pi-trash', variant: 'danger' }
          ]"
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('spcCharacteristics.create')" @close="closeModal">
      <form id="spc-characteristic-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('spcCharacteristics.code')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.code" required :invalid="invalid" :disabled="editing !== null" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.name')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.name" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.chartType')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.chartType" :options="chartTypeOptions" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.sampleSize')" required>
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.sampleSize" :min="1" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.nominalValue')">
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.nominalValue" :step="'any'" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.unit')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.unit" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.lowerSpecLimit')">
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.lowerSpecLimit" :step="'any'" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.upperSpecLimit')">
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.upperSpecLimit" :step="'any'" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.lowerControlLimit')">
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.lowerControlLimit" :step="'any'" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.upperControlLimit')">
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.upperControlLimit" :step="'any'" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.product')">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.productId" :options="productOptions" allow-empty />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.machine')">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.machineId" :options="machineOptions" allow-empty />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcCharacteristics.description')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.description" :rows="2" />
          </template>
        </AppFormField>
        <div class="form-grid__full">
          <AppCheckbox v-model="form.isActive" :label="$t('spcCharacteristics.isActive')" />
        </div>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="spc-characteristic-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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

    <AppModal :open="measurementsOpen" :title="measurementsTitle" @close="closeMeasurements">
      <AppFilterBar @clear="clearMeasurementsRange">
        <AppFormField :label="$t('spcMeasurements.from')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="measurementsFrom" type="datetime-local" @change="onMeasurementsRangeChange" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('spcMeasurements.to')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="measurementsTo" type="datetime-local" @change="onMeasurementsRangeChange" />
          </template>
        </AppFormField>
        <template #actions>
          <AppButton variant="primary" size="sm" :loading="measurementsLoading" @click="applyMeasurementsRange">
            {{ $t('spcMeasurements.apply') }}
          </AppButton>
        </template>
      </AppFilterBar>

      <AppSpinner v-if="measurementsLoading && !measurementsLoaded" />
      <AppEmptyState
        v-else-if="measurementsNotFound"
        icon="pi pi-exclamation-circle"
        :title="$t('spcMeasurements.notFound')"
        :description="$t('spcMeasurements.notFoundHint')"
      />
      <AppEmptyState
        v-else-if="measurementsLoaded && measurementsEmpty"
        icon="pi pi-inbox"
        :title="$t('spcMeasurements.noMeasurements')"
      />
      <template v-else-if="measurementsLoaded && chart">
        <p class="measurements-summary">{{ measurementsSummary }}</p>
        <p v-if="measurementsLoading" class="measurements-refresh" data-testid="measurements-refresh">{{ $t('common.loading') }}</p>
        <SpcControlChart
          :points="chart.points"
          :lower-control-limit="chart.lowerControlLimit"
          :upper-control-limit="chart.upperControlLimit"
          :lower-spec-limit="chart.lowerSpecLimit"
          :upper-spec-limit="chart.upperSpecLimit"
          :nominal-value="chart.nominalValue"
          :empty-label="$t('spcMeasurements.chartEmpty')"
        />
        <h4 class="measurements-subtitle">{{ $t('spcMeasurements.logTitle') }}</h4>
        <AppTable
          :items="logItems"
          :columns="measurementColumns"
          :loading="measurementsLoading"
          :empty-label="$t('spcMeasurements.noMeasurements')"
        >
          <template #cell-measuredAt="{ value }">{{ formatDateTime(String(value)) }}</template>
          <template #cell-status="{ item }">
            <AppBadge v-if="measurementStatus(item) === 'ooc'" variant="danger" dot>{{ $t('spcMeasurements.outOfControl') }}</AppBadge>
            <AppBadge v-else-if="measurementStatus(item) === 'oos'" variant="warning" dot>{{ $t('spcMeasurements.outOfSpec') }}</AppBadge>
            <AppBadge v-else variant="success" dot>{{ $t('spcMeasurements.inControl') }}</AppBadge>
          </template>
        </AppTable>
      </template>
      <template #footer>
        <AppButton variant="ghost" @click="closeMeasurements">{{ $t('common.close') }}</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import SpcControlChart from '../../components/production/SpcControlChart.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import {
  spcCharacteristicService,
  SpcChartType,
  type SpcCharacteristicResponse
} from '../../services/spcCharacteristicService';
import {
  spcMeasurementService,
  type SpcMeasurementChartResponse,
  type SpcMeasurementResponse
} from '../../services/spcMeasurementService';
import { productService, type ProductResponse } from '../../services/productService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();

interface Filters { search?: string; productId?: string; machineId?: string; isActive?: boolean }

const table = useCrudPage<SpcCharacteristicResponse, Filters>({
  fetch: (req) => spcCharacteristicService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('spcCharacteristics.code'), sortable: true },
  { key: 'name', label: t('spcCharacteristics.name'), sortable: true },
  { key: 'chartType', label: t('spcCharacteristics.chartType'), sortable: true },
  { key: 'sampleSize', label: t('spcCharacteristics.sampleSize'), sortable: false, align: 'right' as const },
  { key: 'isActive', label: t('common.status'), sortable: true, width: '120px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const chartTypeOptions = computed(() => Object.values(SpcChartType)
  .filter((v): v is SpcChartType => typeof v === 'number')
  .map(v => ({ value: v, label: chartTypeLabel(v) })));

function chartTypeLabel(v: number) {
  const map = tm('spcCharacteristics.chartTypes') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

const products = ref<ProductResponse[]>([]);
const machines = ref<MachineResponse[]>([]);

async function loadLookups() {
  try {
    const res = await productService.browse({ pageNumber: 1, pageSize: 100 });
    products.value = res.items;
  } catch {
    // silent; product is optional
  }
  try {
    const res = await machineService.browse({ pageNumber: 1, pageSize: 100 });
    machines.value = res.items;
  } catch {
    // silent; machine is optional
  }
}

const productOptions = computed(() => products.value.map(p => ({ value: p.id, label: `${p.code} — ${p.name}` })));
const machineOptions = computed(() => machines.value.map(m => ({ value: m.id, label: `${m.code} — ${m.name}` })));

const productFilterOptions = computed(() => [
  { value: null, label: t('spcCharacteristics.filters.product') },
  ...productOptions.value
]);
const machineFilterOptions = computed(() => [
  { value: null, label: t('spcCharacteristics.filters.machine') },
  ...machineOptions.value
]);

const activeOptions = computed(() => [
  { value: null, label: t('common.status') },
  { value: 'true', label: t('common.active') },
  { value: 'false', label: t('common.inactive') }
]);

const searchFilter = ref('');
const productFilter = ref<string | null>(null);
const machineFilter = ref<string | null>(null);
const activeFilter = ref<string | null>(null);

let d1: number;
function onSearch(v: string | number | null | undefined) { clearTimeout(d1); d1 = window.setTimeout(() => table.setFilter('search', v ? String(v) : undefined), 300); }
function onProductChange(v: string | number | null) {
  table.setFilter('productId', typeof v === 'string' && v ? v : undefined);
}
function onMachineChange(v: string | number | null) {
  table.setFilter('machineId', typeof v === 'string' && v ? v : undefined);
}
function onActiveChange(v: string | number | null) {
  table.setFilter('isActive', v === null ? undefined : v === 'true');
}
function clearFilters() {
  searchFilter.value = '';
  productFilter.value = null;
  machineFilter.value = null;
  activeFilter.value = null;
  table.resetFilters();
}

const modalOpen = ref(false);
const editing = ref<SpcCharacteristicResponse | null>(null);
const saving = ref(false);
const form = reactive({
  code: '',
  name: '',
  description: '' as string | null,
  productId: null as string | null,
  machineId: null as string | null,
  chartType: SpcChartType.XbarR as number | null,
  nominalValue: null as number | null,
  lowerSpecLimit: null as number | null,
  upperSpecLimit: null as number | null,
  lowerControlLimit: null as number | null,
  upperControlLimit: null as number | null,
  sampleSize: 5 as number | null,
  unit: '' as string | null,
  isActive: true
});

function openCreate() {
  editing.value = null;
  Object.assign(form, {
    code: '', name: '', description: '', productId: null, machineId: null,
    chartType: SpcChartType.XbarR, nominalValue: null,
    lowerSpecLimit: null, upperSpecLimit: null,
    lowerControlLimit: null, upperControlLimit: null,
    sampleSize: 5, unit: '', isActive: true
  });
  modalOpen.value = true;
}
function openEdit(item: SpcCharacteristicResponse) {
  editing.value = item;
  Object.assign(form, {
    code: item.code,
    name: item.name,
    description: item.description ?? '',
    productId: item.productId ?? null,
    machineId: item.machineId ?? null,
    chartType: item.chartType,
    nominalValue: item.nominalValue ?? null,
    lowerSpecLimit: item.lowerSpecLimit ?? null,
    upperSpecLimit: item.upperSpecLimit ?? null,
    lowerControlLimit: item.lowerControlLimit ?? null,
    upperControlLimit: item.upperControlLimit ?? null,
    sampleSize: item.sampleSize,
    unit: item.unit ?? '',
    isActive: item.isActive
  });
  modalOpen.value = true;
}
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }

async function onSave() {
  saving.value = true;
  try {
    const limits = {
      name: form.name,
      description: form.description || null,
      productId: form.productId || null,
      machineId: form.machineId || null,
      chartType: Number(form.chartType) as SpcChartType,
      nominalValue: form.nominalValue,
      lowerSpecLimit: form.lowerSpecLimit,
      upperSpecLimit: form.upperSpecLimit,
      lowerControlLimit: form.lowerControlLimit,
      upperControlLimit: form.upperControlLimit,
      sampleSize: form.sampleSize ?? 5,
      unit: form.unit || null,
      isActive: form.isActive
    };
    if (editing.value) {
      await spcCharacteristicService.update(editing.value.id, { id: editing.value.id, ...limits });
      toast.success(t('toasts.updated'));
    } else {
      await spcCharacteristicService.create({ code: form.code, ...limits });
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

const confirmOpen = ref(false);
const toDelete = ref<SpcCharacteristicResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');

function onRowAction(key: string, item: SpcCharacteristicResponse) {
  if (key === 'measurements') void openMeasurements(item);
  else if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await spcCharacteristicService.remove(toDelete.value.id);
    toast.success(t('toasts.deleted'));
    await table.fetch();
    confirmOpen.value = false; toDelete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally { deleting.value = false; }
}
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }

const measurementsOpen = ref(false);
const measurementsItem = ref<SpcCharacteristicResponse | null>(null);
const measurementsLoading = ref(false);
const measurementsLoaded = ref(false);
const measurementsNotFound = ref(false);
const measurementsFrom = ref('');
const measurementsTo = ref('');
const logItems = ref<SpcMeasurementResponse[]>([]);
const chart = ref<SpcMeasurementChartResponse | null>(null);

const measurementsTitle = computed(() => measurementsItem.value
  ? t('spcCharacteristics.measurementsTitle', { name: measurementsItem.value.name })
  : t('spcMeasurements.title'));

const measurementColumns = computed(() => [
  { key: 'measuredAt', label: t('spcMeasurements.measuredAt'), sortable: false },
  { key: 'value', label: t('spcMeasurements.value'), sortable: false, align: 'right' as const },
  { key: 'status', label: t('common.status'), sortable: false, width: '150px' },
  { key: 'notes', label: t('spcMeasurements.notes'), sortable: false }
]);

const flagsById = computed(() => new Map((chart.value?.points ?? []).map(p => [p.id, p])));

const measurementsEmpty = computed(() =>
  (chart.value?.totalCount ?? logItems.value.length) === 0);

const measurementsSummary = computed(() => chart.value
  ? t('spcMeasurements.summary', {
      total: chart.value.totalCount,
      ooc: chart.value.outOfControlCount,
      oos: chart.value.outOfSpecCount
    })
  : '');

function measurementStatus(item: SpcMeasurementResponse): 'ooc' | 'oos' | 'ok' {
  const flags = flagsById.value.get(item.id);
  if (flags?.isOutOfControl) return 'ooc';
  if (flags?.isOutOfSpec) return 'oos';
  return 'ok';
}

function sortMeasurementsAsc(items: SpcMeasurementResponse[]): SpcMeasurementResponse[] {
  // The log must read in ascending time order (acceptance criterion) even if
  // a browse payload arrives unsorted; the chart already sorts its own copy.
  return [...items].sort((a, b) => {
    const ta = Date.parse(a.measuredAt);
    const tb = Date.parse(b.measuredAt);
    if (Number.isNaN(ta) || Number.isNaN(tb)) return 0;
    return ta - tb;
  });
}

function formatDateTime(value: string): string {
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? value : d.toLocaleString();
}

function isNotFoundError(err: unknown): boolean {
  if (typeof err !== 'object' || err === null) return false;
  return (err as { response?: { status?: unknown } }).response?.status === 404;
}

function measurementsRange(): { from?: string; to?: string } {
  const range: { from?: string; to?: string } = {};
  if (measurementsFrom.value) {
    const d = new Date(measurementsFrom.value);
    if (!Number.isNaN(d.getTime())) range.from = d.toISOString();
  }
  if (measurementsTo.value) {
    const d = new Date(measurementsTo.value);
    if (!Number.isNaN(d.getTime())) range.to = d.toISOString();
  }
  return range;
}

async function openMeasurements(item: SpcCharacteristicResponse): Promise<void> {
  measurementsItem.value = item;
  measurementsFrom.value = '';
  measurementsTo.value = '';
  measurementsOpen.value = true;
  await fetchMeasurements();
}

function closeMeasurements(): void {
  // Closing must never trap the dialog open: invalidate any in-flight fetch
  // so its late response is ignored, then reset the dialog state at once.
  measurementsRequest++;
  window.clearTimeout(measurementsRangeTimer);
  measurementsOpen.value = false;
  measurementsItem.value = null;
  logItems.value = [];
  chart.value = null;
  measurementsLoaded.value = false;
  measurementsNotFound.value = false;
  measurementsLoading.value = false;
}

function clearMeasurementsRange(): void {
  measurementsFrom.value = '';
  measurementsTo.value = '';
  void fetchMeasurements();
}

let measurementsRangeTimer: number | undefined;

function onMeasurementsRangeChange(): void {
  // Datetime inputs fire change on every edit; debounce so typing a range
  // triggers a single reload instead of one fetch per keystroke. The Apply
  // button bypasses the debounce via applyMeasurementsRange.
  window.clearTimeout(measurementsRangeTimer);
  measurementsRangeTimer = window.setTimeout(() => {
    void fetchMeasurements();
  }, 300);
}

function applyMeasurementsRange(): void {
  window.clearTimeout(measurementsRangeTimer);
  void fetchMeasurements();
}

let measurementsRequest = 0;

async function fetchMeasurements(): Promise<void> {
  if (!measurementsItem.value) return;
  const request = ++measurementsRequest;
  measurementsLoading.value = true;
  measurementsNotFound.value = false;
  try {
    const id = measurementsItem.value.id;
    const range = measurementsRange();
    const [page, chartRes] = await Promise.all([
      spcMeasurementService.browse({ characteristicId: id, pageNumber: 1, pageSize: 200, ...range }),
      spcMeasurementService.getChart({ characteristicId: id, ...range })
    ]);
    if (request !== measurementsRequest) return;
    logItems.value = sortMeasurementsAsc(page.items);
    chart.value = chartRes;
    measurementsLoaded.value = true;
  } catch (err) {
    if (request !== measurementsRequest) return;
    if (isNotFoundError(err)) {
      // Unknown or cross-tenant characteristic: the API hides foreign rows
      // with 404 — show the not-found state instead of a broken chart.
      measurementsNotFound.value = true;
      measurementsLoaded.value = false;
    } else {
      toast.error(extractErrorMessage(err, t('errors.loadFailed')));
    }
  } finally {
    if (request === measurementsRequest) measurementsLoading.value = false;
  }
}

onMounted(async () => {
  await loadLookups();
  await table.fetch();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.measurements-summary { color: var(--color-text-muted); margin: var(--space-2) 0 var(--space-3); }
.measurements-refresh { color: var(--color-text-muted); font-size: var(--font-size-sm); margin: 0 0 var(--space-2); }
.measurements-subtitle { font-size: var(--font-size-md); font-weight: var(--font-weight-semibold); margin: var(--space-4) 0 var(--space-2); }
</style>
