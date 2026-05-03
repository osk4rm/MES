<template>
  <div>
    <AppPageHeader :title="order?.orderNumber ?? $t('customerOrders.detailTitle')" :subtitle="order?.customerNameSnapshot ?? ''" icon="pi pi-shopping-cart">
      <template #actions>
        <AppButton variant="ghost" icon="pi pi-arrow-left" @click="router.push({ name: 'customer-orders' })">{{ $t('common.back') }}</AppButton>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="load">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openLineModal">{{ $t('customerOrders.addLine') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppSpinner v-if="loading" />
    <AppEmptyState v-else-if="!order" icon="pi pi-search" :title="$t('common.notFound')" />
    <template v-else>
      <div class="summary-grid">
        <AppCard><strong>{{ $t('common.status') }}</strong><br><AppBadge :variant="orderStatusVariant(order.status)">{{ orderStatusLabel(order.status) }}</AppBadge></AppCard>
        <AppCard><strong>{{ $t('customerOrders.customer') }}</strong><br>{{ order.customerNameSnapshot }}<br><small>{{ order.customerTaxIdSnapshot }}</small></AppCard>
        <AppCard><strong>{{ $t('customerOrders.requestedDeliveryDate') }}</strong><br>{{ fmt(order.requestedDeliveryDate) }}</AppCard>
        <AppCard><strong>{{ $t('customerOrders.releaseProgress') }}</strong><br>{{ releaseProgress(order) }}</AppCard>
      </div>

      <AppCard class="lines-card">
        <div class="section-head"><h2>{{ $t('customerOrders.lines') }}</h2></div>
        <AppTable :items="order.lines" :columns="lineColumns">
          <template #cell-product="{ item }"><strong>{{ item.productCode }}</strong><br><small>{{ item.productName }}</small></template>
          <template #cell-quantity="{ item }">{{ item.releasedQuantity }} / {{ item.orderedQuantity }} {{ item.measureUnitCode ?? '' }}</template>
          <template #cell-status="{ item }"><AppBadge :variant="lineStatusVariant(item.status)">{{ lineStatusLabel(item.status) }}</AppBadge></template>
          <template #cell-actions="{ item }"><AppRowActions :actions="[{ key: 'release', label: $t('customerOrders.releaseToProduction'), icon: 'pi-send', disabled: item.remainingQuantity <= 0 }]" @action="(k) => { if (k === 'release') openReleaseModal(item) }" /></template>
        </AppTable>
      </AppCard>

      <AppCard class="lines-card">
        <div class="section-head"><h2>{{ $t('customerOrders.productionReleases') }}</h2></div>
        <AppTable :items="productionReleases" :columns="releaseColumns">
          <template #cell-line="{ item }">{{ item.line.productCode }} — {{ item.line.productName }}</template>
          <template #cell-quantity="{ item }">{{ item.release.quantity }}</template>
          <template #cell-status="{ item }"><AppBadge variant="info">{{ $t(`customerOrders.productionReleaseStatus.${item.release.status}`) }}</AppBadge></template>
        </AppTable>
      </AppCard>
    </template>

    <AppModal :open="lineModalOpen" :title="$t('customerOrders.addLine')" @close="closeLineModal">
      <form id="line-form" class="form-grid" @submit.prevent="saveLine">
        <AppFormField :label="$t('customerOrders.lineNumber')" required><template #default="{ id }"><AppNumberInput :id="id" v-model="lineForm.lineNumber" :min="1" required /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.product')" required><template #default="{ id }"><AppAutocomplete :id="id" v-model="lineForm.productId" :options="productOptions" :placeholder="$t('customerOrders.productPlaceholder')" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.quantity')" required><template #default="{ id }"><AppNumberInput :id="id" v-model="lineForm.orderedQuantity" :min="0.000001" step="0.000001" required /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.requestedDeliveryDate')"><template #default="{ id }"><AppInput :id="id" v-model="lineForm.requestedDeliveryDate" type="date" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.unitNetPrice')"><template #default="{ id }"><AppNumberInput :id="id" v-model="lineForm.unitNetPrice" :min="0" step="0.01" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.notes')"><template #default="{ id }"><AppInput :id="id" v-model="lineForm.notes" /></template></AppFormField>
      </form>
      <template #footer><AppButton variant="ghost" :disabled="saving" @click="closeLineModal">{{ $t('common.cancel') }}</AppButton><AppButton type="submit" form="line-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton></template>
    </AppModal>

    <AppModal :open="releaseModalOpen" :title="$t('customerOrders.releaseToProduction')" @close="closeReleaseModal">
      <form id="release-form" class="form-grid" @submit.prevent="saveRelease">
        <AppFormField :label="$t('customerOrders.recipe')" required class="form-grid__full"><template #default="{ id }"><AppAutocomplete :id="id" v-model="releaseForm.recipeId" :options="recipeOptions" :placeholder="$t('customerOrders.recipePlaceholder')" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.quantity')" required><template #default="{ id }"><AppNumberInput :id="id" v-model="releaseForm.quantity" :min="0.000001" :max="selectedLine?.remainingQuantity" step="0.000001" required /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.plannedDueDate')"><template #default="{ id }"><AppInput :id="id" v-model="releaseForm.plannedDueDate" type="date" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.plannedStartDate')"><template #default="{ id }"><AppInput :id="id" v-model="releaseForm.plannedStartDate" type="date" /></template></AppFormField>
        <AppFormField :label="$t('customerOrders.notes')"><template #default="{ id }"><AppInput :id="id" v-model="releaseForm.notes" /></template></AppFormField>
      </form>
      <template #footer><AppButton variant="ghost" :disabled="saving" @click="closeReleaseModal">{{ $t('common.cancel') }}</AppButton><AppButton type="submit" form="release-form" variant="primary" :loading="saving">{{ $t('customerOrders.releaseToProduction') }}</AppButton></template>
    </AppModal>
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import { useRoute, useRouter } from 'vue-router';
import AppAutocomplete, { type AutocompleteOption } from '../../components/ui/AppAutocomplete.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppNumberInput from '../../components/ui/AppNumberInput.vue';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import AppTable from '../../components/ui/AppTable.vue';
import { customerOrderService, CustomerOrderLineStatus, CustomerOrderStatus, type CustomerOrderLineResponse, type CustomerOrderResponse } from '../../services/customerOrderService';
import { extractErrorMessage } from '../../services/http';
import { productService } from '../../services/productService';
import { recipeService, RecipeVersionStatus } from '../../services/recipeService';
import { useToastStore } from '../../stores/toastStore';

