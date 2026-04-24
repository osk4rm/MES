<template>
  <div>
    <AppPageHeader :title="$t('measureUnits.title')" :subtitle="$t('measureUnits.subtitle')" icon="pi pi-percentage">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('measureUnits.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="searchInput" :placeholder="$t('common.search')" prefix-icon="pi pi-search" clearable @update:modelValue="onSearchChange" />
      <AppSelect
        v-model="activeFilter"
        :options="[{ value: null, label: $t('common.filter') }, { value: 'true', label: $t('common.active') }, { value: 'false', label: $t('common.inactive') }]"
        @change="onActiveChange"
        allow-empty
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
      <template #cell-isActive="{ value }">
        <AppBadge :variant="value ? 'success' : 'idle'" dot>
          {{ value ? $t('common.active') : $t('common.inactive') }}
        </AppBadge>
      </template>
      <template #cell-type="{ value }">
        {{ typeLabel(value) }}
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('measureUnits.create')" @close="closeModal">
      <form id="mu-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('measureUnits.name')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.name" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('measureUnits.symbol')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.symbol" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('measureUnits.type')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.type" :options="typeOptions" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('measureUnits.baseUnit')">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.baseUnitId" :options="baseUnitOptions" allow-empty />
          </template>
        </AppFormField>
        <AppFormField :label="$t('measureUnits.conversionFactor')" :hint="$t('common.optional')">
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.conversionFactor" step="0.000001" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('measureUnits.syncId')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.syncId" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('measureUnits.description')">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.description" :rows="2" />
          </template>
        </AppFormField>
        <div class="form-grid__full">
          <AppCheckbox v-model="form.isActive" :label="$t('measureUnits.isActive')" />
        </div>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="mu-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
  measureUnitService,
  MeasureUnitType,
  type MeasureUnitResponse
} from '../../services/measureUnitService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();

interface Filters { searchTerm?: string; type?: MeasureUnitType; isActive?: boolean }

const table = useCrudPage<MeasureUnitResponse, Filters>({
  fetch: (req) => measureUnitService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'name', label: t('measureUnits.name'), sortable: true },
  { key: 'symbol', label: t('measureUnits.symbol'), sortable: true },
  { key: 'type', label: t('measureUnits.type'), sortable: true },
  { key: 'baseUnitName', label: t('measureUnits.baseUnit') },
  { key: 'conversionFactor', label: t('measureUnits.conversionFactor'), align: 'right' as const },
  { key: 'isActive', label: t('measureUnits.isActive'), sortable: true, width: '120px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const searchInput = ref('');
const activeFilter = ref<string | null>(null);

let searchDebounce: number;
function onSearchChange(v: string | number | null | undefined) {
  clearTimeout(searchDebounce);
  searchDebounce = window.setTimeout(() => {
    table.setFilter('searchTerm', v ? String(v) : undefined);
  }, 300);
}

function onActiveChange(v: string | number | null) {
  table.setFilter('isActive', v === null ? undefined : v === 'true');
}

function clearFilters() {
  searchInput.value = '';
  activeFilter.value = null;
  table.resetFilters();
}

const typeOptions = computed(() => Object.values(MeasureUnitType)
  .filter(v => typeof v === 'number')
  .map(v => ({ value: Number(v), label: typeLabel(Number(v)) })));

const baseUnitOptions = computed(() =>
  table.items.value
    .filter(u => u.id !== editing.value?.id)
    .map(u => ({ value: u.id, label: `${u.name} (${u.symbol})` }))
);

function typeLabel(v: number) {
  const map = tm('measureUnits.types') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

// Modal
const modalOpen = ref(false);
const editing = ref<MeasureUnitResponse | null>(null);
const saving = ref(false);
const form = reactive({
  name: '',
  symbol: '',
  type: MeasureUnitType.Product as number | null,
  conversionFactor: null as number | null,
  baseUnitId: null as string | null,
  isActive: true,
  description: '' as string | null,
  syncId: '' as string | null
});

function openCreate() {
  editing.value = null;
  Object.assign(form, {
    name: '', symbol: '', type: MeasureUnitType.Product,
    conversionFactor: null, baseUnitId: null, isActive: true, description: '', syncId: ''
  });
  modalOpen.value = true;
}

function openEdit(item: MeasureUnitResponse) {
  editing.value = item;
  Object.assign(form, {
    name: item.name,
    symbol: item.symbol,
    type: item.type,
    conversionFactor: item.conversionFactor ?? null,
    baseUnitId: item.baseUnitId ?? null,
    isActive: item.isActive,
    description: item.description ?? '',
    syncId: item.syncId ?? ''
  });
  modalOpen.value = true;
}

function closeModal() {
  if (saving.value) return;
  modalOpen.value = false;
  editing.value = null;
}

async function onSave() {
  saving.value = true;
  try {
    const payload = {
      name: form.name,
      symbol: form.symbol,
      type: Number(form.type) as MeasureUnitType,
      conversionFactor: form.conversionFactor,
      baseUnitId: form.baseUnitId || null,
      isActive: form.isActive,
      description: form.description || null,
      syncId: form.syncId || null
    };
    if (editing.value) {
      await measureUnitService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await measureUnitService.create(payload);
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

// Delete
const confirmOpen = ref(false);
const toDelete = ref<MeasureUnitResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');

function onRowAction(key: string, item: MeasureUnitResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}

async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await measureUnitService.remove(toDelete.value.id);
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
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }

onMounted(() => table.fetch());
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
