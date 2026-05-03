<template>
  <div>
    <AppPageHeader :title="$t('customers.title')" :subtitle="$t('customers.subtitle')" icon="pi pi-users">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('customers.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('customers.filters.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCode" />
      <AppInput v-model="nameFilter" :placeholder="$t('customers.filters.name')" prefix-icon="pi pi-search" clearable @update:modelValue="onName" />
      <AppInput v-model="taxIdFilter" :placeholder="$t('customers.filters.taxId')" prefix-icon="pi pi-search" clearable @update:modelValue="onTaxId" />
    </AppFilterBar>

    <AppTable :items="table.items.value" :columns="columns" :loading="table.loading.value" :sort-key="table.sortKey.value" :sort-direction="table.sortDirection.value" @sort-change="table.setSort">
      <template #cell-isActive="{ item }"><AppBadge :variant="item.isActive ? 'success' : 'idle'">{{ item.isActive ? $t('common.active') : $t('common.inactive') }}</AppBadge></template>
      <template #cell-actions="{ item }">
        <AppRowActions :actions="[
          { key: 'edit', label: $t('common.edit'), icon: 'pi-pencil' },
          { key: 'delete', label: $t('common.delete'), icon: 'pi-trash', variant: 'danger' }
        ]" @action="(k) => onRowAction(k, item)" />
      </template>
    </AppTable>
    <AppPagination :current-page="table.page.value" :page-size="table.pageSize.value" :total-count="table.totalCount.value" :total-pages="table.totalPages.value" @page-change="table.setPage" @page-size-change="table.setPageSize" />

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('customers.create')" @close="closeModal">
      <form id="customer-form" class="form-grid" @submit.prevent="save">
        <AppFormField :label="$t('customers.code')" required><template #default="{ id, invalid }"><AppInput :id="id" v-model="form.code" required :invalid="invalid" /></template></AppFormField>
        <AppFormField :label="$t('customers.name')" required><template #default="{ id, invalid }"><AppInput :id="id" v-model="form.name" required :invalid="invalid" /></template></AppFormField>
        <AppFormField :label="$t('customers.taxId')"><template #default="{ id }"><AppInput :id="id" v-model="form.taxId" /></template></AppFormField>
        <AppFormField :label="$t('customers.email')"><template #default="{ id }"><AppInput :id="id" v-model="form.email" type="email" /></template></AppFormField>
        <AppFormField :label="$t('customers.phone')"><template #default="{ id }"><AppInput :id="id" v-model="form.phone" /></template></AppFormField>
        <AppFormField :label="$t('customers.country')"><template #default="{ id }"><AppInput :id="id" v-model="form.country" /></template></AppFormField>
        <AppFormField :label="$t('customers.addressLine1')" class="form-grid__full"><template #default="{ id }"><AppInput :id="id" v-model="form.addressLine1" /></template></AppFormField>
        <AppFormField :label="$t('customers.addressLine2')" class="form-grid__full"><template #default="{ id }"><AppInput :id="id" v-model="form.addressLine2" /></template></AppFormField>
        <AppFormField :label="$t('customers.postalCode')"><template #default="{ id }"><AppInput :id="id" v-model="form.postalCode" /></template></AppFormField>
        <AppFormField :label="$t('customers.city')"><template #default="{ id }"><AppInput :id="id" v-model="form.city" /></template></AppFormField>
        <AppFormField :label="$t('common.active')" class="form-grid__full"><template #default><AppCheckbox v-model="form.isActive" :label="$t('common.active')" /></template></AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="customer-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppConfirmDialog :open="confirmOpen" :title="$t('common.delete')" :message="deleteMessage" :loading="deleting" @confirm="confirmDelete" @cancel="cancelDelete" />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppTable from '../../components/ui/AppTable.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { customerService, type CustomerResponse } from '../../services/customerOrderService';
import { extractErrorMessage } from '../../services/http';
import { useToastStore } from '../../stores/toastStore';

const { t } = useI18n();
const toast = useToastStore();
interface Filters { code?: string; name?: string; taxId?: string }
const table = useCrudPage<CustomerResponse, Filters>({ fetch: req => customerService.browse(req), initialFilters: {} });
const columns = computed(() => [
  { key: 'code', label: t('customers.code'), sortable: true },
  { key: 'name', label: t('customers.name'), sortable: true },
  { key: 'taxId', label: t('customers.taxId') },
  { key: 'email', label: t('customers.email') },
  { key: 'isActive', label: t('common.status') },
  { key: 'actions', label: t('common.actions'), width: '110px' }
]);
const codeFilter = ref(''); const nameFilter = ref(''); const taxIdFilter = ref('');
let d1: number; let d2: number; let d3: number;
function onCode(v: string | number | null | undefined) { clearTimeout(d1); d1 = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300); }
function onName(v: string | number | null | undefined) { clearTimeout(d2); d2 = window.setTimeout(() => table.setFilter('name', v ? String(v) : undefined), 300); }
function onTaxId(v: string | number | null | undefined) { clearTimeout(d3); d3 = window.setTimeout(() => table.setFilter('taxId', v ? String(v) : undefined), 300); }
function clearFilters() { codeFilter.value = ''; nameFilter.value = ''; taxIdFilter.value = ''; table.resetFilters(); }

const modalOpen = ref(false); const editing = ref<CustomerResponse | null>(null); const saving = ref(false);
const form = reactive({ code: '', name: '', taxId: '', email: '', phone: '', addressLine1: '', addressLine2: '', postalCode: '', city: '', country: '', isActive: true });
function openCreate() { editing.value = null; Object.assign(form, { code: '', name: '', taxId: '', email: '', phone: '', addressLine1: '', addressLine2: '', postalCode: '', city: '', country: '', isActive: true }); modalOpen.value = true; }
function openEdit(item: CustomerResponse) { editing.value = item; Object.assign(form, { code: item.code, name: item.name, taxId: item.taxId ?? '', email: item.email ?? '', phone: item.phone ?? '', addressLine1: item.addressLine1 ?? '', addressLine2: item.addressLine2 ?? '', postalCode: item.postalCode ?? '', city: item.city ?? '', country: item.country ?? '', isActive: item.isActive }); modalOpen.value = true; }
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }
async function save() {
  saving.value = true;
  try {
    const payload = { ...form, taxId: form.taxId || null, email: form.email || null, phone: form.phone || null, addressLine1: form.addressLine1 || null, addressLine2: form.addressLine2 || null, postalCode: form.postalCode || null, city: form.city || null, country: form.country || null };
    if (editing.value) { await customerService.update(editing.value.id, payload); toast.success(t('toasts.updated')); } else { await customerService.create(payload); toast.success(t('toasts.created')); }
    await table.fetch(); closeModal();
  } catch (err) { toast.error(extractErrorMessage(err, t('errors.saveFailed'))); } finally { saving.value = false; }
}
const confirmOpen = ref(false); const deleting = ref(false); const toDelete = ref<CustomerResponse | null>(null);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.name}` : '');
function onRowAction(key: string, item: CustomerResponse) { if (key === 'edit') openEdit(item); else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; } }
async function confirmDelete() { if (!toDelete.value) return; deleting.value = true; try { await customerService.remove(toDelete.value.id); toast.success(t('toasts.deleted')); await table.fetch(); cancelDelete(); } catch (err) { toast.error(extractErrorMessage(err, t('errors.deleteFailed'))); } finally { deleting.value = false; } }
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }
onMounted(() => table.fetch());
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
