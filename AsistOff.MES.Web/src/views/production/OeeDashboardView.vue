<template>
  <div>
    <AppPageHeader :title="$t('oeeDashboard.title')" :subtitle="$t('oeeDashboard.subtitle')" icon="pi pi-chart-bar">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="loading" @click="refresh">
          {{ $t('common.refresh') }}
        </AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearFilters">
      <AppFormField :label="$t('oeeDashboard.workCenter')">
        <template #default="{ id }">
          <AppSelect
            :id="id"
            v-model="machineId"
            :options="machineOptions"
            :placeholder="$t('oeeDashboard.selectWorkCenter')"
            :disabled="machinesLoading"
            @change="onMachineChange"
          />
        </template>
      </AppFormField>
      <AppFormField :label="$t('oeeDashboard.from')">
        <template #default="{ id }">
          <AppInput :id="id" v-model="fromInput" type="datetime-local" @change="onWindowChange" />
        </template>
      </AppFormField>
      <AppFormField :label="$t('oeeDashboard.to')">
        <template #default="{ id }">
          <AppInput :id="id" v-model="toInput" type="datetime-local" @change="onWindowChange" />
        </template>
      </AppFormField>
      <AppFormField :label="$t('oeeDashboard.idealCycleTime')">
        <template #default="{ id }">
          <AppNumberInput :id="id" v-model="idealInput" :min="1" :step="1" suffix="s" @blur="onIdealBlur" />
        </template>
      </AppFormField>
      <AppFormField :label="$t('oeeDashboard.bucket')">
        <template #default="{ id }">
          <AppSelect :id="id" v-model="bucket" :options="bucketOptions" @change="onBucketChange" />
        </template>
      </AppFormField>
      <template #actions>
        <AppButton variant="primary" icon="pi pi-check" :loading="loading" @click="refresh">
          {{ $t('oeeDashboard.apply') }}
        </AppButton>
      </template>
    </AppFilterBar>

    <AppSpinner v-if="loading && !loadedOnce" />
    <AppEmptyState
      v-else-if="!machineId"
      icon="pi pi-chart-bar"
      :title="$t('oeeDashboard.noMachine')"
      :description="$t('oeeDashboard.noMachineHint')"
    />
    <AppEmptyState
      v-else-if="notFound"
      icon="pi pi-exclamation-circle"
      :title="$t('oeeDashboard.notFound')"
      :description="$t('oeeDashboard.notFoundHint')"
    />
    <template v-else-if="snapshot">
      <AppEmptyState
        v-if="nullFactors"
        icon="pi pi-info-circle"
        :title="$t('oeeDashboard.nullFactorsTitle')"
        :description="$t('oeeDashboard.nullFactorsHint')"
      />

      <div class="oee-cards">
        <AppCard v-for="card in factorCards" :key="card.key" class="oee-card">
          <template #header>
            <div class="oee-card__header">
              <span class="oee-card__title">{{ card.title }}</span>
              <AppBadge :variant="card.computed ? 'success' : 'idle'" dot>
                {{ card.computed ? $t('oeeDashboard.computed') : $t('oeeDashboard.notComputed') }}
              </AppBadge>
            </div>
          </template>
          <div class="oee-card__value">{{ card.value }}</div>
          <div class="oee-card__meta">{{ card.hint }}</div>
        </AppCard>
      </div>

      <AppCard class="oee-section">
        <template #header>
          <h3 class="oee-section__title">{{ $t('oeeDashboard.trendTitle') }}</h3>
        </template>
        <AppTable
          :items="trendBuckets"
          :columns="trendColumns"
          :loading="loading"
          row-key="fromUtc"
          :empty-label="$t('oeeDashboard.trendEmpty')"
        >
          <template #cell-fromUtc="{ value }">{{ formatDateTime(String(value)) }}</template>
          <template #cell-toUtc="{ value }">{{ formatDateTime(String(value)) }}</template>
          <template #cell-oee="{ value }">{{ formatOeeFactor(toNullableNumber(value)) }}</template>
          <template #cell-availability="{ value }">{{ formatOeeFactor(toNullableNumber(value)) }}</template>
          <template #cell-performance="{ value }">{{ formatOeeFactor(toNullableNumber(value)) }}</template>
          <template #cell-quality="{ value }">{{ formatOeeFactor(toNullableNumber(value)) }}</template>
        </AppTable>
      </AppCard>

      <div class="oee-pareto">
        <AppCard class="oee-section">
          <template #header>
            <h3 class="oee-section__title">{{ $t('oeeDashboard.downtimeParetoTitle') }}</h3>
          </template>
          <AppTable
            :items="downtimePareto"
            :columns="downtimeColumns"
            :loading="loading"
            row-key="reasonCodeId"
            :empty-label="$t('oeeDashboard.downtimeEmpty')"
          >
            <template #cell-reason="{ item }">{{ paretoRowLabel(item) }}</template>
            <template #cell-share="{ value }">{{ formatOeeShare(toNullableNumber(value)) }}</template>
          </AppTable>
        </AppCard>

        <AppCard class="oee-section">
          <template #header>
            <h3 class="oee-section__title">{{ $t('oeeDashboard.scrapParetoTitle') }}</h3>
          </template>
          <AppTable
            :items="scrapPareto"
            :columns="scrapColumns"
            :loading="loading"
            row-key="reasonCodeId"
            :empty-label="$t('oeeDashboard.scrapEmpty')"
          >
            <template #cell-reason="{ item }">{{ paretoRowLabel(item) }}</template>
            <template #cell-share="{ value }">{{ formatOeeShare(toNullableNumber(value)) }}</template>
          </AppTable>
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
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import {
  OeeBucket,
  formatOeeFactor,
  formatOeeShare,
  hasNullFactors,
  oeeService,
  paretoLabel,
  type GetOeeLossesQuery,
  type GetOeeSnapshotQuery,
  type GetOeeTrendQuery,
  type OeeDowntimeParetoEntry,
  type OeeLosses,
  type OeeScrapParetoEntry,
  type OeeSnapshot,
  type OeeTrend
} from '../../services/oeeService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();
const route = useRoute();
const router = useRouter();

