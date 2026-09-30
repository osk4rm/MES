<template>
  <div>
    <AppPageHeader :title="$t('dashboard.title')" :subtitle="$t('dashboard.subtitle')" icon="pi pi-chart-pie" />
    <section class="dashboard__hero" :aria-label="$t('dashboard.organizationHeroTitle')">
      <img
        class="dashboard__hero-logo"
        src="/organization-logo.svg"
        :alt="$t('dashboard.organizationLogoAlt')"
        data-testid="organization-logo"
      />
      <p class="dashboard__hero-caption">{{ $t('dashboard.organizationHeroText') }}</p>
    </section>
    <div class="dashboard__banner">
      <i class="pi pi-info-circle" aria-hidden="true"></i>
      {{ $t('dashboard.placeholderNote') }}
    </div>
    <AppDataState
      :loading="loading"
      :error="loadError"
      :empty="isEmpty"
      :empty-title="$t('dashboard.emptyTitle')"
      :empty-description="$t('dashboard.emptyHint')"
      @retry="load"
    >
      <div class="dashboard__kpis">
        <AppCard v-for="k in kpis" :key="k.key" :padded="false">
          <div class="kpi">
            <div :class="['kpi__icon', k.iconClass]"><i :class="k.icon" aria-hidden="true"></i></div>
            <div class="kpi__text">
              <div class="kpi__label">{{ $t(k.labelKey) }}</div>
              <div class="kpi__value">{{ k.value }}</div>
            </div>
            <AppBadge variant="success" icon="pi pi-check-circle" dot>{{ $t('dashboard.live') }}</AppBadge>
          </div>
        </AppCard>
      </div>
    </AppDataState>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../components/ui/AppPageHeader.vue';
import AppCard from '../components/ui/AppCard.vue';
import AppBadge from '../components/ui/AppBadge.vue';
import AppDataState from '../components/ui/AppDataState.vue';
import { productionOrderService, ProductionOrderStatus } from '../services/productionOrderService';
import { machineService } from '../services/machineService';
import { operatorService } from '../services/operatorService';
import { warehouseService } from '../services/warehouseService';
import { extractErrorMessage } from '../services/http';

const { t } = useI18n();

const loading = ref(false);
const loadedOnce = ref(false);
const loadError = ref<string | null>(null);

const activeOrders = ref(0);
const machines = ref(0);
const operators = ref(0);
const warehouses = ref(0);

const totalCount = computed(
  () => activeOrders.value + machines.value + operators.value + warehouses.value
);
// Error wins over loading wins over empty wins over content (AppDataState):
// before the first load the region spins, an all-zero tenant reads empty.
const isEmpty = computed(() => !loadedOnce.value || totalCount.value === 0);

const kpis = computed(() => [
  { key: 'orders', labelKey: 'dashboard.activeOrders', value: activeOrders.value, icon: 'pi pi-list', iconClass: 'kpi__icon--orders' },
  { key: 'machines', labelKey: 'dashboard.machines', value: machines.value, icon: 'pi pi-cog', iconClass: 'kpi__icon--machines' },
  { key: 'operators', labelKey: 'dashboard.operators', value: operators.value, icon: 'pi pi-users', iconClass: 'kpi__icon--operators' },
  { key: 'warehouses', labelKey: 'dashboard.warehouses', value: warehouses.value, icon: 'pi pi-building', iconClass: 'kpi__icon--warehouses' }
]);

async function load(): Promise<void> {
  loading.value = true;
  loadError.value = null;
  try {
    // Count-only reads (pageSize 1): the cards show live tenant totals.
    // "Active" Production Orders are Released + InProgress; the browse
    // endpoint filters a single status, so both counts are summed.
    const [released, inProgress, machinePage, operatorPage, warehousePage] = await Promise.all([
      productionOrderService.browse({ pageNumber: 1, pageSize: 1, status: ProductionOrderStatus.Released }),
      productionOrderService.browse({ pageNumber: 1, pageSize: 1, status: ProductionOrderStatus.InProgress }),
      machineService.browse({ pageNumber: 1, pageSize: 1 }),
      operatorService.browse({ pageNumber: 1, pageSize: 1 }),
      warehouseService.browse({ pageNumber: 1, pageSize: 1 })
    ]);
    activeOrders.value = released.totalCount + inProgress.totalCount;
    machines.value = machinePage.totalCount;
    operators.value = operatorPage.totalCount;
    warehouses.value = warehousePage.totalCount;
    loadedOnce.value = true;
  } catch (err) {
    loadError.value = extractErrorMessage(err, t('errors.loadFailed'));
  } finally {
    loading.value = false;
  }
}

onMounted(() => {
  void load();
});
</script>

<style scoped>
.dashboard__hero {
  margin-bottom: var(--space-5);
  border-radius: var(--radius-md);
  overflow: hidden;
  background: var(--color-primary-soft);
  border: 1px solid var(--color-border);
}

.dashboard__hero-logo {
  display: block;
  width: 100%;
  height: clamp(180px, 28vw, 340px);
  object-fit: cover;
}

.dashboard__hero-caption {
  padding: var(--space-2) var(--space-4);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

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
.kpi__icon--orders { color: var(--color-primary); background: var(--color-primary-soft); }
.kpi__icon--machines { color: var(--color-success); background: var(--color-success-soft); }
.kpi__icon--operators { color: var(--color-info); background: var(--color-info-soft); }
.kpi__icon--warehouses { color: var(--color-warning); background: var(--color-warning-soft); }
.kpi__text { flex: 1; min-width: 0; }
.kpi__label { font-size: var(--font-size-sm); color: var(--color-text-muted); text-transform: uppercase; letter-spacing: 0.03em; }
.kpi__value { font-size: var(--font-size-2xl); font-weight: var(--font-weight-semibold); color: var(--color-text); font-variant-numeric: tabular-nums; }
</style>
