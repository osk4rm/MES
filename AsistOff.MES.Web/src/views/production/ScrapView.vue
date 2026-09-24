<template>
  <div>
    <AppPageHeader :title="$t('scrap.title')" :subtitle="$t('scrap.subtitle')" icon="pi pi-trash">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('scrap.report') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppSelect
        v-model="machineFilter"
        :options="machineFilterOptions"
        allow-empty
        @change="onMachineChange"
      />
      <AppSelect
        v-model="reasonFilter"
        :options="reasonFilterOptions"
        allow-empty
        @change="onReasonChange"
      />
      <AppInput v-model="fromFilter" type="datetime-local" @update:modelValue="onFrom" />
      <AppInput v-model="toFilter" type="datetime-local" @update:modelValue="onTo" />
    </AppFilterBar>

    <AppTable
      :items="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      @sort-change="table.setSort"
    >
      <template #cell-reportedAt="{ value }">
        {{ formatDateTime(value) }}
      </template>
      <template #cell-machineId="{ value }">
        {{ machineLabel(value) }}
      </template>
      <template #cell-reasonCodeId="{ value }">
        {{ reasonLabel(value) }}
      </template>
      <template #cell-quantity="{ value }">
        {{ formatQuantity(value) }}
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('scrap.report')" @close="closeModal">
      <form id="scrap-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('scrap.machine')" required>
          <template #default="{ id }">
            <AppSelect
              :id="id"
              v-model="form.machineId"
              :options="machineOptions"
              :disabled="isEditing"
              :placeholder="$t('scrap.selectMachine')"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('scrap.reasonCode')" required>
          <template #default="{ id }">
            <AppSelect
              :id="id"
              v-model="form.reasonCodeId"
              :options="reasonOptions"
              :placeholder="$t('scrap.selectReason')"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('scrap.quantity')" required>
          <template #default="{ id, invalid }">
            <AppNumberInput :id="id" v-model="form.quantity" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('scrap.reportedAt')" required>
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.reportedAt" type="datetime-local" :disabled="isEditing" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('scrap.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="scrap-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { scrapEventService, type ScrapEventResponse } from '../../services/scrapEventService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { reasonCodeService, ReasonCodeCategory, type ReasonCodeResponse } from '../../services/reasonCodeService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

interface Filters {
  machineId?: string;
  reasonCodeId?: string;
  reportedFrom?: string;
  reportedTo?: string;
}