const DEFAULT_IDEAL = 60;
const DEFAULT_WINDOW_HOURS = 8;

const machines = ref<MachineResponse[]>([]);
const machinesLoading = ref(false);

const machineId = ref<string | null>(null);
const fromInput = ref('');
const toInput = ref('');
const idealInput = ref<number | null>(DEFAULT_IDEAL);
const bucket = ref<OeeBucket>(OeeBucket.Day);

const snapshot = ref<OeeSnapshot | null>(null);
const trend = ref<OeeTrend | null>(null);
const losses = ref<OeeLosses | null>(null);
const loading = ref(false);
const loadedOnce = ref(false);
const notFound = ref(false);

const nullFactors = computed(() => hasNullFactors(snapshot.value));
const trendBuckets = computed<OeeSnapshot[]>(() => trend.value?.buckets ?? []);
const downtimePareto = computed<OeeDowntimeParetoEntry[]>(() => losses.value?.downtimePareto ?? []);
const scrapPareto = computed<OeeScrapParetoEntry[]>(() => losses.value?.scrapPareto ?? []);

interface FactorCard {
  key: string;
  title: string;
  value: string;
  hint: string;
  computed: boolean;
}

const factorCards = computed<FactorCard[]>(() => {
  const s = snapshot.value;
  if (!s) return [];
  return [
    {
      key: 'oee',
      title: t('oeeDashboard.factors.oee'),
      value: formatOeeFactor(s.oee),
      computed: s.availabilityComputed && s.performanceComputed && s.qualityComputed,
      hint: t('oeeDashboard.totalsHint', {
        planned: formatMinutes(s.plannedProductionTimeMinutes),
        run: formatMinutes(s.runTimeMinutes)
      })
    },
    {
      key: 'availability',
      title: t('oeeDashboard.factors.availability'),
      value: formatOeeFactor(s.availability),
      computed: s.availabilityComputed,
      hint: t('oeeDashboard.downtimeHint', { minutes: formatMinutes(s.downtimeMinutes) })
    },
    {
      key: 'performance',
      title: t('oeeDashboard.factors.performance'),
      value: formatOeeFactor(s.performance),
      computed: s.performanceComputed,
      hint: t('oeeDashboard.countHint', { total: s.totalCount })
    },
    {
      key: 'quality',
      title: t('oeeDashboard.factors.quality'),
      value: formatOeeFactor(s.quality),
      computed: s.qualityComputed,
      hint: t('oeeDashboard.qualityHint', { good: s.goodCount, scrap: s.scrapCount })
    }
  ];
});

