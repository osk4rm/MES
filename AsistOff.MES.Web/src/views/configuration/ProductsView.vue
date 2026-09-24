<template>
  <div>
    <AppPageHeader :title="$t('products.title')" :subtitle="$t('products.subtitle')" icon="pi pi-box">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('products.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('products.filters.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCodeChange" />
      <AppInput v-model="nameFilter" :placeholder="$t('products.filters.name')" prefix-icon="pi pi-search" clearable @update:modelValue="onNameChange" />
      <AppSelect
        v-model="groupFilter"
        allow-empty
        :empty-label="$t('products.filters.group')"
        :options="groupFilterOptions"
        @change="onGroupChange"
      />
      <AppSelect
        v-model="activeFilter"
        allow-empty
        :empty-label="$t('products.filters.isActive')"
        :options="[{ value: 'true', label: $t('common.active') }, { value: 'false', label: $t('common.inactive') }]"
        @change="onActiveChange"
      />
    </AppFilterBar>

    <AppCard :title="$t('products.scan.title')" class="scan-card">
      <div class="scan-row">
        <AppInput
          v-model="scanValue"
          :placeholder="$t('products.scan.placeholder')"
          prefix-icon="pi pi-barcode"
          clearable
          :disabled="scanLoading"
          @enter="lookupScan"
        />
        <AppButton variant="secondary" icon="pi pi-search" :loading="scanLoading" @click="lookupScan">
          {{ $t('products.scan.search') }}
        </AppButton>
      </div>
      <div v-if="scanLoading" class="scan-state"><AppSpinner /></div>
      <div v-else-if="scanResult" class="scan-result">
        <div class="scan-result__main">
          <strong>{{ scanResult.code }}</strong>
          <span>{{ scanResult.name }}</span>
        </div>
        <div class="scan-result__meta">
          <span v-if="scanResult.ean">EAN: {{ scanResult.ean }}</span>
          <span v-if="scanResult.barcode">{{ $t('products.barcode') }}: {{ scanResult.barcode }}</span>
          <AppBadge :variant="scanResult.isActive ? 'success' : 'idle'" dot>
            {{ scanResult.isActive ? $t('common.active') : $t('common.inactive') }}
          </AppBadge>
        </div>
      </div>
      <AppEmptyState
        v-else-if="scanNotFound"
        icon="pi pi-barcode"
        :title="$t('products.scan.notFound')"
      />
    </AppCard>

    <AppTable
      :items="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      @sort-change="table.setSort"
    >
      <template #cell-group="{ item }">{{ item.group?.code ?? '—' }}</template>
      <template #cell-scanBy="{ value }"><AppBadge variant="neutral">{{ scanByLabel(value) }}</AppBadge></template>
      <template #cell-isActive="{ value }">
        <AppBadge :variant="value ? 'success' : 'idle'" dot>{{ value ? $t('common.active') : $t('common.inactive') }}</AppBadge>
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

    <AppModal :open="modalOpen" size="lg" :title="editing ? $t('common.edit') : $t('products.create')" @close="closeModal">
      <form id="p-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('products.code')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.code" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('products.name')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.name" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('products.ean')">
          <template #default="{ id }"><AppInput :id="id" v-model="form.ean" /></template>
        </AppFormField>
        <AppFormField :label="$t('products.barcode')">
          <template #default="{ id }"><AppInput :id="id" v-model="form.barcode" /></template>
        </AppFormField>
        <AppFormField :label="$t('products.group')">
          <template #default="{ id }"><AppSelect :id="id" v-model="form.productGroupId" :options="groupOptions" allow-empty /></template>
        </AppFormField>
        <AppFormField :label="$t('products.scanBy')" required>
          <template #default="{ id }"><AppSelect :id="id" v-model="form.scanBy" :options="scanByOptions" /></template>
        </AppFormField>
        <AppFormField :label="$t('products.syncId')">
          <template #default="{ id }"><AppInput :id="id" v-model="form.syncId" /></template>
        </AppFormField>
        <div class="form-grid__full"><AppCheckbox v-model="form.isActive" :label="$t('products.isActive')" /></div>
        <AppFormField :label="$t('products.description')" class="form-grid__full">
          <template #default="{ id }"><AppTextarea :id="id" v-model="form.description" :rows="3" /></template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="p-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
import AppCard from '../../components/ui/AppCard.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { productService, ScanBy, type ProductResponse, type CreateProductRequest } from '../../services/productService';
import { productGroupService, type ProductGroupResponse } from '../../services/productGroupService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();

interface Filters { code?: string; name?: string; isActive?: boolean; groupId?: string; scanBy?: ScanBy }

const table = useCrudPage<ProductResponse, Filters>({
  fetch: (req) => productService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('products.code'), sortable: true },
  { key: 'name', label: t('products.name'), sortable: true },
  { key: 'group', label: t('products.group') },
  { key: 'ean', label: t('products.ean') },
  { key: 'barcode', label: t('products.barcode') },
  { key: 'scanBy', label: t('products.scanBy'), sortable: true, width: '120px' },
  { key: 'isActive', label: t('products.isActive'), sortable: true, width: '120px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

// Group lookups
const groups = ref<ProductGroupResponse[]>([]);
async function loadGroups() {
  try {
    const res = await productGroupService.browse({ pageNumber: 1, pageSize: 100 });
    groups.value = res.items;
  } catch {
    // silent; groups optional
  }
}

const groupOptions = computed(() => groups.value.map(g => ({ value: g.id, label: `${g.code} — ${g.name}` })));
const groupFilterOptions = computed(() => groupOptions.value);

function scanByLabel(v: number) {
  const map = tm('scanBy') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

const scanByOptions = computed(() => Object.values(ScanBy)
  .filter(v => typeof v === 'number')
  .map(v => ({ value: Number(v), label: scanByLabel(Number(v)) })));

// filters
const codeFilter = ref('');
const nameFilter = ref('');
const groupFilter = ref<string | null>(null);
const activeFilter = ref<string | null>(null);
let cdeb: number; let ndeb: number;
function onCodeChange(v: string | number | null | undefined) { clearTimeout(cdeb); cdeb = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300); }
function onNameChange(v: string | number | null | undefined) { clearTimeout(ndeb); ndeb = window.setTimeout(() => table.setFilter('name', v ? String(v) : undefined), 300); }
function onGroupChange(v: string | number | null) { table.setFilter('groupId', v ? String(v) : undefined); }
function onActiveChange(v: string | number | null) { table.setFilter('isActive', v === null ? undefined : v === 'true'); }
function clearFilters() {
  codeFilter.value = ''; nameFilter.value = '';
  groupFilter.value = null; activeFilter.value = null;
  table.resetFilters();
}

// shopfloor scan lookup
const scanValue = ref('');
const scanLoading = ref(false);
const scanResult = ref<ProductResponse | null>(null);
const scanNotFound = ref(false);

async function lookupScan() {
  const value = scanValue.value.trim();
  scanResult.value = null;
  scanNotFound.value = false;
  if (!value) return;
  scanLoading.value = true;
  try {
    scanResult.value = await productService.getByScan(value);
  } catch (err) {
    const status = (err as { response?: { status?: number } }).response?.status;
    if (status === 404) {
      scanNotFound.value = true;
    } else {
      toast.error(extractErrorMessage(err, t('errors.saveFailed')));
    }
  } finally {
    scanLoading.value = false;
  }
}

// modal
const modalOpen = ref(false);
const editing = ref<ProductResponse | null>(null);
const saving = ref(false);
const form = reactive<CreateProductRequest>({
  code: '', name: '', description: '', ean: '', barcode: '',
  scanBy: ScanBy.Ean, isActive: true, productGroupId: null, syncId: ''
});

function openCreate() {
  editing.value = null;
  Object.assign(form, {
    code: '', name: '', description: '', ean: '', barcode: '',
    scanBy: ScanBy.Ean, isActive: true, productGroupId: null, syncId: ''
  });
  modalOpen.value = true;
}
function openEdit(item: ProductResponse) {
  editing.value = item;
  Object.assign(form, {
    code: item.code, name: item.name,
    description: item.description ?? '', ean: item.ean ?? '', barcode: item.barcode ?? '',
    scanBy: item.scanBy, isActive: item.isActive,
    productGroupId: item.group?.id ?? null, syncId: item.syncId ?? ''
  });
  modalOpen.value = true;
}
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }

async function onSave() {
  saving.value = true;
  try {
    const payload: CreateProductRequest = {
      code: form.code,
      name: form.name,
      description: form.description || null,
      ean: form.ean || null,
      barcode: form.barcode || null,
      scanBy: Number(form.scanBy) as ScanBy,
      isActive: form.isActive,
      productGroupId: form.productGroupId || null,
      syncId: form.syncId || null
    };
    if (editing.value) {
      await productService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await productService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

// delete
const confirmOpen = ref(false);
const toDelete = ref<ProductResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');
function onRowAction(key: string, item: ProductResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await productService.remove(toDelete.value.id);
    toast.success(t('toasts.deleted'));
    await table.fetch();
    confirmOpen.value = false; toDelete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally { deleting.value = false; }
}
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }

onMounted(async () => {
  await loadGroups();
  await table.fetch();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.scan-card { margin-bottom: var(--space-4); }
.scan-row { display: flex; gap: var(--space-2); align-items: center; }
.scan-row > :first-child { flex: 1; }
.scan-state { display: flex; justify-content: center; padding: var(--space-4); }
.scan-result { display: flex; flex-direction: column; gap: var(--space-1); padding-top: var(--space-3); }
.scan-result__main { display: flex; gap: var(--space-2); align-items: baseline; }
.scan-result__meta { display: flex; gap: var(--space-3); align-items: center; color: var(--color-text-muted); }
.scan-card :deep(.app-empty-state) { min-height: 0; padding: var(--space-4) var(--space-2) var(--space-1); }
</style>
