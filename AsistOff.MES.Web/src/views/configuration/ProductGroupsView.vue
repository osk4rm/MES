<template>
  <div>
    <AppPageHeader :title="$t('productGroups.title')" :subtitle="$t('productGroups.subtitle')" icon="pi pi-tags">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('productGroups.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('productGroups.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCodeChange" />
      <AppInput v-model="nameFilter" :placeholder="$t('productGroups.name')" prefix-icon="pi pi-search" clearable @update:modelValue="onNameChange" />
    </AppFilterBar>

    <AppTable
      :items="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      @sort-change="table.setSort"
    >
      <template #cell-parent="{ item }">{{ item.parent?.code ?? '—' }}</template>
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('productGroups.create')" @close="closeModal">
      <form id="pg-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('productGroups.code')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.code" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('productGroups.name')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.name" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('productGroups.parent')">
          <template #default="{ id }">
            <AppSelect :id="id" v-model="form.parentId" :options="parentOptions" allow-empty />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productGroups.syncId')">
          <template #default="{ id }"><AppInput :id="id" v-model="form.syncId" /></template>
        </AppFormField>
        <AppFormField :label="$t('productGroups.description')" class="form-grid__full">
          <template #default="{ id }"><AppTextarea :id="id" v-model="form.description" :rows="2" /></template>
        </AppFormField>
        <div class="form-grid__full"><AppCheckbox v-model="form.isActive" :label="$t('productGroups.isActive')" /></div>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="pg-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { productGroupService, type ProductGroupResponse } from '../../services/productGroupService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

interface Filters { name?: string; code?: string; isActive?: boolean; parentId?: string }

const table = useCrudPage<ProductGroupResponse, Filters>({
  fetch: (req) => productGroupService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('productGroups.code'), sortable: true },
  { key: 'name', label: t('productGroups.name'), sortable: true },
  { key: 'parent', label: t('productGroups.parent') },
  { key: 'description', label: t('productGroups.description') },
  { key: 'isActive', label: t('productGroups.isActive'), sortable: true, width: '120px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const codeFilter = ref('');
const nameFilter = ref('');
let codeDeb: number; let nameDeb: number;
function onCodeChange(v: string | number | null | undefined) {
  clearTimeout(codeDeb);
  codeDeb = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300);
}
function onNameChange(v: string | number | null | undefined) {
  clearTimeout(nameDeb);
  nameDeb = window.setTimeout(() => table.setFilter('name', v ? String(v) : undefined), 300);
}
function clearFilters() { codeFilter.value = ''; nameFilter.value = ''; table.resetFilters(); }

const parentOptions = computed(() =>
  table.items.value
    .filter(g => g.id !== editing.value?.id)
    .map(g => ({ value: g.id, label: `${g.code} — ${g.name}` }))
);

const modalOpen = ref(false);
const editing = ref<ProductGroupResponse | null>(null);
const saving = ref(false);
const form = reactive({
  code: '', name: '', description: '' as string | null,
  isActive: true, parentId: null as string | null, syncId: '' as string | null
});

function openCreate() {
  editing.value = null;
  Object.assign(form, { code: '', name: '', description: '', isActive: true, parentId: null, syncId: '' });
  modalOpen.value = true;
}
function openEdit(item: ProductGroupResponse) {
  editing.value = item;
  Object.assign(form, {
    code: item.code, name: item.name, description: item.description ?? '',
    isActive: item.isActive, parentId: item.parent?.id ?? null, syncId: item.syncId ?? ''
  });
  modalOpen.value = true;
}
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }

async function onSave() {
  saving.value = true;
  try {
    const payload = {
      code: form.code, name: form.name,
      description: form.description || null, isActive: form.isActive,
      parentId: form.parentId || null, syncId: form.syncId || null
    };
    if (editing.value) {
      await productGroupService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await productGroupService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

const confirmOpen = ref(false);
const toDelete = ref<ProductGroupResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');

function onRowAction(key: string, item: ProductGroupResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await productGroupService.remove(toDelete.value.id);
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
