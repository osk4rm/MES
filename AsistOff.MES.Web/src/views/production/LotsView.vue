<template>
  <div>
    <AppPageHeader :title="$t('lots.title')" :subtitle="$t('lots.subtitle')" icon="pi pi-box">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('lots.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppCard class="scan-card">
      <form class="scan-row" @submit.prevent="onScan">
        <AppInput
          v-model="scanCode"
          :placeholder="$t('lots.scanPlaceholder')"
          prefix-icon="pi pi-barcode"
          clearable
        />
        <AppButton variant="secondary" icon="pi pi-search" :loading="scanning" @click="onScan">
          {{ $t('lots.scan') }}
        </AppButton>
      </form>
      <p v-if="scanError" class="scan-error">{{ scanError }}</p>
      <p v-else-if="scanned" class="scan-hit">
        {{ $t('lots.scanHit', { code: scanned.code }) }} — {{ statusLabel(scanned.status) }} · {{ scanned.quantity }}
      </p>
    </AppCard>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('lots.filters.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCode" />
      <AppInput v-model="productFilter" :placeholder="$t('lots.filters.product')" prefix-icon="pi pi-search" clearable @update:modelValue="onProduct" />
      <AppSelect
        v-model="statusFilter"
        :options="statusFilterOptions"
        allow-empty
        @change="onStatusChange"
      />
      <AppInput v-model="expiryFromFilter" type="date" @update:modelValue="onExpiryFrom" />
      <AppInput v-model="expiryToFilter" type="date" @update:modelValue="onExpiryTo" />
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
      <template #cell-status="{ value }">
        <AppBadge :variant="statusVariant(value)" dot>
          {{ statusLabel(value) }}
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('lots.create')" @close="closeModal">
      <form id="lot-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('lots.code')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.code" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.quantity')" required>
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.quantity" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.productId')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.productId" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.measureUnitId')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.measureUnitId" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.supplierLotNumber')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.supplierLotNumber" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.producedAt')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.producedAt" type="date" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.expiryDate')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.expiryDate" type="date" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="lot-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { lotService, LotStatus, type LotResponse } from '../../services/lotService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();

interface Filters {
  code?: string;
  productId?: string;
  status?: LotStatus;
  expiryFrom?: string;
  expiryTo?: string;
}

const table = useCrudPage<LotResponse, Filters>({
  fetch: (req) => lotService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('lots.code'), sortable: true },
  { key: 'productId', label: t('lots.productId'), sortable: true },
  { key: 'quantity', label: t('lots.quantity'), sortable: false, align: 'right' as const },
  { key: 'status', label: t('common.status'), sortable: true, width: '140px' },
  { key: 'expiryDate', label: t('lots.expiryDate'), sortable: true },
  { key: 'actions', label: t('common.actions'), width: '120px' }
]);

function statusLabel(v: number): string {
  const map = tm('lots.statuses') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

function statusVariant(v: number): 'success' | 'warning' | 'danger' | 'idle' | 'info' {
  switch (v) {
    case LotStatus.Available: return 'success';
    case LotStatus.OnHold: return 'warning';
    case LotStatus.Consumed: return 'info';
    case LotStatus.Scrapped: return 'danger';
    case LotStatus.Expired: return 'idle';
    default: return 'idle';
  }
}

const statusOptions = computed(() => ([LotStatus.Available, LotStatus.OnHold, LotStatus.Consumed, LotStatus.Scrapped, LotStatus.Expired] as LotStatus[])
  .map((v) => ({ value: v, label: statusLabel(v) })));

const statusFilterOptions = computed(() => [
  { value: null, label: t('lots.filters.status') },
  ...statusOptions.value
]);

function rowActions(item: LotResponse): Array<{ key: string; label: string; icon: string; variant?: 'danger' }> {
  const actions: Array<{ key: string; label: string; icon: string; variant?: 'danger' }> = [
    { key: 'edit', label: t('common.edit'), icon: 'pi-pencil' }
  ];
  if (item.status === LotStatus.Available) {
    actions.push({ key: 'hold', label: t('lots.actions.hold'), icon: 'pi-pause' });
  }
  if (item.status === LotStatus.OnHold) {
    actions.push({ key: 'release', label: t('lots.actions.release'), icon: 'pi-play' });
  }
  if (item.status === LotStatus.Available || item.status === LotStatus.OnHold) {
    actions.push({ key: 'scrap', label: t('lots.actions.scrap'), icon: 'pi-trash' });
  }
  actions.push({ key: 'delete', label: t('common.delete'), icon: 'pi-trash', variant: 'danger' });
  return actions;
}

// Scan-by-code lookup
const scanCode = ref('');
const scanning = ref(false);
const scanned = ref<LotResponse | null>(null);
const scanError = ref('');

async function onScan(): Promise<void> {
  const code = scanCode.value.trim();
  scanned.value = null;
  scanError.value = '';
  if (!code) return;
  scanning.value = true;
  try {
    scanned.value = await lotService.getByCode(code);
  } catch (err) {
    scanError.value = extractErrorMessage(err, t('errors.loadFailed'));
  } finally {
    scanning.value = false;
  }
}

// Filters
const codeFilter = ref('');
const productFilter = ref('');
const statusFilter = ref<number | null>(null);
const expiryFromFilter = ref('');
const expiryToFilter = ref('');

let d1 = 0; let d2 = 0;
function onCode(v: string | number | null | undefined): void {
  window.clearTimeout(d1);
  d1 = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300);
}
function onProduct(v: string | number | null | undefined): void {
  window.clearTimeout(d2);
  d2 = window.setTimeout(() => table.setFilter('productId', v ? String(v) : undefined), 300);
}
function onStatusChange(v: string | number | null): void {
  table.setFilter('status', v === null ? undefined : Number(v) as LotStatus);
}
function onExpiryFrom(v: string | number | null | undefined): void {
  table.setFilter('expiryFrom', v ? String(v) : undefined);
}
function onExpiryTo(v: string | number | null | undefined): void {
  table.setFilter('expiryTo', v ? String(v) : undefined);
}
function clearFilters(): void {
  codeFilter.value = '';
  productFilter.value = '';
  statusFilter.value = null;
  expiryFromFilter.value = '';
  expiryToFilter.value = '';
  table.resetFilters();
}

