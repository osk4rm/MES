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
            { key: 'calendar', label: $t('machines.calendar'), icon: 'pi-calendar' },
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

    <AppModal :open="calendarOpen" :title="calendarTitle" size="lg" @close="closeCalendar">
      <p v-if="calendarMachine" class="calendar-subtitle">{{ $t('calendar.subtitle') }} — {{ calendarMachine.code }} ({{ calendarMachine.name }})</p>
      <div v-if="calendarLoading" class="calendar-loading">{{ $t('common.loading') }}</div>
      <div v-else>
        <div v-if="calendarEntries.length === 0" class="calendar-empty">{{ $t('calendar.empty') }}</div>
        <div v-for="(entry, idx) in calendarEntries" :key="idx" class="calendar-row">
          <AppSelect
            :model-value="entry.dayOfWeek"
            :options="dayOptions"
            @update:model-value="(v) => { entry.dayOfWeek = Number(v); }"
          />
          <AppInput v-model="entry.startTime" type="time" required />
          <AppInput v-model="entry.endTime" type="time" required />
          <AppSelect
            :model-value="entry.shiftId"
            :options="shiftOptions"
            allow-empty
            :empty-label="t('calendar.noShift')"
            @update:model-value="(v) => { entry.shiftId = v === null ? null : String(v); }"
          />
          <AppCheckbox v-model="entry.isWorking" :label="t('calendar.working')" />
          <AppButton variant="ghost" icon="pi pi-trash" @click="removeEntry(idx)">{{ $t('calendar.removeEntry') }}</AppButton>
        </div>
        <AppButton variant="secondary" icon="pi pi-plus" @click="addEntry">{{ $t('calendar.addEntry') }}</AppButton>
      </div>
      <template #footer>
        <AppButton variant="ghost" :disabled="calendarSaving" @click="closeCalendar">{{ $t('common.close') }}</AppButton>
        <AppButton variant="primary" :loading="calendarSaving" :disabled="calendarLoading" @click="saveCalendar">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>
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
import AppSelect from '../../components/ui/AppSelect.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { machineService, type MachineResponse, type SaveWorkCenterCalendarEntryRequest } from '../../services/machineService';
import { shiftService, type ShiftResponse } from '../../services/shiftService';
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
  { key: 'actions', label: t('common.actions'), width: '130px' }
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
  else if (key === 'calendar') void openCalendar(item);
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

interface CalendarEntryForm {
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  shiftId: string | null;
  isWorking: boolean;
}

const calendarOpen = ref(false);
const calendarLoading = ref(false);
const calendarSaving = ref(false);
const calendarMachine = ref<MachineResponse | null>(null);
const calendarEntries = ref<CalendarEntryForm[]>([]);
const availableShifts = ref<ShiftResponse[]>([]);

const calendarTitle = computed(() =>
  calendarMachine.value ? `${t('calendar.title')}: ${calendarMachine.value.code}` : t('calendar.title'));

const dayOptions = computed(() => [1, 2, 3, 4, 5, 6, 0].map((d) => ({
  value: d,
  label: t(`calendar.days.${d}`)
})));

const shiftOptions = computed(() => availableShifts.value.map((s) => ({
  value: s.id,
  label: `${s.code} — ${s.name} (${s.startTime}–${s.endTime})`
})));

async function ensureShiftsLoaded(): Promise<void> {
  if (availableShifts.value.length > 0) return;
  try {
    const page = await shiftService.browse({ pageNumber: 1, pageSize: 100 });
    availableShifts.value = page.items;
  } catch {
    availableShifts.value = [];
  }
}

async function openCalendar(item: MachineResponse): Promise<void> {
  calendarMachine.value = item;
  calendarEntries.value = [];
  calendarOpen.value = true;
  calendarLoading.value = true;
  try {
    await ensureShiftsLoaded();
    const calendar = await machineService.getCalendar(item.id);
    calendarEntries.value = calendar.entries.map((e) => ({
      dayOfWeek: e.dayOfWeek,
      startTime: e.startTime,
      endTime: e.endTime,
      shiftId: e.shiftId ?? null,
      isWorking: e.isWorking
    }));
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
    calendarOpen.value = false;
  } finally {
    calendarLoading.value = false;
  }
}

function closeCalendar(): void {
  if (calendarSaving.value || calendarLoading.value) return;
  calendarOpen.value = false;
  calendarMachine.value = null;
  calendarEntries.value = [];
}

function addEntry(): void {
  calendarEntries.value.push({ dayOfWeek: 1, startTime: '06:00', endTime: '14:00', shiftId: null, isWorking: true });
}

function removeEntry(index: number): void {
  calendarEntries.value.splice(index, 1);
}

async function saveCalendar(): Promise<void> {
  if (!calendarMachine.value) return;
  calendarSaving.value = true;
  try {
    const entries: SaveWorkCenterCalendarEntryRequest[] = calendarEntries.value.map((e) => ({
      dayOfWeek: e.dayOfWeek as SaveWorkCenterCalendarEntryRequest['dayOfWeek'],
      startTime: e.startTime,
      endTime: e.endTime,
      shiftId: e.shiftId,
      isWorking: e.isWorking
    }));
    const saved = await machineService.saveCalendar(calendarMachine.value.id, { entries });
    calendarEntries.value = saved.entries.map((e) => ({
      dayOfWeek: e.dayOfWeek,
      startTime: e.startTime,
      endTime: e.endTime,
      shiftId: e.shiftId ?? null,
      isWorking: e.isWorking
    }));
    toast.success(t('toasts.updated'));
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    calendarSaving.value = false;
  }
}

onMounted(() => table.fetch());
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.pill { display: inline-block; padding: 2px 10px; border-radius: 12px; font-size: 12px; }
.pill--ok { background: var(--color-success-soft, #d1fae5); color: var(--color-success, #065f46); }
.pill--muted { background: var(--color-neutral-soft, #e5e7eb); color: var(--color-neutral-strong, #374151); }
.calendar-subtitle { margin: 0 0 var(--space-3); color: var(--color-text-muted); }
.calendar-loading, .calendar-empty { padding: var(--space-4); color: var(--color-text-muted); }
.calendar-row {
  display: grid;
  grid-template-columns: 150px 110px 110px 1fr auto auto;
  gap: var(--space-2);
  align-items: center;
  margin-bottom: var(--space-2);
}
</style>
