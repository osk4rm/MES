<template>
  <div>
    <AppPageHeader :title="$t('customerOrders.title')" :subtitle="$t('customerOrders.subtitle')" icon="pi pi-shopping-cart">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('customerOrders.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="orderNumberFilter" :placeholder="$t('customerOrders.filters.orderNumber')" prefix-icon="pi pi-search" clearable @update:modelValue="onOrderNumber" />
      <AppInput v-model="customerNameFilter" :placeholder="$t('customerOrders.filters.customer')" prefix-icon="pi pi-search" clearable @update:modelValue="onCustomerName" />
      <AppSelect v-model="statusFilter" :options="statusOptions" :placeholder="$t('customerOrders.filters.status')" allow-empty @update:modelValue="onStatus" />
    </AppFilterBar>

    <AppTable :items="table.items.value" :columns="columns" :loading="table.loading.value" :sort-key="table.sortKey.value" :sort-direction="table.sortDirection.value" @sort-change="table.setSort">
      <template #cell-orderNumber="{ item }"><router-link :to="{ name: 'customer-order-detail', params: { id: item.id } }" class="link">{{ item.orderNumber }}</router-link></template>
      <template #cell-customer="{ item }">{{ item.customerNameSnapshot }}</template>
      <template #cell-status="{ item }"><AppBadge :variant="statusTone(item.status)">{{ statusLabel(item.status) }}</AppBadge></template>
      <template #cell-lines="{ item }">{{ item.lines.length }}</template>
      <template #cell-released="{ item }">{{ releaseProgress(item) }}</template>
      <template #cell-actions="{ item }"><AppRowActions :actions="[{ key: 'open', label: $t('common.open'), icon: 'pi-external-link' }, { key: 'edit', label: $t('common.edit'), icon: 'pi-pencil' }, { key: 'delete', label: $t('common.delete'), icon: 'pi-trash', variant: 'danger' }]" @action="(k) => onRowAction(k, item)" /></template>
    </AppTable>
    <AppPagination :current-page="table.page.value" :page-size="table.pageSize.value" :total-count="table.totalCount.value" :total-pages="table.totalPages.value" @page-change="table.setPage" @page-size-change="table.setPageSize" />

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('customerOrders.create')" @close="closeModal">
      <form id="order-form" class="form-grid" @submit.prevent="save">
        <AppFormField :label="$t('customerOrders.orderNumber')" required><template #default="{ id, invalid }"><AppInput :id="id" v-model="form.orderNumber" required :invalid="invalid" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.customer')" required><template #default="{ id }"><AppAutocomplete :id="id" v-model="form.customerId" :options="customerOptions" :placeholder="$t('customerOrders.customerPlaceholder')" /></template></AppFormField>
        <AppFormField :label="$t('common.status')"><template #default="{ id }"><AppSelect :id="id" v-model="form.status" :options="statusOptions" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.orderDate')"><template #default="{ id }"><AppInput :id="id" v-model="form.orderDate" type="date" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.requestedDeliveryDate')"><template #default="{ id }"><AppInput :id="id" v-model="form.requestedDeliveryDate" type="date" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.confirmedDeliveryDate')"><template #default="{ id }"><AppInput :id="id" v-model="form.confirmedDeliveryDate" type="date" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.externalSystem')"><template #default="{ id }"><AppInput :id="id" v-model="form.externalSystem" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.externalOrderId')"><template #default="{ id }"><AppInput :id="id" v-model="form.externalOrderId" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.currency')"><template #default="{ id }"><AppInput :id="id" v-model="form.currency" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.notes')" class="form-grid__full"><template #default="{ id }"><AppInput :id="id" v-model="form.notes" /></template></AppFormField>
      </form>
      <template #footer><AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton><AppButton type="submit" form="order-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton></template>
    </AppModal>

    <AppConfirmDialog :open="confirmOpen" :title="$t('common.delete')" :message="deleteMessage" :loading="deleting" @confirm="confirmDelete" @cancel="cancelDelete" />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import AppAutocomplete, { type AutocompleteOption } from '../../components/ui/AppAutocomplete.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppSelect, { type SelectOption } from '../../components/ui/AppSelect.vue';
import AppTable from '../../components/ui/AppTable.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { CustomerOrderStatus, customerOrderService, customerService, type CustomerOrderResponse } from '../../services/customerOrderService';
import { extractErrorMessage } from '../../services/http';
import { useToastStore } from '../../stores/toastStore';

const { t } = useI18n(); const toast = useToastStore(); const router = useRouter();
const statusOptions = computed<SelectOption[]>(() => Object.values(CustomerOrderStatus).map(v => ({ value: v, label: statusLabel(v) })));
function statusLabel(status: CustomerOrderStatus) { return t(`customerOrders.status.${status}`); }
function statusTone(status: CustomerOrderStatus) { return status === CustomerOrderStatus.Completed ? 'success' : status === CustomerOrderStatus.Cancelled ? 'danger' : status === CustomerOrderStatus.Imported ? 'idle' : 'info'; }
interface Filters { orderNumber?: string; customerName?: string; status?: CustomerOrderStatus }
const table = useCrudPage<CustomerOrderResponse, Filters>({ fetch: req => customerOrderService.browse(req), initialFilters: {} });
const columns = computed(() => [
  { key: 'orderNumber', label: t('customerOrders.orderNumber'), sortable: true },
  { key: 'customer', label: t('customerOrders.customer'), sortable: true },
  { key: 'status', label: t('common.status'), sortable: true },
  { key: 'requestedDeliveryDate', label: t('customerOrders.requestedDeliveryDate'), sortable: true },
  { key: 'lines', label: t('customerOrders.lines') },
  { key: 'released', label: t('customerOrders.releaseProgress') },
  { key: 'externalSystem', label: t('customerOrders.externalSystem') },
  { key: 'actions', label: t('common.actions'), width: '120px' }
]);
function releaseProgress(item: CustomerOrderResponse) { const qty = item.lines.reduce((s, l) => s + l.orderedQuantity, 0); const rel = item.lines.reduce((s, l) => s + l.releasedQuantity, 0); return qty ? `${rel}/${qty}` : '—'; }
const orderNumberFilter = ref(''); const customerNameFilter = ref(''); const statusFilter = ref<number | null>(null); let d1: number; let d2: number;
function onOrderNumber(v: string | number | null | undefined) { clearTimeout(d1); d1 = window.setTimeout(() => table.setFilter('orderNumber', v ? String(v) : undefined), 300); }
function onCustomerName(v: string | number | null | undefined) { clearTimeout(d2); d2 = window.setTimeout(() => table.setFilter('customerName', v ? String(v) : undefined), 300); }
function onStatus(v: string | number | null) { table.setFilter('status', v ? Number(v) as CustomerOrderStatus : undefined); }
function clearFilters() { orderNumberFilter.value = ''; customerNameFilter.value = ''; statusFilter.value = null; table.resetFilters(); }
const customerOptions = ref<AutocompleteOption[]>([]);
async function loadCustomers() { const result = await customerService.browse({ pageSize: 500, isActive: true }); customerOptions.value = result.items.map(c => ({ value: c.id, label: `${c.code} — ${c.name}` })); }
const modalOpen = ref(false); const editing = ref<CustomerOrderResponse | null>(null); const saving = ref(false);
const form = reactive({ orderNumber: '', customerId: null as string | null, status: CustomerOrderStatus.Confirmed as number, orderDate: '', requestedDeliveryDate: '', confirmedDeliveryDate: '', externalSystem: '', externalOrderId: '', currency: 'PLN', notes: '' });
function setForm(item?: CustomerOrderResponse) { Object.assign(form, item ? { orderNumber: item.orderNumber, customerId: item.customer.id, status: item.status, orderDate: toDate(item.orderDate), requestedDeliveryDate: toDate(item.requestedDeliveryDate), confirmedDeliveryDate: toDate(item.confirmedDeliveryDate), externalSystem: item.externalSystem ?? '', externalOrderId: item.externalOrderId ?? '', currency: item.currency ?? 'PLN', notes: item.notes ?? '' } : { orderNumber: '', customerId: null, status: CustomerOrderStatus.Confirmed, orderDate: '', requestedDeliveryDate: '', confirmedDeliveryDate: '', externalSystem: '', externalOrderId: '', currency: 'PLN', notes: '' }); }
function toDate(v?: string | null) { return v ? v.slice(0, 10) : ''; }
function openCreate() { editing.value = null; setForm(); modalOpen.value = true; }
function openEdit(item: CustomerOrderResponse) { editing.value = item; setForm(item); modalOpen.value = true; }
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }
async function save() { if (!form.customerId) return; saving.value = true; try { const payload = { orderNumber: form.orderNumber, customerId: form.customerId, status: Number(form.status) as CustomerOrderStatus, orderDate: form.orderDate || null, requestedDeliveryDate: form.requestedDeliveryDate || null, confirmedDeliveryDate: form.confirmedDeliveryDate || null, externalSystem: form.externalSystem || null, externalOrderId: form.externalOrderId || null, currency: form.currency || null, notes: form.notes || null, lines: [] }; if (editing.value) { await customerOrderService.update(editing.value.id, { id: editing.value.id, ...payload }); toast.success(t('toasts.updated')); await table.fetch(); closeModal(); } else { const created = await customerOrderService.create(payload); toast.success(t('toasts.created')); router.push({ name: 'customer-order-detail', params: { id: created.id } }); } } catch (err) { toast.error(extractErrorMessage(err, t('errors.saveFailed'))); } finally { saving.value = false; } }
const confirmOpen = ref(false); const deleting = ref(false); const toDelete = ref<CustomerOrderResponse | null>(null); const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.orderNumber}` : '');
function onRowAction(key: string, item: CustomerOrderResponse) { if (key === 'open') router.push({ name: 'customer-order-detail', params: { id: item.id } }); else if (key === 'edit') openEdit(item); else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; } }
async function confirmDelete() { if (!toDelete.value) return; deleting.value = true; try { await customerOrderService.remove(toDelete.value.id); toast.success(t('toasts.deleted')); await table.fetch(); cancelDelete(); } catch (err) { toast.error(extractErrorMessage(err, t('errors.deleteFailed'))); } finally { deleting.value = false; } }
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }
onMounted(async () => { try { await loadCustomers(); } catch { /* optional lookup */ } await table.fetch(); });
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.link { color: var(--color-primary); font-weight: 600; }
</style>
