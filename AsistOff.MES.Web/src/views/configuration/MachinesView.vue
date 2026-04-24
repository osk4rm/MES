<template>
  <div>
    <AppPageHeader :title="$t('machines.title')" :subtitle="$t('machines.subtitle')" icon="pi pi-cog">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('machines.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('machines.filters.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCode" />
      <AppInput v-model="nameFilter" :placeholder="$t('machines.filters.name')" prefix-icon="pi pi-search" clearable @update:modelValue="onName" />
    </AppFilterBar>

    <AppTable
      :items="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      @sort-change="table.setSort"
    >
      <template #cell-isActive="{ item }">
        <span :class="['pill', item.isActive ? 'pill--ok' : 'pill--muted']">
          {{ item.isActive ? $t('common.active') : $t('common.inactive') }}
        </span>
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('machines.create')" @close="closeModal">
      <form id="machine-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('machines.code')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.code" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('machines.name')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.name" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('machines.description')" class="form-grid__full">
          <template #default="{ id }"><AppInput :id="id" v-model="form.description" /></template>
        </AppFormField>
        <AppFormField :label="$t('common.active')" class="form-grid__full">
          <template #default><input type="checkbox" v-model="form.isActive" /></template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="machine-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

interface Filters { code?: string; name?: string }

const table = useCrudPage<MachineResponse, Filters>({
  fetch: (req) => machineService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('machines.code'), sortable: true },
  { key: 'name', label: t('machines.name'), sortable: true },
  { key: 'isActive', label: t('common.status') },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const codeFilter = ref('');
const nameFilter = ref('');
let d1: number; let d2: number;
function onCode(v: string | number | null | undefined) { clearTimeout(d1); d1 = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300); }
function onName(v: string | number | null | undefined) { clearTimeout(d2); d2 = window.setTimeout(() => table.setFilter('name', v ? String(v) : undefined), 300); }
function clearFilters() { codeFilter.value = ''; nameFilter.value = ''; table.resetFilters(); }

const modalOpen = ref(false);
const editing = ref<MachineResponse | null>(null);
const saving = ref(false);
const form = reactive({ code: '', name: '', description: '' as string | null, isActive: true });

function openCreate() {
  editing.value = null;
  Object.assign(form, { code: '', name: '', description: '', isActive: true });
  modalOpen.value = true;
}
function openEdit(item: MachineResponse) {
  editing.value = item;
  Object.assign(form, { code: item.code, name: item.name, description: item.description ?? '', isActive: item.isActive });
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
      isActive: form.isActive
    };
    if (editing.value) {
      await machineService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await machineService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

const confirmOpen = ref(false);
const toDelete = ref<MachineResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');
function onRowAction(key: string, item: MachineResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await machineService.remove(toDelete.value.id);
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
.pill { display: inline-block; padding: 2px 10px; border-radius: 12px; font-size: 12px; }
.pill--ok { background: var(--color-success-soft, #d1fae5); color: var(--color-success, #065f46); }
.pill--muted { background: var(--color-neutral-soft, #e5e7eb); color: var(--color-neutral-strong, #374151); }
</style>
