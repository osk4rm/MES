<template>
  <div>
    <AppPageHeader :title="$t('reasonCodes.title')" :subtitle="$t('reasonCodes.subtitle')" icon="pi pi-exclamation-circle">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('reasonCodes.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('reasonCodes.filters.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCode" />
      <AppInput v-model="nameFilter" :placeholder="$t('reasonCodes.filters.name')" prefix-icon="pi pi-search" clearable @update:modelValue="onName" />
      <AppSelect
        v-model="categoryFilter"
        :options="categoryFilterOptions"
        allow-empty
        @change="onCategoryChange"
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
      <template #cell-category="{ value }">
        {{ categoryLabel(value) }}
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('reasonCodes.create')" @close="closeModal">
      <form id="reason-code-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('reasonCodes.code')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.code" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('reasonCodes.name')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.name" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('reasonCodes.category')" required>
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.category" :options="categoryOptions" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('reasonCodes.sortIndex')">
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.sortIndex" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('reasonCodes.description')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.description" :rows="2" />
          </template>
        </AppFormField>
        <div class="form-grid__full">
          <AppCheckbox v-model="form.isActive" :label="$t('reasonCodes.isActive')" />
        </div>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="reason-code-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
  reasonCodeService,
  ReasonCodeCategory,
  type ReasonCodeResponse
} from '../../services/reasonCodeService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();

interface Filters { code?: string; name?: string; category?: ReasonCodeCategory; isActive?: boolean }

const table = useCrudPage<ReasonCodeResponse, Filters>({
  fetch: (req) => reasonCodeService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('reasonCodes.code'), sortable: true },
  { key: 'name', label: t('reasonCodes.name'), sortable: true },
  { key: 'category', label: t('reasonCodes.category'), sortable: true },
  { key: 'sortIndex', label: t('reasonCodes.sortIndex'), sortable: true, align: 'right' as const },
  { key: 'isActive', label: t('common.status'), sortable: true, width: '120px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const categoryOptions = computed(() => Object.values(ReasonCodeCategory)
  .filter((v): v is ReasonCodeCategory => typeof v === 'number')
  .map(v => ({ value: v, label: categoryLabel(v) })));

const categoryFilterOptions = computed(() => [
  { value: null, label: t('reasonCodes.filters.category') },
  ...categoryOptions.value
]);

const activeOptions = computed(() => [
  { value: null, label: t('common.status') },
  { value: 'true', label: t('common.active') },
  { value: 'false', label: t('common.inactive') }
]);

function categoryLabel(v: number) {
  const map = tm('reasonCodes.categories') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

const codeFilter = ref('');
const nameFilter = ref('');
const categoryFilter = ref<number | null>(null);
const activeFilter = ref<string | null>(null);

let d1: number; let d2: number;
function onCode(v: string | number | null | undefined) { clearTimeout(d1); d1 = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300); }
function onName(v: string | number | null | undefined) { clearTimeout(d2); d2 = window.setTimeout(() => table.setFilter('name', v ? String(v) : undefined), 300); }
function onCategoryChange(v: string | number | null) {
  table.setFilter('category', v === null ? undefined : Number(v) as ReasonCodeCategory);
}
function onActiveChange(v: string | number | null) {
  table.setFilter('isActive', v === null ? undefined : v === 'true');
}
function clearFilters() {
  codeFilter.value = '';
  nameFilter.value = '';
  categoryFilter.value = null;
  activeFilter.value = null;
  table.resetFilters();
}

const modalOpen = ref(false);
const editing = ref<ReasonCodeResponse | null>(null);
const saving = ref(false);
const form = reactive({
  code: '',
  name: '',
  description: '' as string | null,
  category: ReasonCodeCategory.Downtime as number | null,
  isActive: true,
  sortIndex: 0 as number | null
});

function openCreate() {
  editing.value = null;
  Object.assign(form, { code: '', name: '', description: '', category: ReasonCodeCategory.Downtime, isActive: true, sortIndex: 0 });
  modalOpen.value = true;
}
function openEdit(item: ReasonCodeResponse) {
  editing.value = item;
  Object.assign(form, {
    code: item.code,
    name: item.name,
    description: item.description ?? '',
    category: item.category,
    isActive: item.isActive,
    sortIndex: item.sortIndex
  });
  modalOpen.value = true;
}
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }

async function onSave() {
  saving.value = true;
  try {
    const payload = {
      code: form.code,
      name: form.name,
      description: form.description || null,
      category: Number(form.category) as ReasonCodeCategory,
      isActive: form.isActive,
      sortIndex: form.sortIndex ?? 0
    };
    if (editing.value) {
      await reasonCodeService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await reasonCodeService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

const confirmOpen = ref(false);
const toDelete = ref<ReasonCodeResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');

function onRowAction(key: string, item: ReasonCodeResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await reasonCodeService.remove(toDelete.value.id);
    toast.success(t('toasts.deleted'));
    await table.fetch();
    confirmOpen.value = false; toDelete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally { deleting.value = false; }
}
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }

onMounted(() => table.fetch());
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