const table = useCrudPage<ScrapEventResponse, Filters>({
  fetch: (req) => scrapEventService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'reportedAt', label: t('scrap.reportedAt'), sortable: true },
  { key: 'machineId', label: t('scrap.machine') },
  { key: 'reasonCodeId', label: t('scrap.reasonCode') },
  { key: 'quantity', label: t('scrap.quantity'), sortable: true, align: 'right' as const },
  { key: 'notes', label: t('scrap.notes') },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const machines = ref<MachineResponse[]>([]);
const reasonCodes = ref<ReasonCodeResponse[]>([]);

async function loadLookups() {
  try {
    const [m, r] = await Promise.all([
      machineService.browse({ pageNumber: 1, pageSize: 200 }),
      reasonCodeService.browse({ pageNumber: 1, pageSize: 200, category: ReasonCodeCategory.Scrap })
    ]);
    machines.value = m.items;
    reasonCodes.value = r.items;
  } catch { /* ignore — table still renders */ }
}

function machineLabel(id: string): string {
  const m = machines.value.find(x => x.id === id);
  return m ? `${m.code} — ${m.name}` : id;
}

function reasonLabel(id: string): string {
  const r = reasonCodes.value.find(x => x.id === id);
  return r ? `${r.code} — ${r.name}` : id;
}

function formatDateTime(v: string): string {
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString();
}

function formatQuantity(v: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(v);
}

const machineOptions = computed(() => machines.value.map(m => ({ value: m.id, label: `${m.code} — ${m.name}` })));
const reasonOptions = computed(() => reasonCodes.value.map(r => ({ value: r.id, label: `${r.code} — ${r.name}` })));
const machineFilterOptions = computed(() => [
  { value: null, label: t('scrap.filters.machine') },
  ...machineOptions.value
]);
const reasonFilterOptions = computed(() => [
  { value: null, label: t('scrap.filters.reasonCode') },
  ...reasonOptions.value
]);

const machineFilter = ref<string | null>(null);
const reasonFilter = ref<string | null>(null);
const fromFilter = ref<string | null>(null);
const toFilter = ref<string | null>(null);

function onMachineChange(v: string | number | null) {
  table.setFilter('machineId', v ? String(v) : undefined);
}
function onReasonChange(v: string | number | null) {
  table.setFilter('reasonCodeId', v ? String(v) : undefined);
}
function toIsoOrUndefined(v: string | number | null | undefined): string | undefined {
  if (v === null || v === undefined || v === '') return undefined;
  const d = new Date(String(v));
  return Number.isNaN(d.getTime()) ? undefined : d.toISOString();
}
function onFrom(v: string | number | null | undefined) {
  table.setFilter('reportedFrom', toIsoOrUndefined(v));
}
function onTo(v: string | number | null | undefined) {
  table.setFilter('reportedTo', toIsoOrUndefined(v));
}
function clearFilters() {
  machineFilter.value = null;
  reasonFilter.value = null;
  fromFilter.value = null;
  toFilter.value = null;
  table.resetFilters();
}

function toLocalInputValue(d: Date): string {
  const pad = (n: number) => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const modalOpen = ref(false);
const editing = ref<ScrapEventResponse | null>(null);
const isEditing = computed(() => editing.value !== null);
const saving = ref(false);
const form = reactive({
  machineId: null as string | null,
  reasonCodeId: null as string | null,
  quantity: null as number | null,
  reportedAt: '',
  notes: '' as string | null
});

function openCreate() {
  editing.value = null;
  Object.assign(form, {
    machineId: null,
    reasonCodeId: null,
    quantity: null,
    reportedAt: toLocalInputValue(new Date()),
    notes: ''
  });
  modalOpen.value = true;
}
function openEdit(item: ScrapEventResponse) {
  editing.value = item;
  const d = new Date(item.reportedAt);
  Object.assign(form, {
    machineId: item.machineId,
    reasonCodeId: item.reasonCodeId,
    quantity: item.quantity,
    reportedAt: Number.isNaN(d.getTime()) ? '' : toLocalInputValue(d),
    notes: item.notes ?? ''
  });
  modalOpen.value = true;
}
function closeModal() {
  if (saving.value) return;
  modalOpen.value = false;
  editing.value = null;
}

async function onSave() {
  if (!form.machineId || !form.reasonCodeId || form.quantity === null || !form.reportedAt) {
    toast.error(t('validation.required'));
    return;
  }
  saving.value = true;
  try {
    if (editing.value) {
      await scrapEventService.update(editing.value.id, {
        id: editing.value.id,
        reasonCodeId: String(form.reasonCodeId),
        quantity: Number(form.quantity),
        notes: form.notes || null
      });
      toast.success(t('toasts.updated'));
    } else {
      await scrapEventService.create({
        machineId: String(form.machineId),
        reasonCodeId: String(form.reasonCodeId),
        quantity: Number(form.quantity),
        reportedAt: new Date(form.reportedAt).toISOString(),
        notes: form.notes || null,
        reportedByOperatorId: null,
        productionOrderId: null
      });
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

const confirmOpen = ref(false);
const toDelete = ref<ScrapEventResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value
  ? `${t('common.delete')}: ${formatQuantity(toDelete.value.quantity)} — ${reasonLabel(toDelete.value.reasonCodeId)}`
  : '');

function onRowAction(key: string, item: ScrapEventResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await scrapEventService.remove(toDelete.value.id);
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
function cancelDelete() {
  confirmOpen.value = false;
  toDelete.value = null;
}

onMounted(() => {
  void loadLookups();
  void table.fetch();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
