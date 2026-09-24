<template>
  <div>
    <AppPageHeader :title="$t('operators.title')" :subtitle="$t('operators.subtitle')" icon="pi pi-id-card">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('operators.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="identifierFilter" :placeholder="$t('operators.filters.identifier')" prefix-icon="pi pi-search" clearable @update:modelValue="onIdent" />
      <AppInput v-model="firstNameFilter" :placeholder="$t('operators.filters.firstName')" prefix-icon="pi pi-search" clearable @update:modelValue="onFirst" />
      <AppInput v-model="lastNameFilter" :placeholder="$t('operators.filters.lastName')" prefix-icon="pi pi-search" clearable @update:modelValue="onLast" />
      <AppSelect
        v-model="departmentFilter"
        allow-empty
        :empty-label="$t('operators.filters.department')"
        :options="departmentOptions"
        @change="onDept"
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
      <template #cell-ratePerHour="{ value }">{{ formatRate(value) }}</template>
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

    <AppCard :title="$t('operators.roster.title')" :subtitle="$t('operators.roster.subtitle')" class="roster-card">
      <div class="roster-controls">
        <AppFormField :label="$t('operators.roster.date')">
          <template #default="{ id }"><AppInput :id="id" v-model="rosterDate" type="date" @update:modelValue="onRosterDate" /></template>
        </AppFormField>
      </div>
      <form class="roster-form" @submit.prevent="onAssign">
        <AppFormField :label="$t('operators.roster.operator')" required>
          <template #default="{ id }"><AppSelect :id="id" v-model="assignOperatorId" :options="rosterOperatorOptions" :empty-label="$t('operators.roster.selectOperator')" allow-empty /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.roster.shift')" required>
          <template #default="{ id }"><AppSelect :id="id" v-model="assignShiftId" :options="rosterShiftOptions" :empty-label="$t('operators.roster.selectShift')" allow-empty /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.roster.notes')">
          <template #default="{ id }"><AppInput :id="id" v-model="assignNotes" :placeholder="$t('operators.roster.notes')" clearable /></template>
        </AppFormField>
        <div class="roster-form__actions">
          <AppButton type="submit" variant="primary" icon="pi pi-plus" :loading="assigning" :disabled="!assignOperatorId || !assignShiftId">{{ $t('operators.roster.assign') }}</AppButton>
        </div>
      </form>
      <AppEmptyState v-if="!rosterLoading && rosterItems.length === 0" icon="pi pi-calendar" :title="$t('operators.roster.empty')" />
      <AppTable
        v-else
        :items="rosterItems"
        :columns="rosterColumns"
        :loading="rosterLoading"
      >
        <template #cell-operator="{ item }">{{ item.operatorName || item.operatorIdentifier || item.operatorId }}</template>
        <template #cell-shift="{ item }">{{ item.shiftName || item.shiftCode || item.shiftId }}</template>
        <template #cell-actions="{ item }">
          <AppRowActions
            :actions="[{ key: 'delete', label: $t('common.delete'), icon: 'pi-trash', variant: 'danger', disabled: removingRosterId === item.id }]"
            @action="(k) => onRosterAction(k, item)"
          />
        </template>
      </AppTable>
    </AppCard>

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('operators.create')" @close="closeModal">
      <form id="op-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('operators.identifier')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.identifier" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.ratePerHour')" required>
          <template #default="{ id }"><AppNumberInput :id="id" v-model="form.ratePerHour" step="0.01" :min="0" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.firstName')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.firstName" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.lastName')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.lastName" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.department')" class="form-grid__full">
          <template #default="{ id }"><AppSelect :id="id" v-model="form.departmentId" :options="departmentOptions" allow-empty /></template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="op-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { operatorService, EMPTY_USER_ID, type OperatorResponse } from '../../services/operatorService';
import { departmentService, type DepartmentResponse } from '../../services/departmentService';
import { shiftService, type ShiftResponse } from '../../services/shiftService';
import { operatorShiftAssignmentService, type OperatorShiftAssignmentResponse } from '../../services/operatorShiftAssignmentService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

interface Filters {
  identifier?: string; firstName?: string; lastName?: string;
  ratePerHourFrom?: number; ratePerHourTo?: number; departmentId?: string;
}

const table = useCrudPage<OperatorResponse, Filters>({
  fetch: (req) => operatorService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'identifier', label: t('operators.identifier'), sortable: true },
  { key: 'firstName', label: t('operators.firstName'), sortable: true },
  { key: 'lastName', label: t('operators.lastName'), sortable: true },
  { key: 'department', label: t('operators.department') },
  { key: 'ratePerHour', label: t('operators.ratePerHour'), sortable: true, align: 'right' as const, width: '120px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

// Departments lookup
const departments = ref<DepartmentResponse[]>([]);
async function loadDepartments() {
  try {
    const res = await departmentService.browse({ pageNumber: 1, pageSize: 100 });
    departments.value = res.items;
  } catch { /* ignore */ }
}
const departmentOptions = computed(() =>
  departments.value.map(d => ({ value: d.id, label: `${d.code} — ${d.name}` }))
);

// filters
const identifierFilter = ref('');
const firstNameFilter = ref('');
const lastNameFilter = ref('');
const departmentFilter = ref<string | null>(null);
let id1: number; let id2: number; let id3: number;
function onIdent(v: string | number | null | undefined) { clearTimeout(id1); id1 = window.setTimeout(() => table.setFilter('identifier', v ? String(v) : undefined), 300); }
function onFirst(v: string | number | null | undefined) { clearTimeout(id2); id2 = window.setTimeout(() => table.setFilter('firstName', v ? String(v) : undefined), 300); }
function onLast(v: string | number | null | undefined) { clearTimeout(id3); id3 = window.setTimeout(() => table.setFilter('lastName', v ? String(v) : undefined), 300); }
function onDept(v: string | number | null) { table.setFilter('departmentId', v ? String(v) : undefined); }
function clearFilters() {
  identifierFilter.value = ''; firstNameFilter.value = ''; lastNameFilter.value = '';
  departmentFilter.value = null;
  table.resetFilters();
}

function formatRate(v: number | null | undefined) {
  if (v == null) return '—';
  return new Intl.NumberFormat(undefined, { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(v);
}

// modal
const modalOpen = ref(false);
const editing = ref<OperatorResponse | null>(null);
const saving = ref(false);
const form = reactive({
  identifier: '', firstName: '', lastName: '',
  ratePerHour: 0 as number, departmentId: null as string | null
});

function openCreate() {
  editing.value = null;
  Object.assign(form, { identifier: '', firstName: '', lastName: '', ratePerHour: 0, departmentId: null });
  modalOpen.value = true;
}
function openEdit(item: OperatorResponse) {
  editing.value = item;
  Object.assign(form, {
    identifier: item.identifier, firstName: item.firstName, lastName: item.lastName,
    ratePerHour: item.ratePerHour,
    departmentId: departments.value.find(d => d.name === item.department)?.id ?? null
  });
  modalOpen.value = true;
}
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }

async function onSave() {
  saving.value = true;
  try {
    const payload = {
      identifier: form.identifier,
      firstName: form.firstName,
      lastName: form.lastName,
      ratePerHour: Number(form.ratePerHour) || 0,
      departmentId: form.departmentId || null,
      userId: EMPTY_USER_ID
    };
    if (editing.value) {
      await operatorService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await operatorService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false; editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { saving.value = false; }
}

// delete
const confirmOpen = ref(false);
const toDelete = ref<OperatorResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.firstName} ${toDelete.value.lastName}` : '');
function onRowAction(key: string, item: OperatorResponse) {
  if (key === 'edit') openEdit(item);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete() {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await operatorService.remove(toDelete.value.id);
    toast.success(t('toasts.deleted'));
    await table.fetch();
    confirmOpen.value = false; toDelete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally { deleting.value = false; }
}
function cancelDelete() { confirmOpen.value = false; toDelete.value = null; }

// roster: operator-to-shift assignments for a selected date
function todayIso(): string {
  return new Date().toISOString().slice(0, 10);
}
const rosterDate = ref<string>(todayIso());
const rosterItems = ref<OperatorShiftAssignmentResponse[]>([]);
const rosterLoading = ref(false);
const rosterOperators = ref<OperatorResponse[]>([]);
const rosterShifts = ref<ShiftResponse[]>([]);
const assignOperatorId = ref<string | null>(null);
const assignShiftId = ref<string | null>(null);
const assignNotes = ref('');
const assigning = ref(false);
const removingRosterId = ref<string | null>(null);

const rosterColumns = computed(() => [
  { key: 'operator', label: t('operators.roster.operator') },
  { key: 'shift', label: t('operators.roster.shift') },
  { key: 'notes', label: t('operators.roster.notes') },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);
const rosterOperatorOptions = computed(() =>
  rosterOperators.value.map(o => ({ value: o.id, label: `${o.identifier} — ${o.firstName} ${o.lastName}` }))
);
const rosterShiftOptions = computed(() =>
  rosterShifts.value.map(s => ({ value: s.id, label: `${s.code} — ${s.name}` }))
);

async function loadRosterLookups() {
  try {
    const [operators, shifts] = await Promise.all([
      operatorService.browse({ pageNumber: 1, pageSize: 100 }),
      shiftService.browse({ pageNumber: 1, pageSize: 100 })
    ]);
    rosterOperators.value = operators.items;
    rosterShifts.value = shifts.items;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  }
}
async function loadRoster() {
  if (!rosterDate.value) { rosterItems.value = []; return; }
  rosterLoading.value = true;
  try {
    const res = await operatorShiftAssignmentService.browse({ date: rosterDate.value, pageNumber: 1, pageSize: 100 });
    rosterItems.value = res.items;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally { rosterLoading.value = false; }
}
function onRosterDate(v: string | number | null | undefined) {
  rosterDate.value = v ? String(v) : todayIso();
  void loadRoster();
}
async function onAssign() {
  if (!assignOperatorId.value || !assignShiftId.value || !rosterDate.value) return;
  assigning.value = true;
  try {
    await operatorShiftAssignmentService.create({
      operatorId: assignOperatorId.value,
      shiftId: assignShiftId.value,
      date: rosterDate.value,
      notes: assignNotes.value || null
    });
    toast.success(t('operators.roster.assignedToast'));
    assignNotes.value = '';
    await loadRoster();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally { assigning.value = false; }
}
function onRosterAction(key: string, item: OperatorShiftAssignmentResponse) {
  if (key === 'delete') void removeRoster(item);
}
async function removeRoster(item: OperatorShiftAssignmentResponse) {
  if (removingRosterId.value) return;
  removingRosterId.value = item.id;
  try {
    await operatorShiftAssignmentService.remove(item.id);
    toast.success(t('toasts.deleted'));
    await loadRoster();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally { removingRosterId.value = null; }
}

onMounted(async () => {
  await loadDepartments();
  await table.fetch();
  await loadRosterLookups();
  await loadRoster();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.roster-card { margin-top: var(--space-5); }
.roster-controls { display: grid; grid-template-columns: 240px; gap: var(--space-3); margin-bottom: var(--space-3); }
.roster-form { display: grid; grid-template-columns: 1fr 1fr 1fr auto; gap: var(--space-3); align-items: end; margin-bottom: var(--space-4); }
.roster-form__actions { padding-bottom: var(--space-1); }
</style>
