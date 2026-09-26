<template>
  <div data-testid="lots-page">
    <AppPageHeader :title="$t('lots.title')" :subtitle="$t('lots.subtitle')" icon="pi pi-box">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="table.fetch">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('lots.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppCard class="scan-card" data-testid="lot-scan-card">
      <form class="scan-row" data-testid="lot-scan-form" @submit.prevent="onScan">
        <AppInput
          v-model="scanCode"
          :placeholder="$t('lots.scanPlaceholder')"
          prefix-icon="pi pi-barcode"
          clearable
          data-testid="lot-scan-input"
        />
        <AppButton variant="secondary" icon="pi pi-search" :loading="scanning" @click="onScan">
          {{ $t('lots.scan') }}
        </AppButton>
      </form>
      <p v-if="scanError" class="scan-error">{{ scanError }}</p>
      <p v-else-if="scanned" class="scan-hit">
        {{ $t('lots.scanHit', { code: scanned.code }) }} — {{ statusLabel(scanned.status) }} · {{ scanned.quantity }}
      </p>
    </AppCard>

    <AppFilterBar @clear="clearFilters">
      <AppInput v-model="codeFilter" :placeholder="$t('lots.filters.code')" prefix-icon="pi pi-search" clearable @update:modelValue="onCode" />
      <AppInput v-model="productFilter" :placeholder="$t('lots.filters.product')" prefix-icon="pi pi-search" clearable @update:modelValue="onProduct" />
      <AppSelect
        v-model="statusFilter"
        :options="statusFilterOptions"
        allow-empty
        @change="onStatusChange"
      />
      <AppInput v-model="expiryFromFilter" type="date" @update:modelValue="onExpiryFrom" />
      <AppInput v-model="expiryToFilter" type="date" @update:modelValue="onExpiryTo" />
    </AppFilterBar>

    <AppTable
      :items="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :sort-key="table.sortKey.value"
      :sort-direction="table.sortDirection.value"
      data-testid="lots-table"
      @sort-change="table.setSort"
    >
      <template #cell-code="{ item }">
        <code>{{ item.code }}</code>
      </template>
      <template #cell-status="{ value }">
        <AppBadge :variant="statusVariant(value)" dot>
          {{ statusLabel(value) }}
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

    <AppModal :open="modalOpen" :title="editing ? $t('common.edit') : $t('lots.create')" @close="closeModal">
      <form id="lot-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('lots.code')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.code" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.quantity')" required>
          <template #default="{ id }">
            <AppNumberInput :id="id" v-model="form.quantity" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.productId')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.productId" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.measureUnitId')" required>
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.measureUnitId" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.supplierLotNumber')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.supplierLotNumber" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.producedAt')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.producedAt" type="date" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.expiryDate')">
          <template #default="{ id }">
            <AppInput :id="id" v-model="form.expiryDate" type="date" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('lots.notes')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.notes" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="lot-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppModal :open="detailOpen" size="xl" :title="detailLot ? detailLot.code : $t('common.notFound')" data-testid="lot-detail-modal" @close="closeDetail">
      <div v-if="detailLoading" class="loading"><i class="pi pi-spin pi-spinner" /> {{ $t('common.loading') }}</div>
      <div v-else-if="detailNotFound || !detailLot">
        <AppEmptyState icon="pi pi-exclamation-circle" :title="$t('lots.genealogy.notFound')" />
      </div>
      <div v-else>
        <nav class="tabs">
          <button
            type="button"
            :class="['tab', detailTab === 'details' && 'tab--active']"
            @click="setDetailTab('details')"
          >
            {{ $t('lots.tabs.details') }}
          </button>
          <button
            type="button"
            :class="['tab', detailTab === 'genealogy' && 'tab--active']"
            @click="setDetailTab('genealogy')"
          >
            {{ $t('lots.tabs.genealogy') }}
          </button>
        </nav>

        <div v-if="detailTab === 'details'" class="tab-body">
          <div class="detail-grid">
            <div class="detail-item">
              <span class="detail-item__label">{{ $t('common.status') }}</span>
              <AppBadge :variant="statusVariant(detailLot.status)" dot>
                {{ statusLabel(detailLot.status) }}
              </AppBadge>
            </div>
            <div class="detail-item">
              <span class="detail-item__label">{{ $t('lots.productId') }}</span>
              <span>{{ detailLot.productId }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-item__label">{{ $t('lots.measureUnitId') }}</span>
              <span>{{ detailLot.measureUnitId }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-item__label">{{ $t('lots.quantity') }}</span>
              <span>{{ detailLot.quantity }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-item__label">{{ $t('lots.supplierLotNumber') }}</span>
              <span>{{ detailLot.supplierLotNumber ?? '—' }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-item__label">{{ $t('lots.producedAt') }}</span>
              <span>{{ formatDateTime(detailLot.producedAt) }}</span>
            </div>
            <div class="detail-item">
              <span class="detail-item__label">{{ $t('lots.expiryDate') }}</span>
              <span>{{ formatDateTime(detailLot.expiryDate) }}</span>
            </div>
            <div v-if="detailLot.notes" class="detail-item detail-item--full">
              <span class="detail-item__label">{{ $t('lots.notes') }}</span>
              <span>{{ detailLot.notes }}</span>
            </div>
          </div>
        </div>

        <div v-else class="tab-body" data-testid="lot-genealogy">
          <div class="genealogy-toolbar">
            <AppFormField :label="$t('lots.genealogy.depth')">
              <template #default="{ id }">
                <AppSelect
                  :id="id"
                  :model-value="depth"
                  :options="depthOptions"
                  @change="onDepthChange"
                />
              </template>
            </AppFormField>
            <AppButton variant="secondary" icon="pi pi-refresh" :loading="genealogyLoading" @click="loadGenealogy">
              {{ $t('common.refresh') }}
            </AppButton>
          </div>

          <div class="genealogy-grid">
            <section class="genealogy-pane" data-testid="lot-genealogy-upstream">
              <h4>{{ $t('lots.genealogy.upstream') }}</h4>
              <p v-if="upstream?.truncated" class="truncation-notice">
                <AppBadge variant="warning" dot>{{ $t('lots.genealogy.truncated') }}</AppBadge>
              </p>
              <AppTable
                :items="upstream?.nodes ?? []"
                :columns="genealogyColumns"
                :loading="genealogyLoading"
                row-key="lotId"
                :empty-label="$t('lots.genealogy.empty')"
              >
                <template #cell-consumedQuantity="{ value }">
                  {{ formatQuantity(value) }}
                </template>
                <template #cell-occurredAt="{ value }">
                  {{ formatDateTime(value) }}
                </template>
                <template #cell-actions="{ item }">
                  <AppRowActions
                    :actions="genealogyRowActions()"
                    @action="(k) => onGenealogyRowAction(k, item)"
                  />
                </template>
              </AppTable>
            </section>

            <section class="genealogy-pane" data-testid="lot-genealogy-downstream">
              <h4>{{ $t('lots.genealogy.downstream') }}</h4>
              <p v-if="downstream?.truncated" class="truncation-notice">
                <AppBadge variant="warning" dot>{{ $t('lots.genealogy.truncated') }}</AppBadge>
              </p>
              <AppTable
                :items="downstream?.nodes ?? []"
                :columns="genealogyColumns"
                :loading="genealogyLoading"
                row-key="lotId"
                :empty-label="$t('lots.genealogy.empty')"
              >
                <template #cell-consumedQuantity="{ value }">
                  {{ formatQuantity(value) }}
                </template>
                <template #cell-occurredAt="{ value }">
                  {{ formatDateTime(value) }}
                </template>
                <template #cell-actions="{ item }">
                  <AppRowActions
                    :actions="genealogyRowActions()"
                    @action="(k) => onGenealogyRowAction(k, item)"
                  />
                </template>
              </AppTable>
            </section>
          </div>
        </div>
      </div>
      <template #footer>
        <AppButton variant="ghost" @click="closeDetail">{{ $t('common.close') }}</AppButton>
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
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppFilterBar from '../../components/ui/AppFilterBar.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppPagination from '../../components/ui/AppPagination.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import { useCrudPage } from '../../composables/useCrudPage';
import { lotService, LotStatus, type LotResponse } from '../../services/lotService';
import {
  LotGenealogyDepth,
  lotGenealogyService,
  normalizeGenealogyDepth,
  type LotTraceabilityNode,
  type LotTraceabilityResponse
} from '../../services/lotGenealogyService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t, tm } = useI18n();
const toast = useToastStore();
const route = useRoute();
const router = useRouter();

interface Filters {
  code?: string;
  productId?: string;
  status?: LotStatus;
  expiryFrom?: string;
  expiryTo?: string;
}

const table = useCrudPage<LotResponse, Filters>({
  fetch: (req) => lotService.browse(req),
  initialFilters: {}
});

const columns = computed(() => [
  { key: 'code', label: t('lots.code'), sortable: true },
  { key: 'productId', label: t('lots.productId'), sortable: true },
  { key: 'quantity', label: t('lots.quantity'), sortable: false, align: 'right' as const },
  { key: 'status', label: t('common.status'), sortable: true, width: '140px' },
  { key: 'expiryDate', label: t('lots.expiryDate'), sortable: true },
  { key: 'actions', label: t('common.actions'), width: '120px' }
]);

function statusLabel(v: number): string {
  const map = tm('lots.statuses') as Record<string, string>;
  return map?.[String(v)] ?? String(v);
}

function statusVariant(v: number): 'success' | 'warning' | 'danger' | 'idle' | 'info' {
  switch (v) {
    case LotStatus.Available: return 'success';
    case LotStatus.OnHold: return 'warning';
    case LotStatus.Consumed: return 'info';
    case LotStatus.Scrapped: return 'danger';
    case LotStatus.Expired: return 'idle';
    default: return 'idle';
  }
}

const statusOptions = computed(() => ([LotStatus.Available, LotStatus.OnHold, LotStatus.Consumed, LotStatus.Scrapped, LotStatus.Expired] as LotStatus[])
  .map((v) => ({ value: v, label: statusLabel(v) })));

const statusFilterOptions = computed(() => [
  { value: null, label: t('lots.filters.status') },
  ...statusOptions.value
]);

function rowActions(item: LotResponse): Array<{ key: string; label: string; icon: string; variant?: 'danger' }> {
  const actions: Array<{ key: string; label: string; icon: string; variant?: 'danger' }> = [
    { key: 'details', label: t('lots.actions.details'), icon: 'pi-sitemap' },
    { key: 'edit', label: t('common.edit'), icon: 'pi-pencil' }
  ];
  if (item.status === LotStatus.Available) {
    actions.push({ key: 'hold', label: t('lots.actions.hold'), icon: 'pi-pause' });
  }
  if (item.status === LotStatus.OnHold) {
    actions.push({ key: 'release', label: t('lots.actions.release'), icon: 'pi-play' });
  }
  if (item.status === LotStatus.Available || item.status === LotStatus.OnHold) {
    actions.push({ key: 'scrap', label: t('lots.actions.scrap'), icon: 'pi-trash' });
  }
  actions.push({ key: 'delete', label: t('common.delete'), icon: 'pi-trash', variant: 'danger' });
  return actions;
}

// Scan-by-code lookup
const scanCode = ref('');
const scanning = ref(false);
const scanned = ref<LotResponse | null>(null);
const scanError = ref('');

async function onScan(): Promise<void> {
  const code = scanCode.value.trim();
  scanned.value = null;
  scanError.value = '';
  if (!code) return;
  scanning.value = true;
  try {
    scanned.value = await lotService.getByCode(code);
  } catch (err) {
    scanError.value = extractErrorMessage(err, t('errors.loadFailed'));
  } finally {
    scanning.value = false;
  }
}

// Filters
const codeFilter = ref('');
const productFilter = ref('');
const statusFilter = ref<number | null>(null);
const expiryFromFilter = ref('');
const expiryToFilter = ref('');

let d1 = 0; let d2 = 0;
function onCode(v: string | number | null | undefined): void {
  window.clearTimeout(d1);
  d1 = window.setTimeout(() => table.setFilter('code', v ? String(v) : undefined), 300);
}
function onProduct(v: string | number | null | undefined): void {
  window.clearTimeout(d2);
  d2 = window.setTimeout(() => table.setFilter('productId', v ? String(v) : undefined), 300);
}
function onStatusChange(v: string | number | null): void {
  table.setFilter('status', v === null ? undefined : Number(v) as LotStatus);
}
function onExpiryFrom(v: string | number | null | undefined): void {
  table.setFilter('expiryFrom', v ? String(v) : undefined);
}
function onExpiryTo(v: string | number | null | undefined): void {
  table.setFilter('expiryTo', v ? String(v) : undefined);
}
function clearFilters(): void {
  codeFilter.value = '';
  productFilter.value = '';
  statusFilter.value = null;
  expiryFromFilter.value = '';
  expiryToFilter.value = '';
  table.resetFilters();
}

// Create/edit
const modalOpen = ref(false);
const editing = ref<LotResponse | null>(null);
const saving = ref(false);
const form = reactive({
  code: '',
  productId: '',
  measureUnitId: '',
  quantity: 0 as number | null,
  supplierLotNumber: '' as string | null,
  producedAt: '' as string | null,
  expiryDate: '' as string | null,
  notes: '' as string | null
});

function openCreate(): void {
  editing.value = null;
  Object.assign(form, {
    code: '', productId: '', measureUnitId: '', quantity: 0,
    supplierLotNumber: '', producedAt: '', expiryDate: '', notes: ''
  });
  modalOpen.value = true;
}
function openEdit(item: LotResponse): void {
  editing.value = item;
  Object.assign(form, {
    code: item.code,
    productId: item.productId,
    measureUnitId: item.measureUnitId,
    quantity: item.quantity,
    supplierLotNumber: item.supplierLotNumber ?? '',
    producedAt: item.producedAt ? item.producedAt.substring(0, 10) : '',
    expiryDate: item.expiryDate ? item.expiryDate.substring(0, 10) : '',
    notes: item.notes ?? ''
  });
  modalOpen.value = true;
}
function closeModal(): void {
  if (saving.value) return;
  modalOpen.value = false;
  editing.value = null;
}

function toIsoOrNull(v: string | null): string | null {
  if (!v) return null;
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? null : d.toISOString();
}

async function onSave(): Promise<void> {
  saving.value = true;
  try {
    const payload = {
      code: form.code,
      productId: form.productId,
      measureUnitId: form.measureUnitId,
      quantity: form.quantity ?? 0,
      supplierLotNumber: form.supplierLotNumber || null,
      producedAt: toIsoOrNull(form.producedAt),
      expiryDate: toIsoOrNull(form.expiryDate),
      notes: form.notes || null
    };
    if (editing.value) {
      await lotService.update(editing.value.id, { id: editing.value.id, ...payload });
      toast.success(t('toasts.updated'));
    } else {
      await lotService.create(payload);
      toast.success(t('toasts.created'));
    }
    await table.fetch();
    modalOpen.value = false;
    editing.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

async function changeStatus(item: LotResponse, status: LotStatus): Promise<void> {
  try {
    await lotService.changeStatus(item.id, status);
    toast.success(t('toasts.updated'));
    await table.fetch();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}

// Delete
const confirmOpen = ref(false);
const toDelete = ref<LotResponse | null>(null);
const deleting = ref(false);
const deleteMessage = computed(() => toDelete.value ? `${t('common.delete')}: ${toDelete.value.code}` : '');

function onRowAction(key: string, item: LotResponse): void {
  if (key === 'details') openDetail(item);
  else if (key === 'edit') openEdit(item);
  else if (key === 'hold') void changeStatus(item, LotStatus.OnHold);
  else if (key === 'release') void changeStatus(item, LotStatus.Available);
  else if (key === 'scrap') void changeStatus(item, LotStatus.Scrapped);
  else if (key === 'delete') { toDelete.value = item; confirmOpen.value = true; }
}
async function confirmDelete(): Promise<void> {
  if (!toDelete.value) return;
  deleting.value = true;
  try {
    await lotService.remove(toDelete.value.id);
    toast.success(t('toasts.deleted'));
    await table.fetch();
    confirmOpen.value = false;
    toDelete.value = null;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally {
    deleting.value = false;
  }
}
function cancelDelete(): void {
  confirmOpen.value = false;
  toDelete.value = null;
}

// Lot detail with genealogy tab (deep-linkable via ?lotId=<id>&tab=details|genealogy)
type DetailTab = 'details' | 'genealogy';

const detailOpen = ref(false);
const detailTab = ref<DetailTab>('details');
const detailLot = ref<LotResponse | null>(null);
const detailLoading = ref(false);
const detailNotFound = ref(false);
const depth = ref<number>(LotGenealogyDepth.default);
const upstream = ref<LotTraceabilityResponse | null>(null);
const downstream = ref<LotTraceabilityResponse | null>(null);
const genealogyLoading = ref(false);

const depthOptions = computed(() => {
  const options: Array<{ value: number; label: string }> = [];
  for (let d = LotGenealogyDepth.min; d <= LotGenealogyDepth.max; d++) {
    options.push({ value: d, label: String(d) });
  }
  return options;
});

const genealogyColumns = computed(() => [
  { key: 'depth', label: t('lots.genealogy.level'), width: '70px' },
  { key: 'lotCode', label: t('lots.code') },
  { key: 'consumedQuantity', label: t('lots.genealogy.quantity'), align: 'right' as const },
  { key: 'productionOrderCode', label: t('lots.genealogy.order') },
  { key: 'machineId', label: t('lots.genealogy.workCenter') },
  { key: 'occurredAt', label: t('lots.genealogy.occurredAt') },
  { key: 'actions', label: t('common.actions'), width: '80px' }
]);

function formatDateTime(v: string | null | undefined): string {
  if (!v) return '—';
  const d = new Date(v);
  return Number.isNaN(d.getTime()) ? String(v) : d.toLocaleString();
}

function formatQuantity(v: number): string {
  return new Intl.NumberFormat(undefined, { maximumFractionDigits: 4 }).format(v);
}

function isNotFoundError(err: unknown): boolean {
  if (typeof err !== 'object' || err === null) return false;
  const response = (err as { response?: { status?: unknown } }).response;
  return response?.status === 404;
}

function syncDetailQuery(): void {
  const query = { ...route.query };
  if (detailOpen.value && detailLot.value) {
    query.lotId = detailLot.value.id;
    query.tab = detailTab.value;
  } else {
    delete query.lotId;
    delete query.tab;
  }
  void router.replace({ query });
}

async function loadGenealogy(): Promise<void> {
  if (!detailLot.value) return;
  const lotId = detailLot.value.id;
  const currentDepth = depth.value;
  genealogyLoading.value = true;
  try {
    const [up, down] = await Promise.all([
      lotGenealogyService.getUpstream(lotId, currentDepth),
      lotGenealogyService.getDownstream(lotId, currentDepth)
    ]);
    upstream.value = up;
    downstream.value = down;
  } catch (err) {
    upstream.value = null;
    downstream.value = null;
    if (isNotFoundError(err)) {
      // Cross-tenant or deleted lot: the API hides it behind 404.
      detailNotFound.value = true;
    }
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    genealogyLoading.value = false;
  }
}

function onDepthChange(v: string | number | null): void {
  depth.value = normalizeGenealogyDepth(typeof v === 'number' ? v : Number(v));
  void loadGenealogy();
}

function openDetail(item: LotResponse, tab: DetailTab = 'details'): void {
  detailLot.value = item;
  detailNotFound.value = false;
  detailTab.value = tab;
  detailOpen.value = true;
  upstream.value = null;
  downstream.value = null;
  syncDetailQuery();
  if (tab === 'genealogy') void loadGenealogy();
}

function setDetailTab(tab: DetailTab): void {
  detailTab.value = tab;
  syncDetailQuery();
  if (tab === 'genealogy' && !upstream.value && !genealogyLoading.value) void loadGenealogy();
}

function closeDetail(): void {
  detailOpen.value = false;
  syncDetailQuery();
}

function genealogyRowActions(): Array<{ key: string; label: string; icon: string }> {
  return [{ key: 'open', label: t('lots.genealogy.switchRoot'), icon: 'pi-arrow-right' }];
}

async function onGenealogyRowAction(key: string, node: LotTraceabilityNode): Promise<void> {
  if (key !== 'open') return;
  genealogyLoading.value = true;
  try {
    const lot = await lotService.get(node.lotId);
    detailLot.value = lot;
    detailNotFound.value = false;
    upstream.value = null;
    downstream.value = null;
    syncDetailQuery();
    await loadGenealogy();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    genealogyLoading.value = false;
  }
}

async function initFromQuery(): Promise<void> {
  const lotId = route.query.lotId;
  if (typeof lotId !== 'string' || !lotId) return;
  const tab: DetailTab = route.query.tab === 'genealogy' ? 'genealogy' : 'details';
  detailLoading.value = true;
  try {
    detailLot.value = await lotService.get(lotId);
    detailNotFound.value = false;
    detailTab.value = tab;
    detailOpen.value = true;
    syncDetailQuery();
    if (tab === 'genealogy') await loadGenealogy();
  } catch (err) {
    detailLot.value = null;
    detailNotFound.value = true;
    detailTab.value = tab;
    detailOpen.value = true;
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    detailLoading.value = false;
  }
}

onMounted(() => { void table.fetch(); void initFromQuery(); });
</script>

<style scoped>
.scan-card { margin-bottom: var(--space-3); padding: var(--space-3); }
.scan-row { display: flex; gap: var(--space-2); align-items: center; }
.scan-row > :first-child { flex: 1; }
.scan-error { color: var(--color-danger, #b91c1c); margin: var(--space-2) 0 0; }
.scan-hit { color: var(--color-success, #065f46); margin: var(--space-2) 0 0; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.loading { display: flex; align-items: center; gap: var(--space-2); padding: var(--space-6); }
.tabs { display: flex; border-bottom: 1px solid var(--color-border, #e5e7eb); margin-bottom: var(--space-3); }
.tab { background: none; border: none; padding: 10px 16px; cursor: pointer; font: inherit; color: var(--color-text-muted, #6b7280); border-bottom: 2px solid transparent; }
.tab--active { color: var(--color-primary, #2563eb); border-bottom-color: var(--color-primary, #2563eb); }
.tab-body { padding-top: var(--space-2); }
.detail-grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); gap: var(--space-3); }
.detail-item { display: flex; flex-direction: column; gap: var(--space-1); }
.detail-item--full { grid-column: 1 / -1; }
.detail-item__label { font-size: var(--font-size-sm); color: var(--color-text-muted); }
.genealogy-toolbar { display: flex; gap: var(--space-3); align-items: flex-end; margin-bottom: var(--space-3); }
.genealogy-toolbar > :first-child { width: 160px; }
.genealogy-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-4); }
.genealogy-pane h4 { margin: 0 0 var(--space-2); }
.truncation-notice { margin: 0 0 var(--space-2); }
@media (max-width: 900px) {
  .genealogy-grid { grid-template-columns: 1fr; }
}
</style>
