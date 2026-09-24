<template>
  <div>
    <AppPageHeader :title="$t('opcUaConnections.title')" :subtitle="$t('opcUaConnections.subtitle')" icon="pi pi-link">
      <template #actions>
        <AppBadge variant="success" dot>{{ $t('opcUaConnections.liveCount', { n: summary.liveCount }) }}</AppBadge>
        <AppBadge variant="warning" dot>{{ $t('opcUaConnections.staleCount', { n: summary.staleCount }) }}</AppBadge>
        <AppBadge variant="idle" dot>{{ $t('opcUaConnections.disabledCount', { n: summary.disabledCount }) }}</AppBadge>
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="loading" @click="refresh">
          {{ $t('common.refresh') }}
        </AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppSelect
        v-model="machineFilter"
        :options="machineFilterOptions"
        allow-empty
        @change="onMachineChange"
      />
    </AppFilterBar>

    <AppSpinner v-if="loading && connections.length === 0" />
    <AppEmptyState
      v-else-if="connections.length === 0"
      :title="$t('opcUaConnections.noConnections')"
      :description="$t('opcUaConnections.noConnectionsHint')"
    />

    <AppTable
      v-else
      :items="connections"
      :columns="columns"
      :loading="loading"
      row-key="connectionId"
    >
      <template #cell-endpointUrl="{ item }">
        <span :class="['conn-endpoint', rowState(item) === 'stale' ? 'conn-endpoint--stale' : '']">
          {{ item.endpointUrl }}
        </span>
      </template>
      <template #cell-machineId="{ value }">
        {{ machineName(String(value)) }}
      </template>
      <template #cell-isEnabled="{ value }">
        <AppBadge :variant="value ? 'success' : 'idle'" dot>
          {{ value ? $t('telemetry.enabled') : $t('telemetry.disabled') }}
        </AppBadge>
      </template>
      <template #cell-status="{ item }">
        <AppBadge :variant="statusVariant(item)" dot>
          {{ statusLabel(item) }}
        </AppBadge>
      </template>
      <template #cell-lastSeenAtUtc="{ item }">
        {{ lastSeenLabel(item) }}
      </template>
      <template #cell-lastError="{ value }">
        <span class="conn-error">{{ value ?? '—' }}</span>
      </template>
      <template #cell-tags="{ item }">
        {{ $t('opcUaConnections.tagsLine', { reporting: item.reportingTags, total: item.totalTags }) }}
      </template>
      <template #cell-actions="{ item }">
        <AppButton
          variant="secondary"
          size="sm"
          icon="pi pi-bolt"
          :loading="testingId === item.connectionId"
          @click="onTest(item)"
        >
          {{ $t('opcUaConnections.test') }}
        </AppButton>
      </template>
    </AppTable>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppSelect, { type SelectOption } from '../../components/ui/AppSelect.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import {
  opcUaConnectionService,
  type OpcUaConnectionStatusEntry
} from '../../services/opcUaConnectionService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const route = useRoute();
const router = useRouter();
const toast = useToastStore();

const machines = ref<MachineResponse[]>([]);
const connections = ref<OpcUaConnectionStatusEntry[]>([]);
const summary = ref({ liveCount: 0, staleCount: 0, disabledCount: 0 });
const loading = ref(false);
const testingId = ref<string | null>(null);

const columns = computed(() => [
  { key: 'endpointUrl', label: t('opcUaConnections.endpoint') },
  { key: 'machineId', label: t('opcUaConnections.machine') },
  { key: 'isEnabled', label: t('common.status'), width: '120px' },
  { key: 'status', label: t('opcUaConnections.health'), width: '130px' },
  { key: 'lastSeenAtUtc', label: t('opcUaConnections.lastSeen'), width: '170px' },
  { key: 'lastError', label: t('opcUaConnections.lastError') },
  { key: 'tags', label: t('opcUaConnections.tags'), width: '150px' },
  { key: 'actions', label: t('common.actions'), width: '110px' }
]);

