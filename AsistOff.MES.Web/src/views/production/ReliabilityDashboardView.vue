<template>
  <div>
    <AppPageHeader :title="$t('reliabilityDashboard.title')" :subtitle="$t('reliabilityDashboard.subtitle')" icon="pi pi-wrench">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="loading" @click="refresh">
          {{ $t('common.refresh') }}
        </AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppFormField :label="$t('reliabilityDashboard.workCenter')">
        <template #default="{ id }">
          <AppSelect
            :id="id"
            v-model="machineId"
            :options="machineOptions"
            :placeholder="$t('reliabilityDashboard.selectWorkCenter')"
            :disabled="machinesLoading"
            @change="onMachineChange"
          />
        </template>
      </AppFormField>
      <AppFormField :label="$t('reliabilityDashboard.preset')">
        <template #default="{ id }">
          <AppSelect :id="id" v-model="preset" :options="presetOptions" @change="onPresetChange" />
        </template>
      </AppFormField>
      <AppFormField :label="$t('reliabilityDashboard.from')">
        <template #default="{ id }">
          <AppInput :id="id" v-model="fromInput" type="datetime-local" @change="onWindowChange" />
        </template>
      </AppFormField>
      <AppFormField :label="$t('reliabilityDashboard.to')">
        <template #default="{ id }">
          <AppInput :id="id" v-model="toInput" type="datetime-local" @change="onWindowChange" />
        </template>
      </AppFormField>
      <template #actions>
        <AppButton variant="primary" icon="pi pi-check" :loading="loading" @click="refresh">
          {{ $t('reliabilityDashboard.apply') }}
        </AppButton>
      </template>
    </AppFilterBar>

    <AppSpinner v-if="loading && !loadedOnce" />
    <AppEmptyState
      v-else-if="!machineId"
      icon="pi pi-wrench"
      :title="$t('reliabilityDashboard.noMachine')"
      :description="$t('reliabilityDashboard.noMachineHint')"
    />
    <AppEmptyState
      v-else-if="notFound"
      icon="pi pi-exclamation-circle"
      :title="$t('reliabilityDashboard.notFound')"
      :description="$t('reliabilityDashboard.notFoundHint')"
    />
    <template v-else-if="snapshot">
      <AppEmptyState
        v-if="nullReliability"
        icon="pi pi-info-circle"
        :title="$t('reliabilityDashboard.nullMtbfTitle')"
        :description="$t('reliabilityDashboard.nullMtbfHint')"
      />

      <div class="reliability-cards">
        <AppCard v-for="card in kpiCards" :key="card.key" class="reliability-card">
          <template #header>
            <div class="reliability-card__header">
              <span class="reliability-card__title">{{ card.title }}</span>
              <AppBadge v-if="card.badge !== null" :variant="card.computed ? 'success' : 'idle'" dot>
                {{ card.computed ? $t('reliabilityDashboard.computed') : $t('reliabilityDashboard.notComputed') }}
              </AppBadge>
            </div>
          </template>
          <div class="reliability-card__value">{{ card.value }}</div>
          <div class="reliability-card__meta">{{ card.hint }}</div>
        </AppCard>
      </div>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppSelect, { type SelectOption } from '../../components/ui/AppSelect.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import {
  ReliabilityPreset,
  formatMinutes,
  formatNullableMinutes,
  hasNullReliability,
  reliabilityService,
  validateReliabilityWindow,
  type GetReliabilitySnapshotQuery,
  type ReliabilitySnapshot
} from '../../services/reliabilityService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();
const route = useRoute();
const router = useRouter();

const machines = ref<MachineResponse[]>([]);
const machinesLoading = ref(false);

const machineId = ref<string | null>(null);
const preset = ref<ReliabilityPreset>(ReliabilityPreset.Last8Hours);
const fromInput = ref('');
const toInput = ref('');

const snapshot = ref<ReliabilitySnapshot | null>(null);
const loading = ref(false);
const loadedOnce = ref(false);
const notFound = ref(false);

const nullReliability = computed(() => hasNullReliability(snapshot.value));

interface KpiCard {
  key: string;
  title: string;
  value: string;
  hint: string;
  computed: boolean;
  badge: boolean | null;
}

