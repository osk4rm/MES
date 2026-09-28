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
      :error="table.error.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      @sort-change="table.setSort"
      @retry="table.retry"
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
      <form ref="rosterFormRef" class="roster-form" novalidate @submit.prevent="onAssign">
        <AppFormField :label="$t('operators.roster.operator')" required :error="rosterErrors.fieldError('operator')">
          <template #default="{ id, invalid }"><AppSelect :id="id" v-model="assignOperatorId" :options="rosterOperatorOptions" :empty-label="$t('operators.roster.selectOperator')" allow-empty :invalid="invalid" @blur="rosterErrors.touch('operator')" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.roster.shift')" required :error="rosterErrors.fieldError('shift')">
          <template #default="{ id, invalid }"><AppSelect :id="id" v-model="assignShiftId" :options="rosterShiftOptions" :empty-label="$t('operators.roster.selectShift')" allow-empty :invalid="invalid" @blur="rosterErrors.touch('shift')" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.roster.notes')">
          <template #default="{ id }"><AppInput :id="id" v-model="assignNotes" :placeholder="$t('operators.roster.notes')" clearable /></template>
        </AppFormField>
        <div class="roster-form__actions">
          <AppButton type="submit" variant="primary" icon="pi pi-plus" :loading="assigning" :disabled="!assignOperatorId || !assignShiftId">{{ $t('operators.roster.assign') }}</AppButton>
        </div>
      </form>
      <AppDataState
        :loading="rosterLoading"
        :error="rosterError"
        :empty="rosterItems.length === 0"
        :empty-title="$t('operators.roster.empty')"
        empty-icon="pi pi-calendar"
        compact
        @retry="loadRoster"
      >
        <AppTable
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
      </AppDataState>
    </AppCard>

    <AppCard :title="$t('operators.skills.title')" :subtitle="$t('operators.skills.subtitle')" class="skills-card">
      <form ref="qualFormRef" class="skills-form" novalidate @submit.prevent="onGrant">
        <AppFormField :label="$t('operators.skills.operator')" required :error="qualErrors.fieldError('operator')">
          <template #default="{ id, invalid }"><AppSelect :id="id" v-model="qualOperatorId" :options="rosterOperatorOptions" :empty-label="$t('operators.skills.selectOperator')" allow-empty :invalid="invalid" @blur="qualErrors.touch('operator')" @change="onQualOperator" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.skills.skill')" required :error="qualErrors.fieldError('skill')">
          <template #default="{ id, invalid }"><AppSelect :id="id" v-model="qualSkillId" :options="skillOptions" :empty-label="$t('operators.skills.selectSkill')" allow-empty :invalid="invalid" @blur="qualErrors.touch('skill')" /></template>
        </AppFormField>
        <div class="skills-form__actions">
          <AppButton type="submit" variant="primary" icon="pi pi-plus" :loading="granting" :disabled="!qualOperatorId || !qualSkillId">{{ $t('operators.skills.assign') }}</AppButton>
        </div>
      </form>
      <AppDataState
        :loading="qualLoading"
        :error="qualError"
        :empty="qualItems.length === 0"
        :empty-title="$t('operators.skills.empty')"
        empty-icon="pi pi-id-card"
        compact
        @retry="loadQualifications"
      >
        <AppTable
          :items="qualItems"
          :columns="qualColumns"
          :loading="qualLoading"
        >
          <template #cell-skill="{ item }">{{ item.skillCode ? `${item.skillCode} — ${item.skillName ?? ''}`.trim() : item.skillId }}</template>
          <template #cell-actions="{ item }">
            <AppRowActions
              :actions="[{ key: 'delete', label: $t('common.delete'), icon: 'pi-trash', variant: 'danger', disabled: removingQualId === item.id }]"
              @action="(k) => onQualAction(k, item)"
            />
          </template>
        </AppTable>
      </AppDataState>
    </AppCard>

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('operators.create')" @close="closeModal">
      <form id="op-form" ref="formRef" class="form-grid" novalidate @submit.prevent="onSave">
        <AppFormField :label="$t('operators.identifier')" required :error="formErrors.fieldError('identifier')">
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.identifier" required :invalid="invalid" @blur="formErrors.touch('identifier')" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.ratePerHour')" required :error="formErrors.fieldError('ratePerHour')">
          <template #default="{ id, invalid }"><AppNumberInput :id="id" v-model="form.ratePerHour" step="0.01" :min="0" :invalid="invalid" @blur="formErrors.touch('ratePerHour')" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.firstName')" required :error="formErrors.fieldError('firstName')">
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.firstName" required :invalid="invalid" @blur="formErrors.touch('firstName')" /></template>
        </AppFormField>
        <AppFormField :label="$t('operators.lastName')" required :error="formErrors.fieldError('lastName')">
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="form.lastName" required :invalid="invalid" @blur="formErrors.touch('lastName')" /></template>
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
import AppDataState from '../../components/ui/AppDataState.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { useFormErrors } from '../../composables/useFormErrors';
import { operatorService, EMPTY_USER_ID, type OperatorResponse } from '../../services/operatorService';
import { departmentService, type DepartmentResponse } from '../../services/departmentService';
import { shiftService, type ShiftResponse } from '../../services/shiftService';
import { skillService, type SkillResponse } from '../../services/skillService';
import { operatorShiftAssignmentService, type OperatorShiftAssignmentResponse } from '../../services/operatorShiftAssignmentService';
import { operatorSkillService, type OperatorSkillQualificationResponse } from '../../services/operatorSkillService';
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
  ratePerHour: 0 as number | null, departmentId: null as string | null
});
const formRef = ref<HTMLFormElement | null>(null);
const formErrors = useFormErrors();