const machineOptions = computed<SelectOption[]>(() =>
  machines.value.map((m) => ({ value: m.id, label: `${m.code} — ${m.name}` }))
);

const bucketOptions = computed<SelectOption[]>(() => [
  { value: OeeBucket.Day, label: t('oeeDashboard.buckets.Day') },
  { value: OeeBucket.Week, label: t('oeeDashboard.buckets.Week') }
]);

const trendColumns = computed(() => [
  { key: 'fromUtc', label: t('oeeDashboard.bucketFrom') },
  { key: 'toUtc', label: t('oeeDashboard.bucketTo') },
  { key: 'oee', label: t('oeeDashboard.factors.oee'), align: 'right' as const },
  { key: 'availability', label: t('oeeDashboard.factors.availability'), align: 'right' as const },
  { key: 'performance', label: t('oeeDashboard.factors.performance'), align: 'right' as const },
  { key: 'quality', label: t('oeeDashboard.factors.quality'), align: 'right' as const }
]);

const downtimeColumns = computed(() => [
  { key: 'reason', label: t('oeeDashboard.reason') },
  { key: 'minutes', label: t('oeeDashboard.minutes'), align: 'right' as const },
  { key: 'share', label: t('oeeDashboard.share'), align: 'right' as const }
]);

const scrapColumns = computed(() => [
  { key: 'reason', label: t('oeeDashboard.reason') },
  { key: 'quantity', label: t('oeeDashboard.quantity'), align: 'right' as const },
  { key: 'share', label: t('oeeDashboard.share'), align: 'right' as const }
]);

