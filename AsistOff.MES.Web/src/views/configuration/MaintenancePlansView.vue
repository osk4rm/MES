<template>
  <div>
    <AppPageHeader :title="$t('maintenancePlans.title')" :subtitle="$t('maintenancePlans.subtitle')" icon="pi pi-calendar-clock">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="secondary" icon="pi pi-bolt" @click="onEvaluateDue" :loading="evaluating">{{ $t('maintenancePlans.evaluateDue') }}</AppButton>
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
        v-model="activeFilter"
        :options="activeFilterOptions"
        allow-empty
        @change="onActiveChange"
      />
      <AppCheckbox v-model="overdueOnly" :label="$t('maintenancePlans.filters.overdueOnly')" @update:model-value="onOverdueChange" />
    </AppFilterBar>

    <AppTable
      :items="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      @sort-change="table.setSort"
    >
      <template #cell-machineId="{ item }">
        {{ machineLabel(item) }}
      </template>
      <template #cell-triggerType="{ value }">
        {{ triggerLabel(Number(value)) }}
      </template>
      <template #cell-nextDueAt="{ value }">
        {{ value ? formatDate(String(value)) : '—' }}
      </template>
      <template #cell-state="{ item }">
        <AppBadge :variant="dueVariant(item)" dot>
          {{ dueLabel(item) }}
        </AppBadge>
      </template>
      <template #cell-actions="{ item }">
        <AppRowActions
          :actions="rowActions(item)"
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
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppSelect, { type SelectOption } from '../../components/ui/AppSelect.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppRowActions, { type RowAction } from '../../components/ui/AppRowActions.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import {
  maintenancePlanService,
  MaintenancePlanTriggerType,
  type MaintenancePlanResponse
} from '../../services/maintenancePlanService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

/** Plans due within this many days count as "due soon" (slice 3/3 highlight). */
const DUE_SOON_DAYS = 7;

interface Filters {
  machineId?: string;
  isActive?: boolean;
  dueBefore?: string;
}

const table = useCrudPage<MaintenancePlanResponse, Filters>({
  fetch: (req) => maintenancePlanService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('maintenancePlans.code'), sortable: true },
  { key: 'name', label: t('maintenancePlans.name'), sortable: true },
  { key: 'machineId', label: t('maintenancePlans.machine'), sortable: false },
  { key: 'triggerType', label: t('maintenancePlans.trigger'), sortable: false },
  { key: 'nextDueAt', label: t('maintenancePlans.nextDueAt'), sortable: true },
  { key: 'state', label: t('maintenancePlans.dueState'), sortable: false, width: '150px' },
  { key: 'actions', label: t('common.actions'), width: '110px' }
]);

const machines = ref<MachineResponse[]>([]);

const machineOptions = computed<SelectOption[]>(() =>
  machines.value.map(m => ({ value: m.id, label: `${m.code} — ${m.name}` })));
const machineFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('maintenancePlans.filters.machine') },
  ...machineOptions.value
]);
const activeFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('common.status') },
  { value: 1, label: t('common.active') },
  { value: 0, label: t('common.inactive') }
]);

function machineLabel(item: MaintenancePlanResponse): string {
  if (item.machineCode) return item.machineCode;
  return machines.value.find(m => m.id === item.machineId)?.name ?? item.machineId;
}
function triggerLabel(v: number): string {
  switch (v) {
    case MaintenancePlanTriggerType.Time: return t('maintenancePlans.triggers.time');
    case MaintenancePlanTriggerType.Meter: return t('maintenancePlans.triggers.meter');
    default: return String(v);
  }
}
function isDueSoon(item: MaintenancePlanResponse): boolean {
  return !item.isOverdue && item.dueInDays !== null && item.dueInDays !== undefined && item.dueInDays <= DUE_SOON_DAYS;
}
function dueVariant(item: MaintenancePlanResponse): 'danger' | 'warning' | 'idle' {
  if (item.isOverdue) return 'danger';
  if (isDueSoon(item)) return 'warning';
  return 'idle';
}
function dueLabel(item: MaintenancePlanResponse): string {
  if (item.isOverdue) return t('maintenancePlans.states.overdue');
  if (isDueSoon(item)) return t('maintenancePlans.states.dueSoon');
  if (item.nextDueAt) return t('maintenancePlans.states.scheduled');
  return t('maintenancePlans.states.noSchedule');
}
function formatDate(d: string): string {
  return new Date(d).toLocaleString();
}

const machineFilter = ref<string | number | null>(null);
const activeFilter = ref<string | number | null>(null);
const overdueOnly = ref(false);

function onMachineChange(v: string | number | null): void {
  table.setFilter('machineId', v === null ? undefined : String(v));
}
function onActiveChange(v: string | number | null): void {
  table.setFilter('isActive', v === null ? undefined : Number(v) === 1);
}
function onOverdueChange(): void {
  table.setFilter('dueBefore', overdueOnly.value ? new Date().toISOString() : undefined);
}
function clearFilters(): void {
  machineFilter.value = null;
  activeFilter.value = null;
  overdueOnly.value = false;
  table.resetFilters();
}

function rowActions(item: MaintenancePlanResponse): RowAction[] {
  if (!item.isActive) return [];
  return [{ key: 'raise', label: t('maintenancePlans.raiseNow'), icon: 'pi-plus' }];
}

async function onRowAction(key: string, item: MaintenancePlanResponse): Promise<void> {
  if (key === 'raise') {
    try {
      await maintenancePlanService.raiseNow(item.id);
      toast.success(t('toasts.created'));
      await table.fetch();
    } catch (err) {
      toast.error(extractErrorMessage(err, t('errors.saveFailed')));
    }
  }
}

const evaluating = ref(false);
async function onEvaluateDue(): Promise<void> {
  evaluating.value = true;
  try {
    const raised = await maintenancePlanService.evaluateDue({});
    toast.success(t('maintenancePlans.raisedCount', { n: raised.length }));
    await table.fetch();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    evaluating.value = false;
  }
}

onMounted(async () => {
  try {
    const m = await machineService.browse({ pageNumber: 1, pageSize: 100 });
    machines.value = m.items;
  } catch {
    // lookups stay empty; machine codes from the API are still rendered
  }
  await table.fetch();
});
</script>