function collectErrors(): Record<string, string | null> {
  const rate = form.ratePerHour;
  return {
    identifier: form.identifier.trim() ? null : t('validation.required'),
    firstName: form.firstName.trim() ? null : t('validation.required'),
    lastName: form.lastName.trim() ? null : t('validation.required'),
    ratePerHour: rate === null || Number.isNaN(rate)
      ? t('validation.required')
      : rate < 0 ? t('validation.mustBeNonNegative') : null
  };
}

function openCreate() {
  editing.value = null;
  Object.assign(form, { identifier: '', firstName: '', lastName: '', ratePerHour: 0, departmentId: null });
  formErrors.reset();
  modalOpen.value = true;
}
function openEdit(item: OperatorResponse) {
  editing.value = item;
  Object.assign(form, {
    identifier: item.identifier, firstName: item.firstName, lastName: item.lastName,
    ratePerHour: item.ratePerHour,
    departmentId: departments.value.find(d => d.name === item.department)?.id ?? null
  });
  formErrors.reset();
  modalOpen.value = true;
}
function closeModal() { if (saving.value) return; modalOpen.value = false; editing.value = null; }

async function onSave() {
  if (!formErrors.submitWith(collectErrors())) {
    toast.error(t('validation.formHasErrors'));
    formErrors.focusFirstInvalidIn(formRef.value);
    return;
  }
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
    if (formErrors.applyServerErrors(err)) {
      toast.error(t('validation.formHasErrors'));
      formErrors.focusFirstInvalidIn(formRef.value);
    } else {
      toast.error(extractErrorMessage(err, t('errors.saveFailed')));
    }
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
const rosterError = ref<string | null>(null);
const rosterOperators = ref<OperatorResponse[]>([]);
const rosterShifts = ref<ShiftResponse[]>([]);
const assignOperatorId = ref<string | null>(null);
const assignShiftId = ref<string | null>(null);
const assignNotes = ref('');
const assigning = ref(false);
const removingRosterId = ref<string | null>(null);
const rosterFormRef = ref<HTMLFormElement | null>(null);
const rosterErrors = useFormErrors({ aliases: { operatorId: 'operator', shiftId: 'shift' } });

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
  rosterError.value = null;
  try {
    const res = await operatorShiftAssignmentService.browse({ date: rosterDate.value, pageNumber: 1, pageSize: 100 });
    rosterItems.value = res.items;
  } catch (err) {
    rosterError.value = extractErrorMessage(err, t('errors.loadFailed'));
  } finally { rosterLoading.value = false; }
}
function onRosterDate(v: string | number | null | undefined) {
  rosterDate.value = v ? String(v) : todayIso();
  void loadRoster();
}
async function onAssign() {
  const valid = rosterErrors.submitWith({
    operator: assignOperatorId.value ? null : t('validation.required'),
    shift: assignShiftId.value ? null : t('validation.required')
  });
  if (!valid || !rosterDate.value) {
    if (!valid) {
      toast.error(t('validation.formHasErrors'));
      rosterErrors.focusFirstInvalidIn(rosterFormRef.value);
    }
    return;
  }
  assigning.value = true;
  try {
    await operatorShiftAssignmentService.create({
      operatorId: assignOperatorId.value as string,
      shiftId: assignShiftId.value as string,
      date: rosterDate.value,
      notes: assignNotes.value || null
    });
    toast.success(t('operators.roster.assignedToast'));
    assignNotes.value = '';
    rosterErrors.reset();
    await loadRoster();
  } catch (err) {
    if (rosterErrors.applyServerErrors(err)) {
      toast.error(t('validation.formHasErrors'));
      rosterErrors.focusFirstInvalidIn(rosterFormRef.value);
    } else {
      toast.error(extractErrorMessage(err, t('errors.saveFailed')));
    }
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

// qualifications: operator skill matrix (issue #397)
const skills = ref<SkillResponse[]>([]);
const qualOperatorId = ref<string | null>(null);
const qualSkillId = ref<string | null>(null);
const qualItems = ref<OperatorSkillQualificationResponse[]>([]);
const qualLoading = ref(false);
const qualError = ref<string | null>(null);
const granting = ref(false);
const removingQualId = ref<string | null>(null);
const qualFormRef = ref<HTMLFormElement | null>(null);
const qualErrors = useFormErrors({ aliases: { operatorId: 'operator', skillId: 'skill' } });

const qualColumns = computed(() => [
  { key: 'skill', label: t('operators.skills.skill') },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);
const skillOptions = computed(() =>
  skills.value.map(s => ({ value: s.id, label: `${s.code} — ${s.name}` }))
);

async function loadSkills() {
  try {
    const res = await skillService.browse({ pageNumber: 1, pageSize: 100 });
    skills.value = res.items;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  }
}
async function loadQualifications() {
  if (!qualOperatorId.value) { qualItems.value = []; return; }
  qualLoading.value = true;
  qualError.value = null;
  try {
    const res = await operatorSkillService.browse({ operatorId: qualOperatorId.value, pageNumber: 1, pageSize: 100 });
    qualItems.value = res.items;
  } catch (err) {
    qualError.value = extractErrorMessage(err, t('errors.loadFailed'));
  } finally { qualLoading.value = false; }
}
function onQualOperator() {
  void loadQualifications();
}
async function onGrant() {
  const valid = qualErrors.submitWith({
    operator: qualOperatorId.value ? null : t('validation.required'),
    skill: qualSkillId.value ? null : t('validation.required')
  });
  if (!valid) {
    toast.error(t('validation.formHasErrors'));
    qualErrors.focusFirstInvalidIn(qualFormRef.value);
    return;
  }
  granting.value = true;
  try {
    await operatorSkillService.assign({
      operatorId: qualOperatorId.value as string,
      skillId: qualSkillId.value as string
    });
    toast.success(t('operators.skills.assignedToast'));
    qualSkillId.value = null;
    qualErrors.reset();
    await loadQualifications();
  } catch (err) {
    if (qualErrors.applyServerErrors(err)) {
      toast.error(t('validation.formHasErrors'));
      qualErrors.focusFirstInvalidIn(qualFormRef.value);
    } else {
      toast.error(extractErrorMessage(err, t('errors.saveFailed')));
    }
  } finally { granting.value = false; }
}
function onQualAction(key: string, item: OperatorSkillQualificationResponse) {
  if (key === 'delete') void removeQual(item);
}
async function removeQual(item: OperatorSkillQualificationResponse) {
  if (removingQualId.value) return;
  removingQualId.value = item.id;
  try {
    await operatorSkillService.unassign(item.id);
    toast.success(t('toasts.deleted'));
    await loadQualifications();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally { removingQualId.value = null; }
}

onMounted(async () => {
  await loadDepartments();
  await table.fetch();
  await loadRosterLookups();
  await loadSkills();
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
.skills-card { margin-top: var(--space-5); }
.skills-form { display: grid; grid-template-columns: 1fr 1fr auto; gap: var(--space-3); align-items: end; margin-bottom: var(--space-4); }
.skills-form__actions { padding-bottom: var(--space-1); }
</style>