const kpiCards = computed<KpiCard[]>(() => {
  const s = snapshot.value;
  if (!s) return [];
  return [
    {
      key: 'failures',
      title: t('reliabilityDashboard.cards.failures'),
      value: String(s.failureCount),
      hint: t('reliabilityDashboard.downtimeHint', { minutes: formatMinutes(s.totalDowntimeMinutes) }),
      computed: true,
      badge: null
    },
    {
      key: 'repairs',
      title: t('reliabilityDashboard.cards.repairs'),
      value: String(s.repairCount),
      hint: t('reliabilityDashboard.avgRepairHint', { value: formatNullableMinutes(s.avgRepairMinutes) }),
      computed: s.avgRepairMinutes !== null,
      badge: true
    },
    {
      key: 'mtbf',
      title: t('reliabilityDashboard.cards.mtbf'),
      value: formatNullableMinutes(s.mtbfMinutes),
      hint: t('reliabilityDashboard.uptimeHint', { minutes: formatMinutes(s.uptimeMinutes) }),
      computed: s.mtbfMinutes !== null,
      badge: true
    },
    {
      key: 'mttr',
      title: t('reliabilityDashboard.cards.mttr'),
      value: formatNullableMinutes(s.mttrMinutes),
      hint: t('reliabilityDashboard.downtimeHint', { minutes: formatMinutes(s.totalDowntimeMinutes) }),
      computed: s.mttrMinutes !== null,
      badge: true
    },
    {
      key: 'avgRepair',
      title: t('reliabilityDashboard.cards.avgRepair'),
      value: formatNullableMinutes(s.avgRepairMinutes),
      hint: t('reliabilityDashboard.repairsHint', { count: s.repairCount }),
      computed: s.avgRepairMinutes !== null,
      badge: true
    },
    {
      key: 'window',
      title: t('reliabilityDashboard.cards.window'),
      value: formatMinutes(s.windowMinutes),
      hint: t('reliabilityDashboard.windowHint'),
      computed: true,
      badge: null
    },
    {
      key: 'uptime',
      title: t('reliabilityDashboard.cards.uptime'),
      value: formatMinutes(s.uptimeMinutes),
      hint: t('reliabilityDashboard.uptimeHint', { minutes: formatMinutes(s.uptimeMinutes) }),
      computed: true,
      badge: null
    },
    {
      key: 'downtime',
      title: t('reliabilityDashboard.cards.downtime'),
      value: formatMinutes(s.totalDowntimeMinutes),
      hint: t('reliabilityDashboard.failuresHint', { count: s.failureCount }),
      computed: true,
      badge: null
    }
  ];
});

const machineOptions = computed<SelectOption[]>(() =>
  machines.value.map((m) => ({ value: m.id, label: `${m.code} — ${m.name}` }))
);

const presetOptions = computed<SelectOption[]>(() => [
  { value: ReliabilityPreset.Last8Hours, label: t('reliabilityDashboard.presets.last8h') },
  { value: ReliabilityPreset.Last24Hours, label: t('reliabilityDashboard.presets.last24h') },
  { value: ReliabilityPreset.Last7Days, label: t('reliabilityDashboard.presets.last7d') },
  { value: ReliabilityPreset.Last30Days, label: t('reliabilityDashboard.presets.last30d') },
  { value: ReliabilityPreset.Custom, label: t('reliabilityDashboard.presets.custom') }
]);