function toNullableNumber(value: unknown): number | null {
  if (value === null || value === undefined) return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

function formatMinutes(value: number): string {
  return `${new Intl.NumberFormat(undefined, { maximumFractionDigits: 1 }).format(value)} ${t('oeeDashboard.minUnit')}`;
}

function formatDateTime(value: string): string {
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return d.toLocaleString();
}

function paretoRowLabel(item: OeeDowntimeParetoEntry | OeeScrapParetoEntry): string {
  return paretoLabel(item);
}

function toDatetimeLocal(d: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function parseDatetimeLocal(value: string): Date | null {
  if (!value) return null;
  const d = new Date(value);
  return Number.isNaN(d.getTime()) ? null : d;
}

function defaultWindow(): { from: string; to: string } {
  const to = new Date();
  const from = new Date(to.getTime() - DEFAULT_WINDOW_HOURS * 3600 * 1000);
  return { from: toDatetimeLocal(from), to: toDatetimeLocal(to) };
}

function isNotFoundError(err: unknown): boolean {
  if (typeof err !== 'object' || err === null) return false;
  return (err as { response?: { status?: unknown } }).response?.status === 404;
}

interface ValidatedQuery {
  snapshot: GetOeeSnapshotQuery;
  trend: GetOeeTrendQuery;
  losses: GetOeeLossesQuery;
}

/**
 * Validates the filter inputs without touching the loaded panels: illegal
 * input shows the error toast and leaves prior data in place.
 */
function readValidatedQuery(): ValidatedQuery | null {
  const id = machineId.value;
  const from = parseDatetimeLocal(fromInput.value);
  const to = parseDatetimeLocal(toInput.value);
  const ideal = idealInput.value;
  if (!id || !from || !to || from >= to || ideal === null || !Number.isFinite(ideal) || ideal <= 0) {
    toast.error(t('oeeDashboard.invalidInput'));
    return null;
  }
  const fromUtc = from.toISOString();
  const toUtc = to.toISOString();
  return {
    snapshot: { machineId: id, fromUtc, toUtc, idealCycleTimeSeconds: ideal },
    trend: { machineId: id, fromUtc, toUtc, idealCycleTimeSeconds: ideal, bucket: bucket.value },
    losses: { machineId: id, fromUtc, toUtc }
  };
}

function syncQuery(q: ValidatedQuery): void {
  const next = {
    machineId: q.snapshot.machineId,
    from: q.snapshot.fromUtc,
    to: q.snapshot.toUtc,
    ideal: String(q.snapshot.idealCycleTimeSeconds),
    bucket: bucket.value
  };
  const cur = route.query as Record<string, unknown>;
  // Guard the replace when the query already matches: clicking Apply twice
  // (or a watch echo) must not push a redundant navigation.
  if (
    String(cur.machineId ?? '') === next.machineId &&
    String(cur.from ?? '') === next.from &&
    String(cur.to ?? '') === next.to &&
    String(cur.ideal ?? '') === next.ideal &&
    String(cur.bucket ?? '') === next.bucket
  ) {
    return;
  }
  // Remember the key we just synced so the query watcher can swallow its
  // own echo instead of fetching every panel a second time.
  lastAppliedKey = [next.machineId, next.from, next.to, next.ideal, next.bucket].join('|');
  void router.replace({
    query: {
      ...route.query,
      ...next
    }
  });
}

async function loadPanels(q: ValidatedQuery): Promise<void> {
  loading.value = true;
  notFound.value = false;
  try {
    const [s, tr, lo] = await Promise.all([
      oeeService.getSnapshot(q.snapshot),
      oeeService.getTrend(q.trend),
      oeeService.getLosses(q.losses)
    ]);
    snapshot.value = s;
    trend.value = tr;
    losses.value = lo;
    loadedOnce.value = true;
  } catch (err) {
    if (isNotFoundError(err)) {
      // Cross-tenant or deleted Work Center: the API hides foreign rows
      // with 404 — drop the panels and show the not-found feedback.
      notFound.value = true;
      snapshot.value = null;
      trend.value = null;
      losses.value = null;
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
  trend.value = null;
  losses.value = null;
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

function onBucketChange(v: string | number | null): void {
  bucket.value = v === OeeBucket.Week ? OeeBucket.Week : OeeBucket.Day;
  if (machineId.value) void refresh();
}

function onWindowChange(): void {
  if (machineId.value) void refresh();
}

function onIdealBlur(): void {
  if (machineId.value) void refresh();
}

function clearFilters(): void {
  const window = defaultWindow();
  fromInput.value = window.from;
  toInput.value = window.to;
  idealInput.value = DEFAULT_IDEAL;
  bucket.value = OeeBucket.Day;
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
  const ideal = typeof q.ideal === 'string' ? Number(q.ideal) : Number.NaN;
  idealInput.value = Number.isFinite(ideal) && ideal > 0 ? ideal : DEFAULT_IDEAL;
  bucket.value = q.bucket === OeeBucket.Week ? OeeBucket.Week : OeeBucket.Day;
}

function queryKey(): string {
  const q = route.query;
  return [q.machineId, q.from, q.to, q.ideal, q.bucket].map((v) => String(v ?? '')).join('|');
}

// Key of the last query we pushed via syncQuery. The query watcher swallows
// the echo of our own replace instead of fetching every panel twice.
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
    const page = await machineService.browse({ pageSize: 500 });
    machines.value = page.items;
  } catch {
    // Work Center lookup stays empty; ids still render raw.
  } finally {
    machinesLoading.value = false;
  }
  if (machineId.value) await refresh();
});
</script>

<style scoped>
.oee-cards {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}
.oee-card__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}
.oee-card__title {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.oee-card__value {
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}
.oee-card__meta {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin-top: var(--space-1);
}
.oee-section {
  margin-bottom: var(--space-3);
}
.oee-section__title {
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
}
.oee-pareto {
  display: grid;
  grid-template-columns: repeat(2, minmax(0, 1fr));
  gap: var(--space-3);
}
@media (max-width: 1100px) {
  .oee-pareto {
    grid-template-columns: 1fr;
  }
}
</style>
