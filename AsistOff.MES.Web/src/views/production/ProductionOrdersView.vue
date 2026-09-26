<template>
  <div>
    <AppPageHeader :title="$t('productionOrders.title')" :subtitle="$t('productionOrders.subtitle')" icon="pi pi-list">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('productionOrders.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('productionOrders.filters.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCode" />
      <AppSelect
        v-model="statusFilter"
        :options="statusFilterOptions"
        allow-empty
        @change="onStatusChange"
      />
    </AppFilterBar>

    <div v-if="table.error.value" class="list-error" role="alert">
      <i class="pi pi-exclamation-triangle" aria-hidden="true"></i>
      <span class="list-error__message">{{ table.error.value }}</span>
      <AppButton variant="secondary" icon="pi pi-refresh" :loading="table.loading.value" @click="table.retry">
        {{ $t('common.retry') }}
      </AppButton>
    </div>

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
      <template #cell-status="{ item }">
        <AppBadge :variant="statusVariant(item.status)" dot>
          {{ statusLabel(item.status) }}
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('productionOrders.create')" @close="closeModal">
      <form id="production-order-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('productionOrders.code')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.code" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionOrders.plannedQuantity')" required>
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.plannedQuantity" :min="0" :step="1" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionOrders.product')" required class="form-grid__full">
          <template #default="{ id }">
            <AppAutocomplete :id="id" v-model="form.productId" :options="productOptions" :placeholder="$t('productionOrders.productPlaceholder')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionOrders.recipe')" required class="form-grid__full">
          <template #default="{ id }">
            <AppAutocomplete :id="id" v-model="form.recipeId" :options="recipeOptions" :placeholder="$t('productionOrders.recipePlaceholder')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionOrders.recipeVersion')" required class="form-grid__full">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.recipeVersionId" :options="versionOptions" :placeholder="$t('productionOrders.recipeVersionPlaceholder')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionOrders.measureUnit')" class="form-grid__full">
          <template #default="{ id }">
            <AppAutocomplete :id="id" v-model="form.measureUnitId" :options="measureUnitOptions" :placeholder="$t('productionOrders.measureUnitPlaceholder')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionOrders.priority')">
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.priority" :step="1" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionOrders.dueDate')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.dueDate" type="date" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionOrders.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="production-order-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppConfirmDialog
      :open="confirmOpen"
      :title="confirmTitle"
      :message="confirmMessage"
      :loading="confirmLoading"
      @confirm="confirmDialog"
      @cancel="cancelDialog"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
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
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import AppAutocomplete, { type AutocompleteOption } from '../../components/ui/AppAutocomplete.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import {
  productionOrderService,
  ProductionOrderStatus,
  type ProductionOrderResponse
} from '../../services/productionOrderService';
import { productService } from '../../services/productService';
import { recipeService, RecipeVersionStatus } from '../../services/recipeService';
import { measureUnitService } from '../../services/measureUnitService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const router = useRouter();
const toast = useToastStore();

interface Filters { code?: string; status?: ProductionOrderStatus }

const table = useCrudPage<ProductionOrderResponse, Filters>({
  fetch: (req, init) => productionOrderService.browse(req, init),
  initialFilters: {},
  errorFallback: t('errors.loadFailed')
});

const columns = computed(() => [
  { key: 'code', label: t('productionOrders.code'), sortable: true },
  { key: 'status', label: t('common.status'), sortable: true },
  { key: 'plannedQuantity', label: t('productionOrders.plannedQuantity'), sortable: false, align: 'right' as const },
  { key: 'priority', label: t('productionOrders.priority'), sortable: true, align: 'right' as const },
  { key: 'dueDate', label: t('productionOrders.dueDate'), sortable: true },
  { key: 'actions', label: t('common.actions'), width: '130px' }
]);

function statusLabel(v: number): string {
  switch (v) {
    case ProductionOrderStatus.Planned: return t('productionOrders.status.planned');
    case ProductionOrderStatus.Released: return t('productionOrders.status.released');
    case ProductionOrderStatus.InProgress: return t('productionOrders.status.inProgress');
    case ProductionOrderStatus.Completed: return t('productionOrders.status.completed');
    case ProductionOrderStatus.Closed: return t('productionOrders.status.closed');
    default: return String(v);
  }
}

