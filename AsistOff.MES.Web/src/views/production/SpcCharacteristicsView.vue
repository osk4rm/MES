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
import { useCrudPage } from '../../composables/useCrudPage';
import {
  spcCharacteristicService,
  SpcChartType,
  type SpcCharacteristicResponse
} from '../../services/spcCharacteristicService';
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
  if (key === 'edit') openEdit(item);
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

onMounted(async () => {
  await loadLookups();
  await table.fetch();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
