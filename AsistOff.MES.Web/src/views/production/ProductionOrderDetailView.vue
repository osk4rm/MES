<template>
  <div v-if="order" class="order-detail">
    <AppPageHeader :title="order.code" :subtitle="$t('productionOrders.detail.subtitle')" icon="pi pi-list">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-arrow-left" @click="$router.push({ name: 'production-orders' })">
          {{ $t('common.back') }}
        </AppButton>
        <AppButton
          v-if="order.status === ProductionOrderStatus.Planned"
          variant="secondary"
          icon="pi pi-check"
          :loading="releasing"
          @click="onRelease"
        >
          {{ $t('productionOrders.release') }}
        </AppButton>
        <AppButton
          v-if="canReport"
          variant="primary"
          icon="pi pi-plus"
          @click="openReport"
        >
          {{ $t('productionConfirmations.report') }}
        </AppButton>
        <AppButton
          v-if="canComplete"
          variant="primary"
          icon="pi pi-check-circle"
          :loading="completing"
          @click="askComplete"
        >
          {{ $t('productionOrders.complete') }}
        </AppButton>
        <AppButton
          v-if="canClose"
          variant="secondary"
          icon="pi pi-lock"
          :loading="closing"
          @click="askClose"
        >
          {{ $t('productionOrders.close') }}
        </AppButton>
      </template>
    </AppPageHeader>

    <AppCard :title="$t('productionOrders.detail.summary')">
      <div class="summary-grid">
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('common.status') }}</span>
          <AppBadge :variant="statusVariant(order.status)" dot>
            {{ statusLabel(order.status) }}
          </AppBadge>
        </div>
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.plannedQuantity') }}</span>
          <span>{{ formatQuantity(order.plannedQuantity) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.detail.producedQuantity') }}</span>
          <span>{{ formatQuantity(order.producedQuantity ?? 0) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.detail.scrappedQuantity') }}</span>
          <span>{{ formatQuantity(order.scrappedQuantity ?? 0) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.detail.remainingQuantity') }}</span>
          <span>{{ formatQuantity(order.remainingQuantity ?? order.plannedQuantity) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.detail.confirmationsCount') }}</span>
          <span>{{ order.confirmationsCount ?? 0 }}</span>
        </div>
        <div class="summary-item summary-item--full">
          <span class="summary-item__label">{{ $t('productionOrders.detail.progress') }} ({{ progressPercent }}%)</span>
          <div class="progress" role="progressbar" :aria-valuenow="progressPercent" aria-valuemin="0" aria-valuemax="100">
            <div class="progress__bar" :style="{ width: progressPercent + '%' }" />
          </div>
        </div>
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.priority') }}</span>
          <span>{{ order.priority }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.dueDate') }}</span>
          <span>{{ formatDate(order.dueDate) }}</span>
        </div>
        <div class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.detail.releasedAt') }}</span>
          <span>{{ formatDateTime(order.releasedAt) }}</span>
        </div>
        <div v-if="order.completedAt" class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.detail.completedAt') }}</span>
          <span>{{ formatDateTime(order.completedAt) }}</span>
        </div>
        <div v-if="order.closedAt" class="summary-item">
          <span class="summary-item__label">{{ $t('productionOrders.detail.closedAt') }}</span>
          <span>{{ formatDateTime(order.closedAt) }}</span>
        </div>
        <div v-if="order.notes" class="summary-item summary-item--full">
          <span class="summary-item__label">{{ $t('productionOrders.notes') }}</span>
          <span>{{ order.notes }}</span>
        </div>
      </div>
    </AppCard>

    <section class="confirmations-section">
      <div class="section-header">
        <h3>{{ $t('movements.title') }}</h3>
        <AppButton variant="ghost" icon="pi pi-refresh" :loading="movementsLoading" @click="loadMovements">
          {{ $t('common.refresh') }}
        </AppButton>
      </div>
      <p class="section-subtitle">{{ $t('movements.subtitle') }}</p>

      <AppTable
        :items="movements"
        :columns="movementColumns"
        :loading="movementsLoading"
      >
        <template #cell-movementType="{ value }">
          <AppBadge :variant="value === 'PW' ? 'success' : 'info'" dot>
            {{ value }}
          </AppBadge>
        </template>
        <template #cell-productId="{ value }">
          {{ productLabel(value) }}
        </template>
        <template #cell-quantity="{ value }">
          {{ formatQuantity(value) }}
        </template>
        <template #cell-preferredWarehouseId="{ value }">
          {{ warehouseLabel(value) }}
        </template>
      </AppTable>

      <AppEmptyState
        v-if="!movementsLoading && movements.length === 0"
        icon="pi pi-list"
        :title="$t('movements.empty')"
      />
    </section>

    <section class="confirmations-section">
      <h3>{{ $t('productionConfirmations.title') }}</h3>

      <AppTable
        :items="table.items.value"
        :columns="columns"
        :loading="table.loading.value"
        :sort-key="table.sortKey.value"
        :sort-direction="table.sortDirection.value"
        @sort-change="table.setSort"
      >
        <template #cell-reportedAt="{ value }">
          {{ formatDateTime(value) }}
        </template>
        <template #cell-machineId="{ value }">
          {{ machineLabel(value) }}
        </template>
        <template #cell-reportedByOperatorId="{ value }">
          {{ operatorLabel(value) }}
        </template>
        <template #cell-goodQuantity="{ value }">
          {{ formatQuantity(value) }}
        </template>
        <template #cell-scrapQuantity="{ value }">
          {{ formatQuantity(value) }}
        </template>
        <template #cell-actions="{ item }">
          <AppRowActions
            :actions="confirmationActions(item)"
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

      <AppEmptyState
        v-if="!table.loading.value && table.items.value.length === 0"
        icon="pi pi-check-square"
        :title="$t('productionConfirmations.empty')"
      />
    </section>

    <AppModal :open="modalOpen" :title="$t('productionConfirmations.report')" @close="closeModal">
      <form id="confirmation-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('productionConfirmations.machine')" required>
          <template #default="{ id }">
            <AppSelect
              :id="id"
              v-model="form.machineId"
              :options="machineOptions"
              :placeholder="$t('productionConfirmations.selectMachine')"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.operator')">
          <template #default="{ id }">
            <AppSelect
              :id="id"
              v-model="form.operatorId"
              :options="operatorFilterOptions"
              :placeholder="$t('productionConfirmations.selectOperator')"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.reportedAt')" required>
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.reportedAt" type="datetime-local" />
          </template>
        </AppFormField>
        <div />
        <AppFormField :label="$t('productionConfirmations.goodQuantity')" required>
          <template #default="{ id, invalid }">
            <AppNumberInput :id="id" v-model="form.goodQuantity" :min="0" :step="1" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.scrapQuantity')" required>
          <template #default="{ id, invalid }">
            <AppNumberInput :id="id" v-model="form.scrapQuantity" :min="0" :step="1" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.notes" :rows="2" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.producedLot')" class="form-grid__full">
          <template #default="{ id }">
            <AppSelect
              :id="id"
              v-model="form.producedLotId"
              :options="lotOptions"
              :placeholder="$t('productionConfirmations.selectProducedLot')"
              allow-empty
            />
          </template>
        </AppFormField>
        <p class="form-hint form-grid__full">{{ $t('productionConfirmations.lotsHint') }}</p>
        <div class="form-grid__full consumed-lots">
          <div class="consumed-lots__header">
            <span>{{ $t('productionConfirmations.consumedLots') }}</span>
            <AppButton variant="ghost" icon="pi pi-plus" @click="addConsumedRow">
              {{ $t('productionConfirmations.addConsumedLot') }}
            </AppButton>
          </div>
          <div v-for="(row, index) in form.consumedLots" :key="index" class="consumed-lots__row">
            <AppSelect
              v-model="row.lotId"
              :options="lotOptions"
              :placeholder="$t('productionConfirmations.selectConsumedLot')"
              :aria-label="$t('productionConfirmations.consumedLot')"
            />
            <AppNumberInput
              v-model="row.quantity"
              :min="0"
              :step="1"
              :placeholder="$t('productionConfirmations.consumedQuantity')"
            />
            <AppButton
              variant="ghost"
              icon="pi pi-trash"
              :aria-label="$t('productionConfirmations.removeConsumedLot')"
              @click="removeConsumedRow(index)"
            />
          </div>
          <p v-if="lotsError" class="form-error" role="alert">{{ lotsError }}</p>
        </div>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="confirmation-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
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

    <AppConfirmDialog
      :open="lifecycleOpen"
      :title="lifecycleTitle"
      :message="lifecycleMessage"
      :loading="completing || closing"
      @confirm="confirmLifecycle"
      @cancel="cancelLifecycle"
    />

    <AppModal :open="movementsModalOpen" :title="$t('movements.perConfirmationTitle')" @close="closeMovementsModal">
      <AppTable
        :items="confirmationMovements"
        :columns="movementColumns"
        :loading="confirmationMovementsLoading"
      >
        <template #cell-movementType="{ value }">
          <AppBadge :variant="value === 'PW' ? 'success' : 'info'" dot>
            {{ value }}
          </AppBadge>
        </template>
        <template #cell-productId="{ value }">
          {{ productLabel(value) }}
        </template>
        <template #cell-quantity="{ value }">
          {{ formatQuantity(value) }}
        </template>
        <template #cell-preferredWarehouseId="{ value }">
          {{ warehouseLabel(value) }}
        </template>
      </AppTable>
      <AppEmptyState
        v-if="!confirmationMovementsLoading && confirmationMovements.length === 0"
        icon="pi pi-list"
        :title="$t('movements.empty')"
      />
      <template #footer>
        <AppButton variant="ghost" @click="closeMovementsModal">{{ $t('common.close') }}</AppButton>
      </template>
    </AppModal>
  </div>
  <div v-else-if="loading" class="loading"><i class="pi pi-spin pi-spinner" /> {{ $t('common.loading') }}</div>
  <div v-else class="loading">{{ $t('common.notFound') }}</div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useRoute } from 'vue-router';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import {
  productionOrderService,
  ProductionOrderStatus,
  type ProductionOrderResponse
} from '../../services/productionOrderService';
import {
  productionConfirmationService,
  type ConsumedLotInput,
  type MovementPreviewLine,
  type ProductionConfirmationResponse
} from '../../services/productionConfirmationService';
import { validateConfirmationLots } from '../../services/productionConfirmationLots';
import { lotService, type LotResponse } from '../../services/lotService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { operatorService, type OperatorResponse } from '../../services/operatorService';
import { productService, type ProductResponse } from '../../services/productService';
import { warehouseService, type WarehouseResponse } from '../../services/warehouseService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const route = useRoute();
const { t } = useI18n();
const toast = useToastStore();

const orderId = route.params.id as string;
const order = ref<ProductionOrderResponse | null>(null);
const loading = ref(false);
const releasing = ref(false);

const canReport = computed(() =>
  order.value !== null &&
  (order.value.status === ProductionOrderStatus.Released ||
    order.value.status === ProductionOrderStatus.InProgress));

const canComplete = computed(() =>
  order.value !== null && order.value.status === ProductionOrderStatus.InProgress);

const canClose = computed(() =>
  order.value !== null && order.value.status === ProductionOrderStatus.Completed);

const progressPercent = computed(() => {
  if (!order.value || order.value.plannedQuantity <= 0) return 0;
  const produced = order.value.producedQuantity ?? 0;
  return Math.min(100, Math.round((produced / order.value.plannedQuantity) * 100));
});

function statusVariant(v: number): 'info' | 'primary' | 'success' | 'warning' | 'idle' {
  switch (v) {
    case ProductionOrderStatus.Planned: return 'info';
    case ProductionOrderStatus.Released: return 'success';
    case ProductionOrderStatus.InProgress: return 'warning';
    case ProductionOrderStatus.Completed: return 'primary';
    case ProductionOrderStatus.Closed: return 'idle';
    default: return 'info';
  }
}

interface Filters { productionOrderId?: string }

const table = useCrudPage<ProductionConfirmationResponse, Filters>({
  fetch: (req) => productionConfirmationService.browse(req),
  initialFilters: { productionOrderId: orderId }
});

const columns = computed(() => [
  { key: 'reportedAt', label: t('productionConfirmations.reportedAt'), sortable: true },
  { key: 'machineId', label: t('productionConfirmations.machine') },
  { key: 'reportedByOperatorId', label: t('productionConfirmations.operator') },
  { key: 'goodQuantity', label: t('productionConfirmations.goodQuantity'), align: 'right' as const },
  { key: 'scrapQuantity', label: t('productionConfirmations.scrapQuantity'), align: 'right' as const },
  { key: 'notes', label: t('productionConfirmations.notes') },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

function statusLabel(v: number): string {
  switch (v) {
    case ProductionOrderStatus.Planned: return t('productionOrders.status.planned');
    case ProductionOrderStatus.Released: return t('productionOrders.status.released');
    case ProductionOrderStatus.InProgress: return t('productionOrders.status.inProgress');
    case ProductionOrderStatus.Completed: return t('productionOrders.status.completed');
    case ProductionOrderStatus.Closed: return t('productionOrders.status.closed');
    default: return String(v);
  }
}

function formatDate(v: string | null | undefined): string {
  if (!v) return '—';
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleDateString();
}

function formatDateTime(v: string | null | undefined): string {
  if (!v) return '—';
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString();
}

function formatQuantity(v: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(v);
}

const machines = ref<MachineResponse[]>([]);
const operators = ref<OperatorResponse[]>([]);
const products = ref<ProductResponse[]>([]);
const warehouses = ref<WarehouseResponse[]>([]);
const lots = ref<LotResponse[]>([]);

const machineOptions = computed(() => machines.value.map(m => ({ value: m.id, label: `${m.code} — ${m.name}` })));
const lotOptions = computed(() => lots.value.map(l => ({ value: l.id, label: `${l.code}` })));
const operatorFilterOptions = computed(() => [
  { value: null, label: t('productionConfirmations.selectOperator') },
  ...operators.value.map(o => ({ value: o.id, label: `${o.identifier} — ${o.firstName} ${o.lastName}` }))
]);

function machineLabel(id: string): string {
  const m = machines.value.find(x => x.id === id);
  return m ? `${m.code} — ${m.name}` : id;
}

function operatorLabel(id: string | null | undefined): string {
  if (!id) return '—';
  const o = operators.value.find(x => x.id === id);
  return o ? `${o.identifier} — ${o.firstName} ${o.lastName}` : id;
}

function productLabel(id: string): string {
  const p = products.value.find(x => x.id === id);
  return p ? `${p.code} — ${p.name}` : id;
}

function warehouseLabel(id: string | null | undefined): string {
  if (!id) return '—';
  const w = warehouses.value.find(x => x.id === id);
  return w ? w.name : id;
}

async function loadOrder(): Promise<void> {
  loading.value = true;
  try {
    order.value = await productionOrderService.get(orderId);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    loading.value = false;
  }
}

async function loadLookups(): Promise<void> {
  try {
    const [m, o, p, w, l] = await Promise.all([
      machineService.browse({ pageNumber: 1, pageSize: 500 }),
      operatorService.browse({ pageNumber: 1, pageSize: 500 }),
      productService.browse({ pageNumber: 1, pageSize: 500 }),
      warehouseService.browse({ pageNumber: 1, pageSize: 500 }),
      lotService.browse({ pageNumber: 1, pageSize: 500 })
    ]);
    machines.value = m.items;
    operators.value = o.items;
    products.value = p.items;
    warehouses.value = w.items;
    lots.value = l.items;
  } catch {
    /* ignore — table still renders */
  }
}

async function onRelease(): Promise<void> {
  if (!order.value) return;
  releasing.value = true;
  try {
    order.value = await productionOrderService.release(order.value.id);
    toast.success(t('productionOrders.releasedToast'));
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    releasing.value = false;
  }
}

function toLocalInputValue(d: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

const modalOpen = ref(false);
const saving = ref(false);

interface ConsumedLotRow {
  lotId: string | null;
  quantity: number | null;
}

const form = reactive({
  machineId: null as string | null,
  operatorId: null as string | null,
  reportedAt: '',
  goodQuantity: null as number | null,
  scrapQuantity: null as number | null,
  notes: '' as string | null,
  producedLotId: null as string | null,
  consumedLots: [] as ConsumedLotRow[]
});

const lotsError = computed(() => {
  const payload: ConsumedLotInput[] = form.consumedLots.map(r => ({
    lotId: r.lotId ?? '',
    quantity: r.quantity ?? 0
  }));
  const violation = validateConfirmationLots(form.producedLotId, payload);
  return violation ? t(`productionConfirmations.lotsErrors.${violation}`) : '';
});

function addConsumedRow(): void {
  form.consumedLots.push({ lotId: null, quantity: null });
}

function removeConsumedRow(index: number): void {
  form.consumedLots.splice(index, 1);
}

function openReport(): void {
  Object.assign(form, {
    machineId: null,
    operatorId: null,
    reportedAt: toLocalInputValue(new Date()),
    goodQuantity: null,
    scrapQuantity: null,
    notes: '',
    producedLotId: null,
    consumedLots: []
  });
  modalOpen.value = true;
}

function closeModal(): void {
  if (saving.value) return;
  modalOpen.value = false;
}

async function onSave(): Promise<void> {
  if (!order.value) return;
  if (!form.machineId || !form.reportedAt) {
    toast.error(t('validation.required'));
    return;
  }
  const good = form.goodQuantity ?? 0;
  const scrap = form.scrapQuantity ?? 0;
  if (good <= 0 && scrap <= 0) {
    toast.error(t('productionConfirmations.positiveQuantityRequired'));
    return;
  }
  const consumedPayload: ConsumedLotInput[] = form.consumedLots.map(r => ({
    lotId: r.lotId ?? '',
    quantity: r.quantity ?? 0
  }));
  const lotsViolation = validateConfirmationLots(form.producedLotId, consumedPayload);
  if (lotsViolation) {
    toast.error(t(`productionConfirmations.lotsErrors.${lotsViolation}`));
    return;
  }
  saving.value = true;
  try {
    await productionConfirmationService.create({
      productionOrderId: order.value.id,
      machineId: form.machineId,
      reportedByOperatorId: form.operatorId,
      reportedAt: new Date(form.reportedAt).toISOString(),
      goodQuantity: good,
      scrapQuantity: scrap,
      notes: form.notes || null,
      producedLotId: form.producedLotId,
      consumedLots: consumedPayload
    });
    toast.success(t('toasts.created'));
    modalOpen.value = false;
    // First confirmation moves the order to InProgress — refresh header, list and movements in place.
    await Promise.all([loadOrder(), table.fetch(), loadMovements()]);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

const confirmOpen = ref(false);
const confirmTarget = ref<ProductionConfirmationResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => confirmTarget.value
  ? `${t('common.delete')}: ${formatDateTime(confirmTarget.value.reportedAt)}`
  : '');

const movements = ref<MovementPreviewLine[]>([]);
const movementsLoading = ref(false);
const movementColumns = computed(() => [
  { key: 'movementType', label: t('movements.type'), width: '90px' },
  { key: 'productId', label: t('movements.product') },
  { key: 'quantity', label: t('movements.quantity'), align: 'right' as const },
  { key: 'preferredWarehouseId', label: t('movements.warehouseHint') }
]);

async function loadMovements(): Promise<void> {
  movementsLoading.value = true;
  try {
    movements.value = await productionOrderService.getMovements(orderId);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    movementsLoading.value = false;
  }
}

const movementsModalOpen = ref(false);
const confirmationMovements = ref<MovementPreviewLine[]>([]);
const confirmationMovementsLoading = ref(false);

async function openConfirmationMovements(item: ProductionConfirmationResponse): Promise<void> {
  movementsModalOpen.value = true;
  confirmationMovements.value = [];
  confirmationMovementsLoading.value = true;
  try {
    confirmationMovements.value = await productionConfirmationService.getMovements(item.id);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
    movementsModalOpen.value = false;
  } finally {
    confirmationMovementsLoading.value = false;
  }
}

function closeMovementsModal(): void {
  if (confirmationMovementsLoading.value) return;
  movementsModalOpen.value = false;
}

interface RowAction { key: string; label: string; icon: string; variant?: 'danger' }

function confirmationActions(_item: ProductionConfirmationResponse): RowAction[] {
  const actions: RowAction[] = [
    { key: 'movements', label: t('movements.show'), icon: 'pi-list' }
  ];
  if (canReport.value) {
    actions.push({ key: 'delete', label: t('common.delete'), icon: 'pi-trash', variant: 'danger' });
  }
  return actions;
}

function onRowAction(key: string, item: ProductionConfirmationResponse): void {
  if (key === 'movements') {
    void openConfirmationMovements(item);
  } else if (key === 'delete') {
    confirmTarget.value = item;
    confirmOpen.value = true;
  }
}

async function confirmDelete(): Promise<void> {
  if (!confirmTarget.value) return;
  deleting.value = true;
  try {
    await productionConfirmationService.remove(confirmTarget.value.id);
    toast.success(t('toasts.deleted'));
    confirmOpen.value = false;
    confirmTarget.value = null;
    await Promise.all([table.fetch(), loadMovements()]);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    deleting.value = false;
  }
}

function cancelDelete(): void {
  confirmOpen.value = false;
  confirmTarget.value = null;
}

const lifecycleOpen = ref(false);
const lifecycleKind = ref<'complete' | 'close' | null>(null);
const completing = ref(false);
const closing = ref(false);
const lifecycleTitle = computed(() => lifecycleKind.value === 'close'
  ? t('productionOrders.close')
  : t('productionOrders.complete'));
const lifecycleMessage = computed(() => lifecycleKind.value === 'close'
  ? t('productionOrders.confirmClose')
  : t('productionOrders.confirmComplete'));

function askComplete(): void {
  lifecycleKind.value = 'complete';
  lifecycleOpen.value = true;
}

function askClose(): void {
  lifecycleKind.value = 'close';
  lifecycleOpen.value = true;
}

function cancelLifecycle(): void {
  lifecycleOpen.value = false;
  lifecycleKind.value = null;
}

async function confirmLifecycle(): Promise<void> {
  if (!order.value || !lifecycleKind.value) return;
  const kind = lifecycleKind.value;
  if (kind === 'complete') completing.value = true;
  else closing.value = true;
  try {
    order.value = kind === 'complete'
      ? await productionOrderService.complete(order.value.id)
      : await productionOrderService.close(order.value.id);
    toast.success(kind === 'complete' ? t('productionOrders.completedToast') : t('productionOrders.closedToast'));
    lifecycleOpen.value = false;
    lifecycleKind.value = null;
    await table.fetch();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    completing.value = false;
    closing.value = false;
  }
}

onMounted(() => {
  void loadOrder();
  void loadLookups();
  void table.fetch();
  void loadMovements();
});
</script>

<style scoped>
.order-detail {
  display: flex;
  flex-direction: column;
  gap: var(--space-4);
}
.summary-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: var(--space-3);
}
.summary-item {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
}
.summary-item--full {
  grid-column: 1 / -1;
}
.summary-item__label {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.confirmations-section {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}
.confirmations-section h3 {
  margin: 0;
}
.section-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-3);
}
.section-header h3 {
  margin: 0;
}
.section-subtitle {
  margin: 0;
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.progress {
  height: 8px;
  border-radius: var(--radius-sm);
  background: var(--color-surface-sunken);
  overflow: hidden;
}
.progress__bar {
  height: 100%;
  background: var(--color-primary);
  transition: width var(--transition-normal);
}
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.form-hint {
  margin: 0;
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.form-error {
  margin: var(--space-2) 0 0;
  font-size: var(--font-size-sm);
  color: var(--color-danger, #b91c1c);
}
.consumed-lots { display: flex; flex-direction: column; gap: var(--space-2); }
.consumed-lots__header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-2);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.consumed-lots__row {
  display: grid;
  grid-template-columns: 1fr 160px auto;
  gap: var(--space-2);
  align-items: center;
}
.loading {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-6);
}
</style>
