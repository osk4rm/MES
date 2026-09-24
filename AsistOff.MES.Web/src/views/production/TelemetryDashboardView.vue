<template>
  <div>
    <AppPageHeader :title="$t('telemetryDashboard.title')" :subtitle="$t('telemetryDashboard.subtitle')" icon="pi pi-chart-line">
      <template #actions>
        <AppBadge :variant="simulatorEnabled === true ? 'success' : 'idle'" dot>
          {{ simulatorEnabled === true ? $t('telemetry.simulator.enabled') : $t('telemetry.simulator.disabled') }}
        </AppBadge>
        <AppButton variant="secondary" :icon="autoRefresh ? 'pi pi-pause' : 'pi pi-play'" @click="toggleAutoRefresh">
          {{ autoRefresh ? $t('telemetryDashboard.pause') : $t('telemetryDashboard.resume') }}
        </AppButton>
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="loading" @click="refreshAll">
          {{ $t('common.refresh') }}
        </AppButton>
        <AppButton variant="primary" icon="pi pi-download" :loading="exporting" @click="downloadCsv">
          {{ $t('telemetryDashboard.downloadCsv') }}
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

    <AppSpinner v-if="loading && cards.length === 0" />
    <AppEmptyState
      v-else-if="cards.length === 0"
      :title="$t('telemetryDashboard.noTags')"
      :description="$t('telemetryDashboard.noTagsHint')"
    />

    <div v-else class="telemetry-grid">
      <AppCard
        v-for="card in cards"
        :key="card.tag.tagId"
        :class="['telemetry-card', card.tag.stale ? 'telemetry-card--stale' : '', !card.latest ? 'telemetry-card--never' : '']"
      >
        <template #header>
          <div class="telemetry-card__header">
            <div>
              <h3 class="telemetry-card__title">{{ card.tag.displayName }}</h3>
              <p class="telemetry-card__subtitle">{{ machineName(card.tag.machineId) }}</p>
            </div>
            <AppBadge :variant="card.tag.stale ? 'warning' : card.latest ? 'success' : 'idle'" dot>
              {{ card.tag.stale ? $t('telemetry.status.stale') : card.latest ? $t('telemetry.status.recent') : $t('telemetry.status.never') }}
            </AppBadge>
          </div>
        </template>

        <div class="telemetry-card__value-row">
          <span class="telemetry-card__value">{{ cardValue(card) }}</span>
          <AppBadge v-if="card.latest" :variant="qualityVariant(card.latest.quality)" dot>
            {{ qualityLabel(card.latest.quality) }}
          </AppBadge>
        </div>
        <div class="telemetry-card__meta">
          {{ cardAge(card) }}
        </div>

        <svg
          v-if="card.points"
          class="telemetry-card__sparkline"
          viewBox="0 0 120 36"
          role="img"
          :aria-label="$t('telemetryDashboard.trend')"
        >
          <polyline :points="card.points" fill="none" stroke="var(--color-primary)" stroke-width="1.5" />
        </svg>
        <div v-else class="telemetry-card__no-trend">
          {{ $t('telemetryDashboard.noTrend') }}
        </div>
      </AppCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, onUnmounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppSelect, { type SelectOption } from '../../components/ui/AppSelect.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import {
  telemetryTagService,
  telemetryReadingService,
  TelemetryQuality,
  type TelemetryReadingResponse,
  type TelemetryTagStatusEntry
} from '../../services/telemetryService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

const REFRESH_MS = 30000;
const TREND_TAKE = 50;

interface DashboardCard {
  tag: TelemetryTagStatusEntry;
  latest: TelemetryReadingResponse | null;
  points: string | null;
}

const machines = ref<MachineResponse[]>([]);
const statusTags = ref<TelemetryTagStatusEntry[]>([]);
const trends = ref<Map<string, TelemetryReadingResponse[]>>(new Map());
const simulatorEnabled = ref<boolean | null>(null);
const loading = ref(false);
const exporting = ref(false);
const autoRefresh = ref(true);
const machineFilter = ref<string | number | null>(null);
const machineIdFilter = ref<string | undefined>(undefined);
let timer = 0;

function machineName(id: string): string {
  return machines.value.find((m) => m.id === id)?.name ?? id;
}

function qualityLabel(v: number): string {
  return t(`telemetry.qualities.${v}`);
}

function qualityVariant(v: number): 'success' | 'danger' | 'warning' {
  if (v === TelemetryQuality.Good) return 'success';
  if (v === TelemetryQuality.Bad) return 'danger';
  return 'warning';
}

function readingValue(r: TelemetryReadingResponse): string {
  return r.stringValue ?? (r.doubleValue !== null && r.doubleValue !== undefined ? String(r.doubleValue) : '—');
}