function statusVariant(v: number): 'info' | 'primary' | 'success' | 'warning' | 'idle' {
  switch (v) {
    case ProductionOrderStatus.Planned: return 'info';
    case ProductionOrderStatus.Released: return 'success';
    case ProductionOrderStatus.InProgress: return 'warning';
    case ProductionOrderStatus.Completed: return 'primary';
    case ProductionOrderStatus.Closed: return 'idle';
    default: return 'info';
  }
}

const statusOptions = computed(() => [
  ProductionOrderStatus.Planned,
  ProductionOrderStatus.Released,
  ProductionOrderStatus.InProgress,
  ProductionOrderStatus.Completed,
  ProductionOrderStatus.Closed
].map(v => ({ value: v, label: statusLabel(v) })));

const statusFilterOptions = computed(() => [
  { value: null, label: t('productionOrders.filters.status') },
  ...statusOptions.value
]);

const codeFilter = ref('');
const statusFilter = ref<number | null>(null);

let d1: number;
function onCode(v: string | number | null | undefined) { clearTimeout(d1); d1 = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300); }
function onStatusChange(v: string | number | null) {
  table.setFilter('status', v === null ? undefined : Number(v) as ProductionOrderStatus);
}
function clearFilters() { codeFilter.value = ''; statusFilter.value = null; table.resetFilters(); }

function rowActions(item: ProductionOrderResponse): Array<{ key: string; label: string; icon: string; variant?: 'default' | 'danger' }> {
  const actions: Array<{ key: string; label: string; icon: string; variant?: 'default' | 'danger' }> = [
    { key: 'details', label: t('common.open'), icon: 'pi-eye' }
  ];
  if (item.status === ProductionOrderStatus.Planned) {
    actions.push({ key: 'release', label: t('productionOrders.release'), icon: 'pi-check' });
    actions.push({ key: 'edit', label: t('common.edit'), icon: 'pi-pencil' });
    actions.push({ key: 'delete', label: t('common.delete'), icon: 'pi-trash', variant: 'danger' });
  }
  return actions;
}

// lookups
const productOptions = ref<AutocompleteOption[]>([]);
const recipeOptions = ref<AutocompleteOption[]>([]);
const measureUnitOptions = ref<AutocompleteOption[]>([]);
const recipeVersions = ref<Array<{ id: string; versionNumber: number; status: number }>>([]);

async function loadLookups(): Promise<void> {
  try {
    const [products, recipes, units] = await Promise.all([
      productService.browse({ pageNumber: 1, pageSize: 100 }),
      recipeService.browse({ pageSize: 500 }),
      measureUnitService.browse({ pageSize: 500 })
    ]);
    productOptions.value = products.items.map(p => ({ value: p.id, label: `${p.code} — ${p.name}` }));
    recipeOptions.value = recipes.items.map(r => ({ value: r.id, label: `${r.code} — ${r.name}` }));
    measureUnitOptions.value = units.items.map(u => ({ value: u.id, label: `${u.symbol} — ${u.name}` }));
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  }
}

async function loadVersions(recipeId: string | null): Promise<void> {
  if (!recipeId) { recipeVersions.value = []; return; }
  try {
    const recipe = await recipeService.get(recipeId);
    recipeVersions.value = (recipe.versions ?? []).map(v => ({ id: v.id, versionNumber: v.versionNumber, status: v.status }));
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  }
}

const versionOptions = computed(() => recipeVersions.value.map(v => ({
  value: v.id,
  label: `v${v.versionNumber}${v.status === RecipeVersionStatus.Released ? ' — ' + t('recipes.versionStatus.released') : ''}`,
  disabled: v.status !== RecipeVersionStatus.Released
})));

// modal form
const modalOpen = ref(false);
const editing = ref<ProductionOrderResponse | null>(null);
const saving = ref(false);
const form = reactive({
  code: '',
  productId: null as string | null,
  recipeId: null as string | null,
  recipeVersionId: null as string | null,
  plannedQuantity: null as number | null,
  measureUnitId: null as string | null,
  priority: 0 as number | null,
  dueDate: '' as string | null,
  notes: '' as string | null
});