function toDatetimeLocal(d: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function parseDatetimeLocal(value: string): Date | null {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

function presetHours(value: ReliabilityPreset): number | null {
  switch (value) {
    case ReliabilityPreset.Last8Hours: return 8;
    case ReliabilityPreset.Last24Hours: return 24;
    case ReliabilityPreset.Last7Days: return 7 * 24;
    case ReliabilityPreset.Last30Days: return 30 * 24;
    default: return null;
  }
}

function windowForPreset(value: ReliabilityPreset): { from: string; to: string } {
  const hours = presetHours(value);
  const to = new Date();
  if (hours === null) return { from: fromInput.value, to: toInput.value };
  return { from: toDatetimeLocal(new Date(to.getTime() - hours * 3600 * 1000)), to: toDatetimeLocal(to) };
}

function defaultWindow(): { from: string; to: string } {
  return windowForPreset(ReliabilityPreset.Last8Hours);
}

function isNotFoundError(err: unknown): boolean {
  if (typeof err !== 'object' || err === null) return false;
  return (err as { response?: { status?: unknown } }).response?.status === 404;
}

/**
 * Validates the filter inputs without touching the loaded panels: illegal
 * input shows the error toast and leaves prior data in place. The 93-day cap
 * and the reversed-window rule mirror the API validator so no request is
 * sent for input the backend would reject with 400.
 */
function readValidatedQuery(): GetReliabilitySnapshotQuery | null {
  const id = machineId.value;
  if (!id) {
    toast.error(t('reliabilityDashboard.invalidInput'));
    return null;
  }
  const windowError = validateReliabilityWindow(fromInput.value, toInput.value);
  if (windowError !== null) {
    toast.error(t('reliabilityDashboard.invalidInput'));
    return null;
  }
  const from = parseDatetimeLocal(fromInput.value);
  const to = parseDatetimeLocal(toInput.value);
  if (!from || !to) {
    toast.error(t('reliabilityDashboard.invalidInput'));
    return null;
  }
  return { machineId: id, fromUtc: from.toISOString(), toUtc: to.toISOString() };
}

function syncQuery(q: GetReliabilitySnapshotQuery): void {
  const next = { machineId: q.machineId, from: q.fromUtc, to: q.toUtc, preset: preset.value };
  const cur = route.query as Record<string, unknown>;
  // Guard the replace when the query already matches: clicking Apply twice
  // (or a watch echo) must not push a redundant navigation.
  if (
    String(cur.machineId ?? '') === next.machineId &&
    String(cur.from ?? '') === next.from &&
    String(cur.to ?? '') === next.to &&
    String(cur.preset ?? '') === next.preset
  ) {
    return;
  }
  // Remember the key we just synced so the query watcher can swallow its
  // own echo instead of fetching the snapshot a second time.
  lastAppliedKey = [next.machineId, next.from, next.to, next.preset].join('|');
  void router.replace({ query: { ...route.query, ...next } });
}

async function loadPanels(q: GetReliabilitySnapshotQuery): Promise<void> {
  loading.value = true;
  notFound.value = false;
  try {
    snapshot.value = await reliabilityService.getSnapshot(q);
    loadedOnce.value = true;
  } catch (err) {
    if (isNotFoundError(err)) {
      // Cross-tenant or deleted Work Center: the API hides foreign rows
      // with 404 — drop the panels and show the not-found feedback.
      notFound.value = true;
      snapshot.value = null;
    } else {
      toast.error(extractErrorMessage(err, t('errors.loadFailed')));
    }
  } finally {
    loading.value = false;
  }
}

async function refresh(): Promise<void> {
  const q = readValidatedQuery();
  if (!q) return;
  syncQuery(q);
  await loadPanels(q);
}

function clearPanels(): void {
  snapshot.value = null;
  notFound.value = false;
  loadedOnce.value = false;
}

function onMachineChange(v: string | number | null): void {
  machineId.value = v === null ? null : String(v);
  if (machineId.value) {
    void refresh();
  } else {
    clearPanels();
    const query = { ...route.query };
    delete query.machineId;
    void router.replace({ query });
  }
}

function onPresetChange(v: string | number | null): void {
  const next = v === null ? ReliabilityPreset.Custom : String(v) as ReliabilityPreset;
  preset.value = next;
  if (next !== ReliabilityPreset.Custom) {
    const window = windowForPreset(next);
    fromInput.value = window.from;
    toInput.value = window.to;
    if (machineId.value) void refresh();
  }
}

function onWindowChange(): void {
  // Manual edits detach the preset so the selector reflects a custom range.
  preset.value = ReliabilityPreset.Custom;
  if (machineId.value) void refresh();
}

function clearFilters(): void {
  preset.value = ReliabilityPreset.Last8Hours;
  const window = defaultWindow();
  fromInput.value = window.from;
  toInput.value = window.to;
  if (machineId.value) void refresh();
}

function readStateFromQuery(): void {
  const q = route.query;
  machineId.value = typeof q.machineId === 'string' && q.machineId ? q.machineId : null;
  const from = typeof q.from === 'string' ? new Date(q.from) : null;
  const to = typeof q.to === 'string' ? new Date(q.to) : null;
  const window = defaultWindow();
  fromInput.value = from && !Number.isNaN(from.getTime()) ? toDatetimeLocal(from) : window.from;
  toInput.value = to && !Number.isNaN(to.getTime()) ? toDatetimeLocal(to) : window.to;
  const rawPreset = typeof q.preset === 'string' ? q.preset : '';
  preset.value = Object.values(ReliabilityPreset).includes(rawPreset as ReliabilityPreset)
    ? (rawPreset as ReliabilityPreset)
    : ReliabilityPreset.Custom;
}

function queryKey(): string {
  const q = route.query;
  return [q.machineId, q.from, q.to, q.preset].map((v) => String(v ?? '')).join('|');
}

// Key of the last query we pushed via syncQuery. The query watcher swallows
// the echo of our own replace instead of fetching the snapshot twice.
let lastAppliedKey: string | null = null;

watch(queryKey, () => {
  if (lastAppliedKey !== null && queryKey() === lastAppliedKey) {
    lastAppliedKey = null;
    return;
  }
  lastAppliedKey = null;
  readStateFromQuery();
  if (machineId.value) void refresh();
});

onMounted(async () => {
  const window = defaultWindow();
  fromInput.value = window.from;
  toInput.value = window.to;
  readStateFromQuery();
  machinesLoading.value = true;
  try {
    // Machine picker is limited to active machines of the current tenant;
    // tenant isolation itself follows the snapshot API (404 for foreign ids).
    const page = await machineService.browse({ pageSize: 500, isActive: true });
    machines.value = page.items.filter((m) => m.isActive);
  } catch {
    // Work Center lookup stays empty; ids still render raw.
  } finally {
    machinesLoading.value = false;
  }
  if (machineId.value) await refresh();
});
</script>

<style scoped>
.reliability-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}
.reliability-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}
.reliability-card__title {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.reliability-card__value {
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}
.reliability-card__meta {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin-top: var(--space-1);
}
</style>