function sparklinePoints(readings: TelemetryReadingResponse[]): string | null {
  const values = readings
    .map((r) => r.doubleValue)
    .filter((v): v is number => v !== null && v !== undefined);
  if (values.length < 2) return null;
  const min = Math.min(...values);
  const max = Math.max(...values);
  const span = max - min || 1;
  return values
    .map((v, i) => {
      const x = (i / (values.length - 1)) * 120;
      const y = 34 - ((v - min) / span) * 32;
      return `${x.toFixed(1)},${y.toFixed(1)}`;
    })
    .join(' ');
}

const cards = computed<DashboardCard[]>(() =>
  statusTags.value
    .filter((tag) => tag.isEnabled)
    .filter((tag) => !machineIdFilter.value || tag.machineId === machineIdFilter.value)
    .map((tag) => {
      const trend = trends.value.get(tag.tagId) ?? [];
      const latest = trend.length > 0 ? trend[trend.length - 1] : null;
      return { tag, latest, points: sparklinePoints(trend) };
    })
);

function cardValue(card: DashboardCard): string {
  if (!card.latest) return t('telemetryDashboard.neverReported');
  return readingValue(card.latest);
}

function cardAge(card: DashboardCard): string {
  if (!card.latest?.readAt) return t('telemetryDashboard.neverReported');
  const seconds = Math.max(0, Math.round((Date.now() - new Date(card.latest.readAt).getTime()) / 1000));
  if (seconds < 60) return t('telemetryDashboard.ageSeconds', { n: seconds });
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return t('telemetryDashboard.ageMinutes', { n: minutes });
  return t('telemetryDashboard.ageHours', { n: Math.floor(minutes / 60) });
}

const machineFilterOptions = computed<SelectOption[]>(() => [
  { value: null, label: t('telemetryDashboard.allMachines') },
  ...machines.value.map((m) => ({ value: m.id, label: `${m.code} — ${m.name}` }))
]);

function onMachineChange(v: string | number | null): void {
  machineIdFilter.value = v === null ? undefined : String(v);
}

function clearFilters(): void {
  machineFilter.value = null;
  machineIdFilter.value = undefined;
}

async function refreshAll(): Promise<void> {
  loading.value = true;
  try {
    const status = await telemetryTagService.getStatus();
    simulatorEnabled.value = status.simulatorEnabled;
    const enabled = status.tags.filter((tag) => tag.isEnabled);
    statusTags.value = status.tags;
    const entries = await Promise.all(
      enabled.map(async (tag): Promise<[string, TelemetryReadingResponse[]]> => {
        try {
          return [tag.tagId, await telemetryReadingService.trend(tag.tagId, TREND_TAKE)];
        } catch {
          return [tag.tagId, []];
        }
      })
    );
    trends.value = new Map(entries);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    loading.value = false;
  }
}

function toggleAutoRefresh(): void {
  autoRefresh.value = !autoRefresh.value;
  restartTimer();
}

function restartTimer(): void {
  window.clearInterval(timer);
  timer = 0;
  if (autoRefresh.value) {
    timer = window.setInterval(() => void refreshAll(), REFRESH_MS);
  }
}

async function downloadCsv(): Promise<void> {
  exporting.value = true;
  try {
    const blob = await telemetryReadingService.downloadCsv(
      machineIdFilter.value ? { machineId: machineIdFilter.value } : {}
    );
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = 'telemetry-readings.csv';
    document.body.appendChild(link);
    link.click();
    link.remove();
    URL.revokeObjectURL(url);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    exporting.value = false;
  }
}

onMounted(async () => {
  try {
    const m = await machineService.browse({ pageSize: 500 });
    machines.value = m.items;
  } catch {
    // lookups stay empty; ids are still rendered raw
  }
  await refreshAll();
  restartTimer();
});

onUnmounted(() => {
  window.clearInterval(timer);
});
</script>

<style scoped>
.telemetry-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(280px, 1fr));
  gap: var(--space-4);
  margin-top: var(--space-4);
}
.telemetry-card__header {
  display: flex;
  align-items: flex-start;
  justify-content: space-between;
  gap: var(--space-2);
}
.telemetry-card__title {
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
}
.telemetry-card__subtitle {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin-top: var(--space-1);
}
.telemetry-card--stale {
  border: 1px solid var(--color-warning);
}
.telemetry-card--never {
  opacity: 0.75;
}
.telemetry-card__value-row {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
}
.telemetry-card__value {
  font-size: var(--font-size-xl);
  font-weight: var(--font-weight-bold);
}
.telemetry-card__meta {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin-top: var(--space-1);
}
.telemetry-card__sparkline {
  width: 100%;
  height: 48px;
  margin-top: var(--space-2);
}
.telemetry-card__no-trend {
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
  margin-top: var(--space-2);
}
</style>
