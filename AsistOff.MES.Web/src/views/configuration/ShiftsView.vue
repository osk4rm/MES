<template>
  <div>
    <AppPageHeader :title="$t('shifts.title')" :subtitle="$t('shifts.subtitle')" icon="pi pi-clock">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('shifts.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('shifts.filters.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCode" />
      <AppInput v-model="nameFilter" :placeholder="$t('shifts.filters.name')" prefix-icon="pi pi-search" clearable @update:modelValue="onName" />
      <AppSelect v-model="activeFilter" :options="activeOptions" allow-empty @change="onActiveChange" />
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
      <template #cell-startTime="{ value }">
        <span class="shift-window">{{ value }}</span>
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('shifts.create')" @close="closeModal">
      <form id="shift-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('shifts.code')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.code" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('shifts.name')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.name" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('shifts.startTime')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.startTime" type="time" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('shifts.endTime')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.endTime" type="time" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('shifts.description')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.description" :rows="2" />
          </template>
        </AppFormField>
        <div class="form-grid__full">
          <AppCheckbox v-model="form.isActive" :label="$t('shifts.isActive')" />
        </div>
        <p v-if="isOvernight" class="form-grid__full shift-overnight-hint">
          <i class="pi pi-moon" aria-hidden="true"></i>
          {{ $t('shifts.overnightHint') }}
        </p>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="shift-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { shiftService, type ShiftResponse } from '../../services/shiftService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

interface Filters { code?: string; name?: string; isActive?: boolean }

const table = useCrudPage<ShiftResponse, Filters>({
  fetch: (req) => shiftService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('shifts.code'), sortable: true },
  { key: 'name', label: t('shifts.name'), sortable: true },
  { key: 'startTime', label: t('shifts.startTime'), sortable: true },
  { key: 'endTime', label: t('shifts.endTime'), sortable: true },
  { key: 'isActive', label: t('common.status'), sortable: true, width: '120px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const activeOptions = computed(() => [
  { value: null, label: t('common.status') },
  { value: 'true', label: t('common.active') },
  { value: 'false', label: t('common.inactive') }
]);

const codeFilter = ref('');
const nameFilter = ref('');
const activeFilter = ref<string | null>(null);

let d1: number; let d2: number;
function onCode(v: string | number | null | undefined) { clearTimeout(d1); d1 = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300); }
function onName(v: string | number | null | undefined) { clearTimeout(d2); d2 = window.setTimeout(() => table.setFilter('name', v ? String(v) : undefined), 300); }
function onActiveChange(v: string | number | null) {
  table.setFilter('isActive', v === null ? undefined : v === 'true');
}
function clearFilters() {
  codeFilter.value = '';
  nameFilter.value = '';
  activeFilter.value = null;
  table.resetFilters();
}

const modalOpen = ref(false);
const editing = ref<ShiftResponse | null>(null);
const saving = ref(false);
const form = reactive({
  code: '',
  name: '',
  description: '' as string | null,
  startTime: '06:00',
  endTime: '14:00',
  isActive: true
});

/** `endTime <= startTime` means the window crosses midnight. */
const isOvernight = computed(() => !!form.startTime && !!form.endTime && form.endTime <= form.startTime);

function openCreate() {
  editing.value = null;
  Object.assign(form, { code: '', name: '', description: '', startTime: '06:00', endTime: '14:00', isActive: true });
  modalOpen.value = true;
}
function openEdit(item: ShiftResponse) {
  editing.value = item;
  Object.assign(form, {
    code: item.code,
    name: item.name,
    description: item.description ?? '',
    startTime: item.startTime,
    endTime: item.endTime,
    isActive: item.isActive
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
      startTime: form.startTime,
      endTime: form.endTime,
      isActive: form.isActive
    };
    if (editing.value) {
      await shiftService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await shiftService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

const confirmOpen = ref(false);
const toDelete = ref<ShiftResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');

function onRowAction(key: string, item: ShiftResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await shiftService.remove(toDelete.value.id);
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
.shift-window { font-variant-numeric: tabular-nums; }
.shift-overnight-hint {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  margin: 0;
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
</style>
