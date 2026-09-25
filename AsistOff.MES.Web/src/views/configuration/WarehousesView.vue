<template>
  <div>
    <AppPageHeader :title="$t('warehouses.title')" :subtitle="$t('warehouses.subtitle')" icon="pi pi-building">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="refreshAll">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('warehouses.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="nameFilter" :placeholder="$t('warehouses.filters.name')" prefix-icon="pi pi-search" clearable @update:modelValue="onName" />
    </AppFilterBar>

    <AppTable
      :items="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      @sort-change="table.setSort"
    >
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

    <AppCard class="stock-card">
      <template #header>
        <div class="stock-card__head">
          <div>
            <h3>{{ $t('warehouses.stock.title') }}</h3>
            <p>{{ $t('warehouses.stock.subtitle') }}</p>
          </div>
          <AppButton variant="secondary" icon="pi pi-refresh" :loading="stockLoading" @click="fetchStock">
            {{ $t('common.refresh') }}
          </AppButton>
        </div>
      </template>
      <AppTable
        :items="stockBalances"
        :columns="stockColumns"
        :loading="stockLoading"
        :empty-label="$t('warehouses.stock.empty')"
      >
        <template #cell-warehouseId="{ value }">
          {{ warehouseLabel(value) }}
        </template>
        <template #cell-quantityOnHand="{ value }">
          {{ formatQuantity(value) }}
        </template>
      </AppTable>
    </AppCard>

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('warehouses.create')" @close="closeModal">
      <form id="wh-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('warehouses.name')" required class="form-grid__full">
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.name" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField v-if="!editing" :label="$t('warehouses.syncId')" class="form-grid__full">
          <template #default="{ id }"><AppInput :id="id" v-model="form.syncId" /></template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="wh-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { warehouseService, type WarehouseResponse } from '../../services/warehouseService';
import { stockOnHandService, type StockOnHandBalance } from '../../services/stockOnHandService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

interface Filters { name?: string }

const table = useCrudPage<WarehouseResponse, Filters>({
  fetch: (req) => warehouseService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'name', label: t('warehouses.name'), sortable: true },
  { key: 'syncId', label: t('warehouses.syncId') },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const nameFilter = ref('');
let deb: number;
function onName(v: string | number | null | undefined) {
  clearTimeout(deb);
  deb = window.setTimeout(() => table.setFilter('name', v ? String(v) : undefined), 300);
}
function clearFilters() { nameFilter.value = ''; table.resetFilters(); }

const stockBalances = ref<StockOnHandBalance[]>([]);
const stockLoading = ref(false);

const stockColumns = computed(() => [
  { key: 'productId', label: t('warehouses.stock.product') },
  { key: 'warehouseId', label: t('warehouses.stock.warehouse') },
  { key: 'quantityOnHand', label: t('warehouses.stock.quantity'), align: 'right' as const }
]);

async function fetchStock() {
  stockLoading.value = true;
  try {
    stockBalances.value = await stockOnHandService.browse();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    stockLoading.value = false;
  }
}

async function refreshAll() {
  await Promise.all([table.fetch(), fetchStock()]);
}

function warehouseLabel(id: string | null): string {
  if (!id) return t('warehouses.stock.unassigned');
  return table.items.value.find((w) => w.id === id)?.name ?? id;
}

function formatQuantity(value: number): string {
  return String(value);
}

const modalOpen = ref(false);
const editing = ref<WarehouseResponse | null>(null);
const saving = ref(false);
const form = reactive({ name: '', syncId: '' as string | null });

function openCreate() { editing.value = null; Object.assign(form, { name: '', syncId: '' }); modalOpen.value = true; }
function openEdit(item: WarehouseResponse) { editing.value = item; Object.assign(form, { name: item.name, syncId: item.syncId ?? '' }); modalOpen.value = true; }
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }

async function onSave() {
  saving.value = true;
  try {
    if (editing.value) {
      await warehouseService.update(editing.value.id, { id: editing.value.id, name: form.name });
      toast.success(t('toasts.updated'));
    } else {
      await warehouseService.create({ name: form.name, syncId: form.syncId || null });
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

const confirmOpen = ref(false);
const toDelete = ref<WarehouseResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');
function onRowAction(key: string, item: WarehouseResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await warehouseService.remove(toDelete.value.id);
    toast.success(t('toasts.deleted'));
    await table.fetch();
    confirmOpen.value = false; toDelete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally { deleting.value = false; }
}
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }

onMounted(() => { table.fetch(); fetchStock(); });
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.stock-card { margin-top: var(--space-4); }
.stock-card__head { display: flex; align-items: center; justify-content: space-between; gap: var(--space-3); }
.stock-card__head h3 { font-size: var(--font-size-lg); font-weight: var(--font-weight-semibold); }
.stock-card__head p { color: var(--color-text-muted); font-size: var(--font-size-sm); margin-top: var(--space-1); }
</style>