// Create/edit
const modalOpen = ref(false);
const editing = ref<LotResponse | null>(null);
const saving = ref(false);
const form = reactive({
  code: '',
  productId: '',
  measureUnitId: '',
  quantity: 0 as number | null,
  supplierLotNumber: '' as string | null,
  producedAt: '' as string | null,
  expiryDate: '' as string | null,
  notes: '' as string | null
});

function openCreate(): void {
  editing.value = null;
  Object.assign(form, {
    code: '', productId: '', measureUnitId: '', quantity: 0,
    supplierLotNumber: '', producedAt: '', expiryDate: '', notes: ''
  });
  modalOpen.value = true;
}
function openEdit(item: LotResponse): void {
  editing.value = item;
  Object.assign(form, {
    code: item.code,
    productId: item.productId,
    measureUnitId: item.measureUnitId,
    quantity: item.quantity,
    supplierLotNumber: item.supplierLotNumber ?? '',
    producedAt: item.producedAt ? item.producedAt.substring(0, 10) : '',
    expiryDate: item.expiryDate ? item.expiryDate.substring(0, 10) : '',
    notes: item.notes ?? ''
  });
  modalOpen.value = true;
}
function closeModal(): void {
  if (saving.value) return;
  modalOpen.value = false;
  editing.value = null;
}

function toIsoOrNull(v: string | null): string | null {
  if (!v) return null;
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

async function onSave(): Promise<void> {
  saving.value = true;
  try {
    const payload = {
      code: form.code,
      productId: form.productId,
      measureUnitId: form.measureUnitId,
      quantity: form.quantity ?? 0,
      supplierLotNumber: form.supplierLotNumber || null,
      producedAt: toIsoOrNull(form.producedAt),
      expiryDate: toIsoOrNull(form.expiryDate),
      notes: form.notes || null
    };
    if (editing.value) {
      await lotService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await lotService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false;
    editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

async function changeStatus(item: LotResponse, status: LotStatus): Promise<void> {
  try {
    await lotService.changeStatus(item.id, status);
    toast.success(t('toasts.updated'));
    await table.fetch();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}

// Delete
const confirmOpen = ref(false);
const toDelete = ref<LotResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.code}` : '');

function onRowAction(key: string, item: LotResponse): void {
  if (key === 'edit') openEdit(item);
  else if (key === 'hold') void changeStatus(item, LotStatus.OnHold);
  else if (key === 'release') void changeStatus(item, LotStatus.Available);
  else if (key === 'scrap') void changeStatus(item, LotStatus.Scrapped);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete(): Promise<void> {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await lotService.remove(toDelete.value.id);
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

onMounted(() => { void table.fetch(); });
</script>

<style scoped>
.scan-card { margin-bottom: var(--space-3); padding: var(--space-3); }
.scan-row { display: flex; gap: var(--space-2); align-items: center; }
.scan-row > :first-child { flex: 1; }
.scan-error { color: var(--color-danger, #b91c1c); margin: var(--space-2) 0 0; }
.scan-hit { color: var(--color-success, #065f46); margin: var(--space-2) 0 0; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
