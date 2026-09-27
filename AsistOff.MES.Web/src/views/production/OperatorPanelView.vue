<template>
  <div data-testid="operator-panel" :class="viewClass">
    <AppPageHeader :title="$t('operatorPanel.title')" :subtitle="$t('operatorPanel.subtitle')" icon="pi pi-tablet">
      <template #actions>
        <AppButton variant="ghost" @click="toggleDensity">{{ $t('shopfloor.density.label') }}: {{ densityLabel }}</AppButton>
        <AppButton variant="secondary" icon="pi pi-refresh" :loading="loading" @click="refresh">
          {{ $t('common.refresh') }}
        </AppButton>
      </template>
    </AppPageHeader>

    <AppFilterBar @clear="clearOperator">
      <AppFormField :label="$t('operatorPanel.operatorCode')" required :error="codeError">
        <template #default="{ id, invalid }">
          <AppInput
            :id="id"
            v-model="operatorInput"
            :placeholder="$t('operatorPanel.operatorCodePlaceholder')"
            :invalid="invalid"
            autocomplete="off"
            @enter="applyOperator"
          />
        </template>
      </AppFormField>
      <template #actions>
        <AppButton variant="primary" icon="pi pi-check" :loading="loading" @click="applyOperator">
          {{ $t('operatorPanel.load') }}
        </AppButton>
      </template>
    </AppFilterBar>

    <AppErrorState v-if="loadError" :message="loadError" :loading="loading" @retry="refresh" />
    <AppSpinner v-else-if="loading && !loadedOnce" />
    <AppEmptyState
      v-else-if="!queue"
      icon="pi pi-id-card"
      :title="$t('operatorPanel.enterCode')"
    />

    <template v-else>
      <AppCard :title="shiftTitle" class="panel-shift" data-testid="operator-shift">
        <div v-if="queue.shift" class="panel-shift__grid">
          <div class="panel-shift__item">
            <span class="panel-shift__label">{{ $t('operatorPanel.shiftCode') }}</span>
            <AppBadge variant="primary" icon="pi pi-clock" dot>{{ queue.shift.shiftCode }}</AppBadge>
          </div>
          <div class="panel-shift__item">
            <span class="panel-shift__label">{{ $t('operatorPanel.shiftName') }}</span>
            <span>{{ queue.shift.shiftName }}</span>
          </div>
          <div class="panel-shift__item">
            <span class="panel-shift__label">{{ $t('operatorPanel.shiftWindow') }}</span>
            <span>{{ formatTime(queue.shift.windowStartUtc) }}–{{ formatTime(queue.shift.windowEndUtc) }}</span>
          </div>
          <div v-if="queue.shift.isOvernight" class="panel-shift__item">
            <AppBadge variant="info" icon="pi pi-moon" dot>{{ $t('operatorPanel.overnight') }}</AppBadge>
          </div>
        </div>
        <AppEmptyState v-else icon="pi pi-clock" :title="$t('operatorPanel.offShift')" />
      </AppCard>

      <AppCard :title="$t('andon.board')" class="panel-signals" data-testid="operator-signals">
        <AppEmptyState
          v-if="signalGroups.length === 0"
          icon="pi pi-check-circle"
          :title="$t('andon.boardEmpty')"
        />
        <div v-else class="panel-grid">
          <div
            v-for="group in signalGroups"
            :key="group.machineId"
            class="panel-card panel-card--signal"
            :data-testid="`signal-${group.machineId}`"
          >
            <div class="panel-card__header">
              <span class="panel-card__machine">
                <i :class="['panel-card__severity', groupMeta(group).icon]" aria-hidden="true"></i>
                <strong>{{ machineTitle(group.machineId, group.machineCode) }}</strong>
              </span>
              <AppBadge :variant="groupMeta(group).variant" :icon="groupMeta(group).icon" dot>
                {{ $t('operatorPanel.openSignals', { n: group.signals.length }) }}
              </AppBadge>
            </div>
            <ul class="panel-signal-list">
              <li v-for="signal in group.signals" :key="signal.id" class="panel-signal">
                <AppBadge :variant="signalMeta(signal.category).variant" :icon="signalMeta(signal.category).icon" dot>
                  {{ categoryLabel(signal.category) }}
                </AppBadge>
                <span class="panel-signal__severity">{{ signal.severity }}</span>
                <span class="panel-signal__time">{{ formatDateTime(signal.raisedAt) }}</span>
                <AppButton
                  variant="secondary"
                  icon="pi pi-eye"
                  :loading="acknowledging === signal.id"
                  @click="onAcknowledge(signal.id)"
                >
                  {{ $t('andon.acknowledge') }}
                </AppButton>
              </li>
            </ul>
          </div>
        </div>
      </AppCard>

      <section class="panel-queue" data-testid="operator-queue">
        <h3 class="panel-queue__title">{{ $t('operatorPanel.queueTitle') }}</h3>
        <AppEmptyState
          v-if="queue.orders.length === 0"
          icon="pi pi-list"
          :title="$t('operatorPanel.queueEmpty')"
        />
        <div v-else class="panel-grid">
          <div
            v-for="(order, index) in queue.orders"
            :key="order.id"
            class="panel-card"
            :class="{ 'panel-card--nextup': index === 0 }"
            :data-testid="index === 0 ? 'queue-nextup' : `queue-order-${order.id}`"
          >
            <div class="panel-card__header">
              <AppBadge v-if="index === 0" variant="success" icon="pi pi-arrow-up" dot>
                {{ $t('operatorPanel.nextUp') }}
              </AppBadge>
              <AppBadge :variant="statusVariant(order.status)" :icon="statusIcon(order.status)" dot>
                {{ statusLabel(order.status) }}
              </AppBadge>
              <span class="panel-card__priority">{{ $t('operatorPanel.priority', { n: order.priority }) }}</span>
            </div>
            <div class="panel-card__code">{{ order.code }}</div>
            <div class="panel-card__meta">
              <span>{{ $t('operatorPanel.product', { code: order.productCode ?? '—' }) }}</span>
              <span>{{ $t('operatorPanel.remaining', { remaining: formatQuantity(order.remainingQuantity), planned: formatQuantity(order.plannedQuantity) }) }}</span>
              <span>{{ $t('operatorPanel.workCenter', { name: machineTitle(order.machineId, order.machineCode ?? order.machineName) }) }}</span>
              <span>{{ formatDueDate(order.dueDate) }}</span>
            </div>
            <div class="panel-card__actions">
              <AppButton variant="primary" icon="pi pi-play" :loading="claiming === order.id" @click="openConfirm(order)">
                {{ claimLabel(order.status) }}
              </AppButton>
              <AppButton variant="secondary" icon="pi pi-trash" @click="openScrap(order)">
                {{ $t('operatorPanel.reportScrap') }}
              </AppButton>
              <AppButton variant="secondary" icon="pi pi-pause-circle" @click="openDowntime(order)">
                {{ $t('operatorPanel.reportDowntime') }}
              </AppButton>
              <AppButton variant="ghost" icon="pi pi-arrow-right" @click="openOrder(order.id)">
                {{ $t('common.open') }}
              </AppButton>
            </div>
          </div>
        </div>
      </section>
    </template>

    <AppModal :open="confirmOpen" :title="$t('productionConfirmations.report')" data-testid="panel-confirm-modal" @close="closeConfirm">
      <form id="panel-confirm-form" class="form-grid" novalidate @submit.prevent="onConfirmSave">
        <AppFormField :label="$t('productionConfirmations.machine')" required :error="confirmErrors.machineId">
          <template #default="{ id, invalid }">
            <AppSelect
              :id="id"
              v-model="confirmForm.machineId"
              :options="machineOptions"
              :placeholder="$t('productionConfirmations.selectMachine')"
              :invalid="invalid"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.reportedAt')" required :error="confirmErrors.reportedAt">
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="confirmForm.reportedAt" type="datetime-local" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.goodQuantity')" required :error="confirmErrors.quantities">
          <template #default="{ id, invalid }">
            <AppNumberInput :id="id" v-model="confirmForm.goodQuantity" :min="0" :step="1" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.scrapQuantity')" required :error="confirmErrors.quantities">
          <template #default="{ id, invalid }">
            <AppNumberInput :id="id" v-model="confirmForm.scrapQuantity" :min="0" :step="1" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('productionConfirmations.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="confirmForm.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="confirmSaving" @click="closeConfirm">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="panel-confirm-form" variant="primary" :loading="confirmSaving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppModal :open="scrapOpen" :title="$t('scrap.report')" data-testid="panel-scrap-modal" @close="closeScrap">
      <form id="panel-scrap-form" class="form-grid" novalidate @submit.prevent="onScrapSave">
        <AppFormField :label="$t('scrap.machine')" required :error="scrapErrors.machineId">
          <template #default="{ id, invalid }">
            <AppSelect
              :id="id"
              v-model="scrapForm.machineId"
              :options="machineOptions"
              :placeholder="$t('scrap.selectMachine')"
              :invalid="invalid"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('scrap.reasonCode')" required :error="scrapErrors.reasonCodeId">
          <template #default="{ id, invalid }">
            <AppSelect
              :id="id"
              v-model="scrapForm.reasonCodeId"
              :options="scrapReasonOptions"
              :placeholder="$t('scrap.selectReason')"
              :invalid="invalid"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('scrap.quantity')" required :error="scrapErrors.quantity">
          <template #default="{ id, invalid }">
            <AppNumberInput :id="id" v-model="scrapForm.quantity" :min="0" :step="1" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('scrap.reportedAt')" required :error="scrapErrors.reportedAt">
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="scrapForm.reportedAt" type="datetime-local" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('scrap.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="scrapForm.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="scrapSaving" @click="closeScrap">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="panel-scrap-form" variant="primary" :loading="scrapSaving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppModal :open="downtimeOpen" :title="$t('downtime.start')" data-testid="panel-downtime-modal" @close="closeDowntime">
      <form id="panel-downtime-form" class="form-grid" novalidate @submit.prevent="onDowntimeSave">
        <AppFormField :label="$t('downtime.machine')" required :error="downtimeErrors.machineId">
          <template #default="{ id, invalid }">
            <AppSelect
              :id="id"
              v-model="downtimeForm.machineId"
              :options="machineOptions"
              :placeholder="$t('downtime.selectMachine')"
              :invalid="invalid"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('downtime.reasonCode')" required :error="downtimeErrors.reasonCodeId">
          <template #default="{ id, invalid }">
            <AppSelect
              :id="id"
              v-model="downtimeForm.reasonCodeId"
              :options="downtimeReasonOptions"
              :placeholder="$t('downtime.selectReason')"
              :invalid="invalid"
            />
          </template>
        </AppFormField>
        <AppFormField :label="$t('downtime.startedAt')" required :error="downtimeErrors.startedAt">
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="downtimeForm.startedAt" type="datetime-local" :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('downtime.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="downtimeForm.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="downtimeSaving" @click="closeDowntime">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="panel-downtime-form" variant="primary" :loading="downtimeSaving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRouter } from 'vue-router';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import AppErrorState from '../../components/ui/AppErrorState.vue';
import {
  groupSignalsByMachine,
  operatorQueueService,
  operatorQueueSignalMeta,
  type MachineSignalGroup,
  type OperatorShiftQueue,
  type OperatorShiftQueuedOrder
} from '../../services/operatorQueueService';
import { ProductionOrderStatus } from '../../services/productionOrderService';
import { productionConfirmationService } from '../../services/productionConfirmationService';
import { andonSignalService, AndonSignalCategory } from '../../services/andonSignalService';
import { downtimeEventService } from '../../services/downtimeEventService';
import { scrapEventService } from '../../services/scrapEventService';
import { machineService, type MachineResponse } from '../../services/machineService';
import { operatorService } from '../../services/operatorService';
import { reasonCodeService, ReasonCodeCategory, type ReasonCodeResponse } from '../../services/reasonCodeService';
import {
  productionOrderStatusMeta,
  ShopfloorDensity,
  useShopfloorDensity,
  type StatusSignalMeta
} from '../../composables/useShopfloorDisplay';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();
const router = useRouter();
const { density, viewClass, toggleDensity } = useShopfloorDensity();
const densityLabel = computed(() => t(density.value === ShopfloorDensity.Compact
  ? 'shopfloor.density.compact'
  : 'shopfloor.density.comfortable'));

const operatorInput = ref('');
const appliedCode = ref('');
const codeTouched = ref(false);
const codeError = computed((): string | null => {
  if (!codeTouched.value) return null;
  return operatorInput.value.trim() ? null : t('validation.required');
});

const queue = ref<OperatorShiftQueue | null>(null);
const loading = ref(false);
const loadedOnce = ref(false);
const loadError = ref<string | null>(null);
const claiming = ref<string | null>(null);
const acknowledging = ref<string | null>(null);
const resolvedOperatorId = ref<string | null>(null);

const machines = ref<MachineResponse[]>([]);
const reasonCodes = ref<ReasonCodeResponse[]>([]);

const shiftTitle = computed(() => queue.value?.shift
  ? `${queue.value.shift.shiftCode} — ${queue.value.shift.shiftName}`
  : t('operatorPanel.shiftTitle'));

const signalGroups = computed<MachineSignalGroup[]>(() => groupSignalsByMachine(queue.value?.activeSignals ?? []));

const machineById = computed(() => new Map(machines.value.map((m) => [m.id, m])));
const machineOptions = computed(() => machines.value.map((m) => ({
  value: m.id as string | number | null,
  label: `${m.code} — ${m.name}`
})));

function reasonOptions(category: ReasonCodeCategory): Array<{ value: string | number | null; label: string }> {
  return reasonCodes.value
    .filter((r) => r.category === category && r.isActive)
    .sort((a, b) => a.sortIndex - b.sortIndex || a.code.localeCompare(b.code))
    .map((r) => ({ value: r.id as string | number | null, label: `${r.code} — ${r.name}` }));
}
const scrapReasonOptions = computed(() => reasonOptions(ReasonCodeCategory.Scrap));
const downtimeReasonOptions = computed(() => reasonOptions(ReasonCodeCategory.Downtime));

function machineTitle(id: string | null, fallback: string | null): string {
  if (!id) return fallback ?? '—';
  const m = machineById.value.get(id);
  if (m) return `${m.code} — ${m.name}`;
  return fallback ?? id;
}

function categoryLabel(v: number): string {
  const map = tm('andon.categories') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

function signalMeta(category: number): StatusSignalMeta {
  return operatorQueueSignalMeta(category);
}

function groupMeta(group: MachineSignalGroup): StatusSignalMeta {
  const first = group.signals[0];
  return operatorQueueSignalMeta(first ? first.category : AndonSignalCategory.Other);
}

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

function statusVariant(v: number): StatusSignalMeta['variant'] {
  return productionOrderStatusMeta(v).variant;
}

function statusIcon(v: number): string {
  return productionOrderStatusMeta(v).icon;
}

/**
 * Claim advances a Released order to InProgress via its first
 * Confirmation; an InProgress order just records another Confirmation.
 */
function claimLabel(status: number): string {
  return status === ProductionOrderStatus.InProgress
    ? t('productionConfirmations.report')
    : t('operatorPanel.claim');
}

function formatQuantity(v: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(v);
}

function formatDueDate(value: string | null): string {
  if (!value) return t('scheduleDispatch.noDueDate');
  const d = new Date(value);
  if (Number.isNaN(d.getTime())) return value;
  return `${t('scheduleDispatch.dueDate')}: ${d.toLocaleDateString()}`;
}

function formatDateTime(v: string): string {
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? v : d.toLocaleString();
}

function formatTime(v: string): string {
  const d = new Date(v);
  if (Number.isNaN(d.getTime())) return v;
  return d.toLocaleTimeString(undefined, { hour: '2-digit', minute: '2-digit' });
}

function toLocalInputValue(d: Date): string {
  const pad = (n: number): string => String(n).padStart(2, '0');
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function applyOperator(): void {
  codeTouched.value = true;
  const code = operatorInput.value.trim();
  if (!code) return;
  appliedCode.value = code;
  void loadQueue();
}

function clearOperator(): void {
  operatorInput.value = '';
  appliedCode.value = '';
  resolvedOperatorId.value = null;
  queue.value = null;
  loadError.value = null;
  codeTouched.value = false;
}

async function loadQueue(): Promise<void> {
  if (!appliedCode.value) return;
  loading.value = true;
  loadError.value = null;
  try {
    queue.value = await operatorQueueService.getQueue(appliedCode.value);
    loadedOnce.value = true;
    await resolveOperatorId(appliedCode.value);
  } catch (err) {
    loadError.value = extractErrorMessage(err, t('errors.loadFailed'));
  } finally {
    loading.value = false;
  }
}

async function resolveOperatorId(code: string): Promise<void> {
  resolvedOperatorId.value = null;
  try {
    const page = await operatorService.browse({ identifier: code, pageSize: 5 });
    // Fail safe to null: only an exact identifier match resolves. Falling
    // back to the first page item would attribute confirmations, scrap and
    // downtime to the wrong operator.
    const match = page.items.find((o) => o.identifier === code);
    resolvedOperatorId.value = match ? match.id : null;
  } catch {
    resolvedOperatorId.value = null;
  }
}

async function refresh(): Promise<void> {
  if (!appliedCode.value) {
    await loadLookups();
    return;
  }
  await Promise.all([loadQueue(), loadLookups()]);
}

async function loadLookups(): Promise<void> {
  try {
    const [m, scrapReasons, downtimeReasons] = await Promise.all([
      machineService.browse({ pageNumber: 1, pageSize: 100 }),
      reasonCodeService.browse({ category: ReasonCodeCategory.Scrap, isActive: true, pageNumber: 1, pageSize: 100 }),
      reasonCodeService.browse({ category: ReasonCodeCategory.Downtime, isActive: true, pageNumber: 1, pageSize: 100 })
    ]);
    machines.value = m.items;
    reasonCodes.value = [...scrapReasons.items, ...downtimeReasons.items];
  } catch {
    /* ignore — queue still renders; dialogs surface lookup errors on save */
  }
}

function openOrder(id: string): void {
  void router.push({ name: 'production-order-detail', params: { id } });
}

async function onAcknowledge(signalId: string): Promise<void> {
  acknowledging.value = signalId;
  try {
    await andonSignalService.acknowledge(signalId);
    toast.success(t('toasts.updated'));
    await loadQueue();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    acknowledging.value = null;
  }
}

// Claim / Confirm dialog: persists a Confirmation via the existing
// endpoint. The first Confirmation on a Released order moves it to
// InProgress, which is the claim transition — no new mutation needed.
const confirmOpen = ref(false);
const confirmSaving = ref(false);
const confirmTarget = ref<OperatorShiftQueuedOrder | null>(null);
const confirmForm = reactive({
  machineId: null as string | number | null,
  reportedAt: '',
  goodQuantity: null as number | null,
  scrapQuantity: null as number | null,
  notes: ''
});
const confirmErrors = reactive({
  machineId: null as string | null,
  reportedAt: null as string | null,
  quantities: null as string | null
});

function openConfirm(order: OperatorShiftQueuedOrder): void {
  confirmTarget.value = order;
  Object.assign(confirmForm, {
    machineId: order.machineId ?? machines.value[0]?.id ?? null,
    reportedAt: toLocalInputValue(new Date()),
    goodQuantity: null,
    scrapQuantity: null,
    notes: ''
  });
  Object.assign(confirmErrors, { machineId: null, reportedAt: null, quantities: null });
  confirmOpen.value = true;
}

function closeConfirm(): void {
  if (confirmSaving.value) return;
  confirmOpen.value = false;
  confirmTarget.value = null;
}

async function onConfirmSave(): Promise<void> {
  if (!confirmTarget.value) return;
  const good = confirmForm.goodQuantity ?? 0;
  const scrap = confirmForm.scrapQuantity ?? 0;
  confirmErrors.machineId = confirmForm.machineId ? null : t('validation.required');
  confirmErrors.reportedAt = !confirmForm.reportedAt
    ? t('validation.required')
    : Number.isNaN(new Date(String(confirmForm.reportedAt)).getTime()) ? t('validation.invalidDate') : null;
  confirmErrors.quantities = (good > 0 || scrap > 0) ? null : t('productionConfirmations.positiveQuantityRequired');
  if (confirmErrors.machineId || confirmErrors.reportedAt || confirmErrors.quantities) return;
  confirmSaving.value = true;
  claiming.value = confirmTarget.value.id;
  try {
    await productionConfirmationService.create({
      productionOrderId: confirmTarget.value.id,
      machineId: String(confirmForm.machineId),
      reportedByOperatorId: resolvedOperatorId.value,
      reportedAt: new Date(String(confirmForm.reportedAt)).toISOString(),
      goodQuantity: good,
      scrapQuantity: scrap,
      notes: confirmForm.notes || null,
      producedLotId: null,
      consumedLots: null
    });
    toast.success(t('toasts.created'));
    confirmOpen.value = false;
    confirmTarget.value = null;
    await loadQueue();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    confirmSaving.value = false;
    claiming.value = null;
  }
}

// Scrap dialog: persists a scrap event with a Reason code.
const scrapOpen = ref(false);
const scrapSaving = ref(false);
const scrapTarget = ref<OperatorShiftQueuedOrder | null>(null);
const scrapForm = reactive({
  machineId: null as string | number | null,
  reasonCodeId: null as string | number | null,
  quantity: null as number | null,
  reportedAt: '',
  notes: ''
});
const scrapErrors = reactive({
  machineId: null as string | null,
  reasonCodeId: null as string | null,
  quantity: null as string | null,
  reportedAt: null as string | null
});

function openScrap(order: OperatorShiftQueuedOrder): void {
  scrapTarget.value = order;
  Object.assign(scrapForm, {
    machineId: order.machineId ?? machines.value[0]?.id ?? null,
    reasonCodeId: null,
    quantity: null,
    reportedAt: toLocalInputValue(new Date()),
    notes: ''
  });
  Object.assign(scrapErrors, { machineId: null, reasonCodeId: null, quantity: null, reportedAt: null });
  scrapOpen.value = true;
}

function closeScrap(): void {
  if (scrapSaving.value) return;
  scrapOpen.value = false;
  scrapTarget.value = null;
}

async function onScrapSave(): Promise<void> {
  if (!scrapTarget.value) return;
  const qty = scrapForm.quantity ?? 0;
  scrapErrors.machineId = scrapForm.machineId ? null : t('validation.required');
  scrapErrors.reasonCodeId = scrapForm.reasonCodeId ? null : t('validation.required');
  scrapErrors.quantity = qty > 0 ? null : t('validation.mustBePositive');
  scrapErrors.reportedAt = !scrapForm.reportedAt
    ? t('validation.required')
    : Number.isNaN(new Date(String(scrapForm.reportedAt)).getTime()) ? t('validation.invalidDate') : null;
  if (scrapErrors.machineId || scrapErrors.reasonCodeId || scrapErrors.quantity || scrapErrors.reportedAt) return;
  scrapSaving.value = true;
  try {
    await scrapEventService.create({
      machineId: String(scrapForm.machineId),
      reasonCodeId: String(scrapForm.reasonCodeId),
      quantity: qty,
      reportedAt: new Date(String(scrapForm.reportedAt)).toISOString(),
      notes: scrapForm.notes || null,
      reportedByOperatorId: resolvedOperatorId.value,
      productionOrderId: scrapTarget.value.id
    });
    toast.success(t('toasts.created'));
    scrapOpen.value = false;
    scrapTarget.value = null;
    await loadQueue();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    scrapSaving.value = false;
  }
}

// Downtime dialog: opens a downtime event with a Reason code.
const downtimeOpen = ref(false);
const downtimeSaving = ref(false);
const downtimeTarget = ref<OperatorShiftQueuedOrder | null>(null);
const downtimeForm = reactive({
  machineId: null as string | number | null,
  reasonCodeId: null as string | number | null,
  startedAt: '',
  notes: ''
});
const downtimeErrors = reactive({
  machineId: null as string | null,
  reasonCodeId: null as string | null,
  startedAt: null as string | null
});

function openDowntime(order: OperatorShiftQueuedOrder): void {
  downtimeTarget.value = order;
  Object.assign(downtimeForm, {
    machineId: order.machineId ?? machines.value[0]?.id ?? null,
    reasonCodeId: null,
    startedAt: toLocalInputValue(new Date()),
    notes: ''
  });
  Object.assign(downtimeErrors, { machineId: null, reasonCodeId: null, startedAt: null });
  downtimeOpen.value = true;
}

function closeDowntime(): void {
  if (downtimeSaving.value) return;
  downtimeOpen.value = false;
  downtimeTarget.value = null;
}

async function onDowntimeSave(): Promise<void> {
  if (!downtimeTarget.value) return;
  downtimeErrors.machineId = downtimeForm.machineId ? null : t('validation.required');
  downtimeErrors.reasonCodeId = downtimeForm.reasonCodeId ? null : t('validation.required');
  downtimeErrors.startedAt = !downtimeForm.startedAt
    ? t('validation.required')
    : Number.isNaN(new Date(String(downtimeForm.startedAt)).getTime()) ? t('validation.invalidDate') : null;
  if (downtimeErrors.machineId || downtimeErrors.reasonCodeId || downtimeErrors.startedAt) return;
  downtimeSaving.value = true;
  try {
    await downtimeEventService.start({
      machineId: String(downtimeForm.machineId),
      reasonCodeId: String(downtimeForm.reasonCodeId),
      startedAt: new Date(String(downtimeForm.startedAt)).toISOString(),
      notes: downtimeForm.notes || null,
      reportedByOperatorId: resolvedOperatorId.value,
      productionOrderId: downtimeTarget.value.id
    });
    toast.success(t('toasts.created'));
    downtimeOpen.value = false;
    downtimeTarget.value = null;
    await loadQueue();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    downtimeSaving.value = false;
  }
}

onMounted(() => {
  void loadLookups();
});
</script>

<style scoped>
.panel-shift {
  margin-bottom: var(--space-3);
}
.panel-shift__grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  gap: var(--space-3);
}
.panel-shift__item {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  min-width: 0;
}
.panel-shift__label {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.panel-signals {
  margin-bottom: var(--space-3);
}
.panel-queue {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  margin-bottom: var(--space-3);
}
.panel-queue__title {
  margin: 0;
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
}
.panel-grid {
  display: grid;
  grid-template-columns: repeat(auto-fill, minmax(340px, 1fr));
  gap: var(--space-3);
}
.panel-card {
  border: 1px solid var(--color-border);
  border-radius: var(--radius-md);
  padding: var(--space-3);
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
  background: var(--color-surface);
  min-width: 0;
}
.panel-card--nextup {
  border-left-width: 4px;
  border-left-color: var(--color-success);
}
.panel-card--signal {
  border-left-width: 4px;
  border-left-color: var(--color-danger);
}
.panel-card__header {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}
.panel-card__priority {
  margin-left: auto;
  color: var(--color-text-muted);
  font-size: var(--font-size-sm);
}
.panel-card__code {
  font-size: var(--font-size-lg);
  font-weight: var(--font-weight-semibold);
  overflow-wrap: anywhere;
}
.panel-card__meta {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  font-size: var(--font-size-md);
  min-width: 0;
}
.panel-card__meta > span {
  overflow-wrap: anywhere;
}
.panel-card__machine {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
  font-size: var(--font-size-md);
  min-width: 0;
}
.panel-card__severity {
  font-size: 22px;
  flex: none;
  color: var(--color-danger);
}
.panel-card__actions {
  display: flex;
  gap: var(--space-2);
  flex-wrap: wrap;
  margin-top: var(--space-1);
}
.panel-card__actions > * {
  flex: 1 1 160px;
  min-width: 0;
}
.panel-signal-list {
  list-style: none;
  margin: 0;
  padding: 0;
  display: flex;
  flex-direction: column;
  gap: var(--space-2);
}
.panel-signal {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  flex-wrap: wrap;
}
.panel-signal__severity {
  font-weight: var(--font-weight-medium);
}
.panel-signal__time {
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
/* Tablet viewport (1024x768): single column, full-width primary actions,
   no overlapping controls and no horizontal scrolling of actions. */
@media (max-width: 1100px) {
  .panel-grid { grid-template-columns: 1fr; }
  .form-grid { grid-template-columns: 1fr; }
  .panel-card__actions > * { flex: 1 1 100%; }
  .panel-signal { row-gap: var(--space-2); }
}
</style>