const route = useRoute(); const router = useRouter(); const { t } = useI18n(); const toast = useToastStore();
const loading = ref(false); const saving = ref(false); const order = ref<CustomerOrderResponse | null>(null);
const productOptions = ref<AutocompleteOption[]>([]); const recipeOptions = ref<AutocompleteOption[]>([]); const selectedLine = ref<CustomerOrderLineResponse | null>(null);
const lineColumns = computed(() => [{ key: 'lineNumber', label: t('customerOrders.lineNumber') }, { key: 'product', label: t('customerOrders.product') }, { key: 'quantity', label: t('customerOrders.quantity') }, { key: 'requestedDeliveryDate', label: t('customerOrders.requestedDeliveryDate') }, { key: 'status', label: t('common.status') }, { key: 'actions', label: t('common.actions'), width: '110px' }]);
const releaseColumns = computed(() => [{ key: 'line', label: t('customerOrders.line') }, { key: 'quantity', label: t('customerOrders.quantity') }, { key: 'plannedDueDate', label: t('customerOrders.plannedDueDate') }, { key: 'status', label: t('common.status') }]);
const productionReleases = computed(() => order.value?.lines.flatMap(line => line.productionReleases.map(release => ({ line, release, plannedDueDate: release.plannedDueDate }))) ?? []);
async function load() { loading.value = true; try { order.value = await customerOrderService.get(String(route.params.id)); } catch (err) { toast.error(extractErrorMessage(err, t('errors.loadFailed'))); } finally { loading.value = false; } }
async function loadProducts() { const result = await productService.browse({ pageSize: 500, isActive: true }); productOptions.value = result.items.map(p => ({ value: p.id, label: `${p.code} — ${p.name}` })); }
function fmt(value?: string | null) { return value ? value.slice(0, 10) : '—'; }
function releaseProgress(item: CustomerOrderResponse) { const qty = item.lines.reduce((s, l) => s + l.orderedQuantity, 0); const rel = item.lines.reduce((s, l) => s + l.releasedQuantity, 0); return qty ? `${rel}/${qty}` : '—'; }
function orderStatusLabel(status: CustomerOrderStatus) { return t(`customerOrders.status.${status}`); }
function lineStatusLabel(status: CustomerOrderLineStatus) { return t(`customerOrders.lineStatus.${status}`); }
function orderStatusVariant(status: CustomerOrderStatus) { return status === CustomerOrderStatus.Completed ? 'success' : status === CustomerOrderStatus.Cancelled ? 'danger' : 'info'; }
function lineStatusVariant(status: CustomerOrderLineStatus) { return status === CustomerOrderLineStatus.Completed ? 'success' : status === CustomerOrderLineStatus.Cancelled ? 'danger' : status === CustomerOrderLineStatus.Open ? 'idle' : 'info'; }
const lineModalOpen = ref(false); const lineForm = reactive({ lineNumber: 1 as number | null, productId: null as string | null, orderedQuantity: 1 as number | null, requestedDeliveryDate: '', unitNetPrice: null as number | null, notes: '' });
function openLineModal() { lineForm.lineNumber = (order.value?.lines.length ?? 0) + 1; lineForm.productId = null; lineForm.orderedQuantity = 1; lineForm.requestedDeliveryDate = order.value?.requestedDeliveryDate?.slice(0, 10) ?? ''; lineForm.unitNetPrice = null; lineForm.notes = ''; lineModalOpen.value = true; }
function closeLineModal() { if (!saving.value) lineModalOpen.value = false; }
async function saveLine() { if (!order.value || !lineForm.productId || !lineForm.lineNumber || !lineForm.orderedQuantity) return; saving.value = true; try { order.value = await customerOrderService.addLine(order.value.id, { customerOrderId: order.value.id, lineNumber: lineForm.lineNumber, productId: lineForm.productId, orderedQuantity: lineForm.orderedQuantity, requestedDeliveryDate: lineForm.requestedDeliveryDate || null, unitNetPrice: lineForm.unitNetPrice, lineNetAmount: null, notes: lineForm.notes || null }); toast.success(t('toasts.created')); closeLineModal(); } catch (err) { toast.error(extractErrorMessage(err, t('errors.saveFailed'))); } finally { saving.value = false; } }
const releaseModalOpen = ref(false); const releaseForm = reactive({ recipeId: null as string | null, quantity: 1 as number | null, plannedDueDate: '', plannedStartDate: '', notes: '' });
async function openReleaseModal(line: CustomerOrderLineResponse) { selectedLine.value = line; releaseForm.recipeId = null; releaseForm.quantity = line.remainingQuantity; releaseForm.plannedDueDate = line.requestedDeliveryDate?.slice(0, 10) ?? order.value?.requestedDeliveryDate?.slice(0, 10) ?? ''; releaseForm.plannedStartDate = ''; releaseForm.notes = ''; const result = await recipeService.browse({ pageSize: 500, isActive: true, primaryProductId: line.productId }); recipeOptions.value = result.items.filter(r => r.currentVersionId && r.versions?.some(v => v.id === r.currentVersionId && v.status === RecipeVersionStatus.Released)).map(r => ({ value: r.id, label: `${r.code} — ${r.name}` })); releaseModalOpen.value = true; }
function closeReleaseModal() { if (!saving.value) releaseModalOpen.value = false; }
async function saveRelease() { if (!selectedLine.value || !releaseForm.recipeId || !releaseForm.quantity) return; saving.value = true; try { order.value = await customerOrderService.createProductionRelease(selectedLine.value.id, { customerOrderLineId: selectedLine.value.id, recipeId: releaseForm.recipeId, quantity: releaseForm.quantity, plannedDueDate: releaseForm.plannedDueDate || null, plannedStartDate: releaseForm.plannedStartDate || null, notes: releaseForm.notes || null }); toast.success(t('customerOrders.releaseCreated')); closeReleaseModal(); } catch (err) { toast.error(extractErrorMessage(err, t('errors.saveFailed'))); } finally { saving.value = false; } }
onMounted(async () => { await Promise.all([load(), loadProducts()]); });
</script>

<style scoped>
.summary-grid { display: grid; grid-template-columns: repeat(4, minmax(0, 1fr)); gap: var(--space-3); margin-bottom: var(--space-4); }
.lines-card { margin-top: var(--space-4); }
.section-head { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-3); }
.section-head h2 { margin: 0; font-size: var(--font-size-lg); }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
@media (max-width: 900px) { .summary-grid { grid-template-columns: 1fr; } }
</style>
