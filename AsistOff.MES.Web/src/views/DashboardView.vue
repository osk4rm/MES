<template>
  <div>
    <AppPageHeader :title="$t('dashboard.title')" :subtitle="$t('dashboard.subtitle')" icon="pi pi-chart-pie" />
    <div class="dashboard__banner">
      <i class="pi pi-info-circle" aria-hidden="true"></i>
      {{ $t('dashboard.placeholderNote') }}
    </div>
    <div class="dashboard__kpis">
      <AppCard v-for="k in kpis" :key="k.key" :padded="false">
        <div class="kpi">
          <div class="kpi__icon" :style="{ color: k.color, background: k.bg }"><i :class="k.icon"></i></div>
          <div class="kpi__text">
            <div class="kpi__label">{{ $t(k.labelKey) }}</div>
            <div class="kpi__value">{{ k.value }}<span v-if="k.unit" class="kpi__unit">{{ k.unit }}</span></div>
          </div>
          <AppBadge :variant="k.status.variant" dot>{{ k.status.label }}</AppBadge>
        </div>
      </AppCard>
    </div>
  </div>
</template>

<script setup lang="ts">
import AppPageHeader from '../components/ui/AppPageHeader.vue';
import AppCard from '../components/ui/AppCard.vue';
import AppBadge from '../components/ui/AppBadge.vue';

const kpis = [
  { key: 'orders', labelKey: 'dashboard.activeOrders', value: 0, unit: '', icon: 'pi pi-list', color: '#1e3a5f', bg: '#e5edf5', status: { variant: 'idle' as const, label: '—' } },
  { key: 'oee', labelKey: 'dashboard.oee', value: 0, unit: '%', icon: 'pi pi-chart-bar', color: '#147a4a', bg: '#e0f3ea', status: { variant: 'idle' as const, label: '—' } },
  { key: 'operators', labelKey: 'dashboard.operatorsOnline', value: 0, unit: '', icon: 'pi pi-users', color: '#0b6bb8', bg: '#e0eef9', status: { variant: 'idle' as const, label: '—' } },
  { key: 'warehouses', labelKey: 'dashboard.warehouses', value: 0, unit: '', icon: 'pi pi-building', color: '#b45309', bg: '#fdf0dd', status: { variant: 'idle' as const, label: '—' } }
];
</script>

<style scoped>
.dashboard__banner {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-2) var(--space-3);
  background: var(--color-info-soft);
  color: var(--color-info);
  border-radius: var(--radius-md);
  font-size: var(--font-size-sm);
  margin-bottom: var(--space-5);
}

.dashboard__kpis {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(240px, 1fr));
  gap: var(--space-4);
}

.kpi { display: flex; align-items: center; gap: var(--space-3); padding: var(--space-4); }
.kpi__icon { width: 40px; height: 40px; border-radius: var(--radius-md); display: inline-flex; align-items: center; justify-content: center; font-size: 18px; flex-shrink: 0; }
.kpi__text { flex: 1; min-width: 0; }
.kpi__label { font-size: var(--font-size-sm); color: var(--color-text-muted); text-transform: uppercase; letter-spacing: 0.03em; }
.kpi__value { font-size: var(--font-size-2xl); font-weight: var(--font-weight-semibold); color: var(--color-text); font-variant-numeric: tabular-nums; }
.kpi__unit { font-size: var(--font-size-md); color: var(--color-text-muted); margin-left: var(--space-1); font-weight: var(--font-weight-regular); }
</style>