type RowState = 'live' | 'stale' | 'disabled' | 'never';

function rowState(item: OpcUaConnectionStatusEntry): RowState {
  if (!item.isEnabled) return 'disabled';
  if (item.isLive) return 'live';
  return item.lastSeenAtUtc ? 'stale' : 'never';
}

function statusVariant(item: OpcUaConnectionStatusEntry): 'success' | 'warning' | 'idle' {
  const state = rowState(item);
  if (state === 'live') return 'success';
  if (state === 'stale') return 'warning';
  return 'idle';
}

function statusLabel(item: OpcUaConnectionStatusEntry): string {
  const state = rowState(item);
  if (state === 'live') return t('opcUaConnections.statusLive');
  if (state === 'stale') return t('opcUaConnections.statusStale');
  if (state === 'never') return t('opcUaConnections.statusNever');
  return t('opcUaConnections.statusDisabled');
}

function lastSeenLabel(item: OpcUaConnectionStatusEntry): string {
  if (!item.lastSeenAtUtc) return t('opcUaConnections.statusNever');
  const seconds = Math.max(0, Math.round((Date.now() - new Date(item.lastSeenAtUtc).getTime()) / 1000));
  if (seconds < 60) return t('telemetryDashboard.ageSeconds', { n: seconds });
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return t('telemetryDashboard.ageMinutes', { n: minutes });
  return t('telemetryDashboard.ageHours', { n: Math.floor(minutes / 60) });
}

function machineName(id: string): string {
  return machines.value.find((m) => m.id === id)?.name ?? id;
}

const machineFilter = ref<string | number | null>(null);
const machineIdFilter = ref<string | undefined>(undefined);

const machineFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('opcUaConnections.allMachines') },
  ...machines.value.map((m) => ({ value: m.id, label: `${m.code} — ${m.name}` }))
]);

function syncMachineQuery(): void {
  const query = { ...route.query };
  if (machineIdFilter.value) {
    query.machineId = machineIdFilter.value;
  } else {
    delete query.machineId;
  }
  void router.replace({ query });
}

function onMachineChange(v: string | number | null): void {
  machineIdFilter.value = v === null ? undefined : String(v);
  syncMachineQuery();
  void refresh();
}

function clearFilters(): void {
  machineFilter.value = null;
  machineIdFilter.value = undefined;
  syncMachineQuery();
  void refresh();
}

async function refresh(): Promise<void> {
  loading.value = true;
  try {
    const status = await opcUaConnectionService.getStatus(machineIdFilter.value);
    connections.value = status.connections;
    summary.value = {
      liveCount: status.liveCount,
      staleCount: status.staleCount,
      disabledCount: status.disabledCount
    };
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    loading.value = false;
  }
}

async function onTest(item: OpcUaConnectionStatusEntry): Promise<void> {
  testingId.value = item.connectionId;
  try {
    const result = await opcUaConnectionService.test(item.connectionId);
    if (result.reachable) {
      toast.success(t('opcUaConnections.testOk'));
    } else {
      toast.warning(t('opcUaConnections.testFailed'));
    }
    await refresh();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('opcUaConnections.testFailed')));
  } finally {
    testingId.value = null;
  }
}

onMounted(async () => {
  try {
    const m = await machineService.browse({ pageSize: 500 });
    machines.value = m.items;
  } catch {
    // lookups stay empty; ids are still rendered raw
  }
  const queryMachine = route.query.machineId;
  if (typeof queryMachine === 'string' && queryMachine) {
    machineIdFilter.value = queryMachine;
    machineFilter.value = queryMachine;
  }
  await refresh();
});
</script>

<style scoped>
.conn-endpoint {
  font-weight: 600;
}
.conn-endpoint--stale {
  color: var(--color-warning);
}
.conn-error {
  color: var(--color-text-muted);
  overflow-wrap: anywhere;
}
</style>