watch(() => form.recipeId, (v) => {
  form.recipeVersionId = null;
  void loadVersions(v);
});

function toDateInput(v: string | null | undefined): string {
  if (!v) return '';
  return v.slice(0, 10);
}

function openCreate(): void {
  editing.value = null;
  Object.assign(form, {
    code: '', productId: null, recipeId: null, recipeVersionId: null,
    plannedQuantity: null, measureUnitId: null, priority: 0, dueDate: '', notes: ''
  });
  recipeVersions.value = [];
  modalOpen.value = true;
}

async function openEdit(item: ProductionOrderResponse): Promise<void> {
  editing.value = item;
  Object.assign(form, {
    code: item.code,
    productId: item.productId,
    recipeId: item.recipeId,
    recipeVersionId: item.recipeVersionId,
    plannedQuantity: item.plannedQuantity,
    measureUnitId: item.measureUnitId ?? null,
    priority: item.priority,
    dueDate: toDateInput(item.dueDate),
    notes: item.notes ?? ''
  });
  await loadVersions(item.recipeId);
  // keep the stored version even if it is not released anymore
  modalOpen.value = true;
}

function closeModal(): void { if (saving.value) return; modalOpen.value = false; editing.value = null; }

async function onSave(): Promise<void> {
  if (!form.productId || !form.recipeId || !form.recipeVersionId || form.plannedQuantity === null) {
    toast.error(t('validation.required'));
    return;
  }
  saving.value = true;
  try {
    const payload = {
      code: form.code,
      productId: form.productId,
      recipeId: form.recipeId,
      recipeVersionId: form.recipeVersionId,
      plannedQuantity: form.plannedQuantity,
      measureUnitId: form.measureUnitId || null,
      priority: form.priority ?? 0,
      dueDate: form.dueDate ? new Date(form.dueDate).toISOString() : null,
      notes: form.notes || null
    };
    if (editing.value) {
      await productionOrderService.update(editing.value.id, { id: editing.value.id, ...payload, concurrencyToken: editing.value.concurrencyToken });
      toast.success(t('toasts.updated'));
    } else {
      await productionOrderService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

// confirm dialog (delete + release)
const confirmOpen = ref(false);
const confirmKind = ref<'delete' | 'release' | null>(null);
const confirmTarget = ref<ProductionOrderResponse | null>(null);
const confirmLoading = ref(false);
const confirmTitle = computed(() => confirmKind.value === 'release' ? t('productionOrders.release') : t('common.delete'));
const confirmMessage = computed(() => confirmTarget.value
  ? confirmKind.value === 'release'
    ? `${t('productionOrders.release')}: ${confirmTarget.value.code}`
    : `${t('common.delete')}: ${confirmTarget.value.code}`
  : '');

function onRowAction(key: string, item: ProductionOrderResponse): void {
  if (key === 'details') { void router.push({ name: 'production-order-detail', params: { id: item.id } }); }
  else if (key === 'edit') { void openEdit(item); }
  else if (key === 'delete' || key === 'release') {
    confirmKind.value = key;
    confirmTarget.value = item;
    confirmOpen.value = true;
  }
}

async function confirmDialog(): Promise<void> {
  if (!confirmTarget.value || !confirmKind.value) return;
  confirmLoading.value = true;
  try {
    if (confirmKind.value === 'delete') {
      await productionOrderService.remove(confirmTarget.value.id);
      toast.success(t('toasts.deleted'));
    } else {
      await productionOrderService.release(confirmTarget.value.id, confirmTarget.value.concurrencyToken);
      toast.success(t('productionOrders.releasedToast'));
    }
    await table.fetch();
    confirmOpen.value = false; confirmTarget.value = null; confirmKind.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { confirmLoading.value = false; }
}
function cancelDialog(): void { confirmOpen.value = false; confirmTarget.value = null; confirmKind.value = null; }

onMounted(() => { void loadLookups(); void table.fetch(); });
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.list-error {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin-bottom: var(--space-3);
  padding: var(--space-3) var(--space-4);
  border: 1px solid var(--color-danger);
  border-radius: var(--radius-md);
  color: var(--color-danger);
  background: var(--color-bg);
}
.list-error__message { flex: 1; }
</style>
