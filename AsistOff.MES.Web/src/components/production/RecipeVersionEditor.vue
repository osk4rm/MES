<template>
  <div class="rv-editor">
    <div class="rv-editor__header">
      <div class="rv-editor__title">
        <h2>v{{ version.versionNumber }}</h2>
        <span :class="['pill', statusPillClass]">{{ $t(`recipes.versionStatus.${statusKey}`) }}</span>
      </div>
      <div class="rv-editor__actions">
        <AppButton v-if="isDraft" variant="primary" icon="pi pi-check" :loading="releasing" @click="releaseVersion">
          {{ $t('recipes.detail.release') }}
        </AppButton>
        <AppButton v-if="isDraft" variant="danger" icon="pi pi-trash" :disabled="!canDeleteVersion" @click="onDeleteVersion">
          {{ $t('recipes.detail.deleteVersion') }}
        </AppButton>
      </div>
    </div>

    <div class="rv-editor__layout">
      <aside class="rv-editor__sidebar">
        <div class="rv-editor__sidebar-header">
          <strong>{{ $t('recipes.detail.operations') }}</strong>
          <AppButton v-if="isDraft" size="sm" variant="secondary" icon="pi pi-plus" @click="openAddOperation">
            {{ $t('common.add') }}
          </AppButton>
        </div>
        <ul class="op-list">
          <li
            v-for="op in sortedOperations"
            :key="op.id"
            :class="['op-list__item', selectedOperationId === op.id && 'op-list__item--active']"
            @click="selectedOperationId = op.id"
          >
            <span class="op-list__idx">{{ op.sortIndex + 1 }}</span>
            <span class="op-list__info">
              <span class="op-list__code">{{ op.code }}</span>
              <span class="op-list__name">{{ op.name }}</span>
            </span>
          </li>
          <li v-if="sortedOperations.length === 0" class="op-list__empty">{{ $t('recipes.detail.noOperations') }}</li>
        </ul>
      </aside>

      <section class="rv-editor__main">
        <div v-if="!selectedOperation" class="placeholder">{{ $t('recipes.detail.selectOperation') }}</div>

        <div v-else class="op-panel">
          <header class="op-panel__header">
            <h3>{{ selectedOperation.code }} — {{ selectedOperation.name }}</h3>
            <div class="op-panel__actions">
              <AppButton v-if="isDraft" size="sm" variant="secondary" icon="pi pi-pencil" @click="openEditOperation">
                {{ $t('common.edit') }}
              </AppButton>
              <AppButton v-if="isDraft" size="sm" variant="danger" icon="pi pi-trash" @click="deleteOperation">
                {{ $t('common.delete') }}
              </AppButton>
            </div>
          </header>

          <nav class="tabs">
            <button
              v-for="tab in tabs"
              :key="tab.key"
              type="button"
              :class="['tab', activeTab === tab.key && 'tab--active']"
              @click="activeTab = tab.key"
            >
              {{ $t(`recipes.detail.tabs.${tab.key}`) }}
            </button>
          </nav>

          <div class="tab-body">
            <!-- dependencies -->
            <div v-if="activeTab === 'dependencies'">
              <p class="muted">{{ $t('recipes.detail.dependenciesHelp') }}</p>
              <ul class="items">
                <li v-for="(dep, idx) in selectedOperation.dependencies" :key="idx" class="item">
                  <span class="item__label">{{ predecessorName(dep.predecessorOperationId) }}</span>
                  <span class="muted">{{ $t(`recipes.dependencyType.${depTypeKey(dep.dependencyType)}`) }}</span>
                  <AppButton v-if="isDraft" size="sm" variant="ghost" icon="pi pi-times" @click="removeDependency(idx)" />
                </li>
                <li v-if="selectedOperation.dependencies.length === 0" class="muted small">{{ $t('common.empty') }}</li>
              </ul>
              <div v-if="isDraft" class="add-row">
                <select v-model="newDep.predecessorOperationId">
                  <option value="">{{ $t('recipes.detail.predecessor') }}</option>
                  <option v-for="o in otherOperations" :key="o.id" :value="o.id">{{ o.code }} — {{ o.name }}</option>
                </select>
                <select v-model.number="newDep.dependencyType">
                  <option :value="1">{{ $t('recipes.dependencyType.finishToStart') }}</option>
                  <option :value="2">{{ $t('recipes.dependencyType.startToStart') }}</option>
                  <option :value="3">{{ $t('recipes.dependencyType.finishToFinish') }}</option>
                  <option :value="4">{{ $t('recipes.dependencyType.startToFinish') }}</option>
                </select>
                <AppButton size="sm" variant="primary" icon="pi pi-plus" :disabled="!newDep.predecessorOperationId" @click="addDependency">
                  {{ $t('common.add') }}
                </AppButton>
              </div>
            </div>

            <!-- BOM -->
            <div v-else-if="activeTab === 'bom'">
              <table class="grid">
                <thead><tr>
                  <th>{{ $t('recipes.detail.product') }}</th>
                  <th>{{ $t('recipes.detail.quantity') }}</th>
                  <th>{{ $t('recipes.detail.quantityType') }}</th>
                  <th v-if="isDraft"></th>
                </tr></thead>
                <tbody>
                  <tr v-for="item in selectedOperation.bomItems" :key="item.id">
                    <td>{{ productLabel(item.productId) }}</td>
                    <td>{{ item.quantity }}</td>
                    <td>{{ $t(`recipes.quantityType.${qtyTypeKey(item.quantityType)}`) }}</td>
                    <td v-if="isDraft">
                      <AppButton size="sm" variant="ghost" icon="pi pi-times" @click="removeBomItem(item.id)" />
                    </td>
                  </tr>
                  <tr v-if="selectedOperation.bomItems.length === 0"><td colspan="4" class="muted">{{ $t('common.empty') }}</td></tr>
                </tbody>
              </table>
              <div v-if="isDraft" class="add-row wrap">
                <AppAutocomplete v-model="newBom.productId" :options="productOptions" :placeholder="$t('recipes.detail.product')" />
                <AppInput v-model.number="newBom.quantity" type="number" :placeholder="$t('recipes.detail.quantity')" />
                <select v-model.number="newBom.quantityType">
                  <option :value="1">{{ $t('recipes.quantityType.perUnit') }}</option>
                  <option :value="2">{{ $t('recipes.quantityType.perBatch') }}</option>
                  <option :value="3">{{ $t('recipes.quantityType.fixed') }}</option>
                </select>
                <AppButton size="sm" variant="primary" icon="pi pi-plus" :disabled="!newBom.productId || !newBom.quantity" @click="addBomItem">
                  {{ $t('common.add') }}
                </AppButton>
              </div>
            </div>

            <!-- Outputs -->
            <div v-else-if="activeTab === 'outputs'">
              <table class="grid">
                <thead><tr>
                  <th>{{ $t('recipes.detail.product') }}</th>
                  <th>{{ $t('recipes.detail.quantity') }}</th>
                  <th>{{ $t('recipes.detail.outputType') }}</th>
                  <th v-if="isDraft"></th>
                </tr></thead>
                <tbody>
                  <tr v-for="o in selectedOperation.outputs" :key="o.id">
                    <td>{{ productLabel(o.productId) }}</td>
                    <td>{{ o.quantity }}</td>
                    <td>{{ $t(`recipes.outputType.${outputTypeKey(o.outputType)}`) }}</td>
                    <td v-if="isDraft"><AppButton size="sm" variant="ghost" icon="pi pi-times" @click="removeOutput(o.id)" /></td>
                  </tr>
                  <tr v-if="selectedOperation.outputs.length === 0"><td colspan="4" class="muted">{{ $t('common.empty') }}</td></tr>
                </tbody>
              </table>
              <div v-if="isDraft" class="add-row wrap">
                <AppAutocomplete v-model="newOutput.productId" :options="productOptions" :placeholder="$t('recipes.detail.product')" />
                <AppInput v-model.number="newOutput.quantity" type="number" :placeholder="$t('recipes.detail.quantity')" />
                <select v-model.number="newOutput.outputType">
                  <option :value="1">{{ $t('recipes.outputType.product') }}</option>
                  <option :value="2">{{ $t('recipes.outputType.byProduct') }}</option>
                  <option :value="3">{{ $t('recipes.outputType.waste') }}</option>
                  <option :value="4">{{ $t('recipes.outputType.sample') }}</option>
                </select>
                <AppButton size="sm" variant="primary" icon="pi pi-plus" :disabled="!newOutput.productId || !newOutput.quantity" @click="addOutput">
                  {{ $t('common.add') }}
                </AppButton>
              </div>
            </div>

            <!-- Resources -->
            <div v-else-if="activeTab === 'resources'">
              <table class="grid">
                <thead><tr>
                  <th>{{ $t('recipes.detail.capability') }}</th>
                  <th>{{ $t('recipes.detail.operatorCount') }}</th>
                  <th>{{ $t('recipes.detail.role') }}</th>
                  <th v-if="isDraft"></th>
                </tr></thead>
                <tbody>
                  <tr v-for="r in selectedOperation.resourceRequirements" :key="r.id">
                    <td>{{ r.requiredCapability || '—' }}</td>
                    <td>{{ r.requiredOperatorCount }}</td>
                    <td>{{ r.requiredRole || '—' }}</td>
                    <td v-if="isDraft"><AppButton size="sm" variant="ghost" icon="pi pi-times" @click="removeResource(r.id)" /></td>
                  </tr>
                  <tr v-if="selectedOperation.resourceRequirements.length === 0"><td colspan="4" class="muted">{{ $t('common.empty') }}</td></tr>
                </tbody>
              </table>
              <div v-if="isDraft" class="add-row wrap">
                <AppAutocomplete v-model="newResource.selectedSkillId" :options="skillOptions" :placeholder="$t('recipes.detail.capability')" />
                <AppInput v-model.number="newResource.requiredOperatorCount" type="number" :placeholder="$t('recipes.detail.operatorCount')" />
                <AppInput v-model="newResource.requiredRole" :placeholder="$t('recipes.detail.role')" />
                <AppButton size="sm" variant="primary" icon="pi pi-plus" @click="addResource">
                  {{ $t('common.add') }}
                </AppButton>
              </div>
            </div>

            <!-- Attachments -->
            <div v-else-if="activeTab === 'attachments'">
              <AttachmentsPanel owner-type="operation" :owner-id="selectedOperation.id" />
            </div>
          </div>
        </div>
      </section>
    </div>

    <!-- Add / Edit operation modal -->
    <AppModal :open="opModalOpen" :title="editingOperation ? $t('common.edit') : $t('recipes.detail.addOperation')" @close="opModalOpen = false">
      <form id="op-form" class="form-grid" @submit.prevent="saveOperation">
        <AppFormField v-if="!editingOperation && templateOptions.length > 0" :label="$t('recipes.detail.fromTemplate')" class="form-grid__full">
          <template #default="{ id }">
            <AppAutocomplete :id="id" v-model="selectedTemplateId" :options="templateOptions" :placeholder="$t('recipes.detail.fromTemplatePlaceholder')" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('recipes.detail.opCode')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="opForm.code" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('recipes.detail.opName')" required>
          <template #default="{ id, invalid }"><AppInput :id="id" v-model="opForm.name" required :invalid="invalid" /></template>
        </AppFormField>
        <AppFormField :label="$t('recipes.detail.opDescription')" class="form-grid__full">
          <template #default="{ id }"><AppInput :id="id" v-model="opForm.description" /></template>
        </AppFormField>
        <AppFormField :label="$t('recipes.detail.setupTimeMinutes')">
          <template #default="{ id }"><AppInput :id="id" v-model.number="opForm.setupTimeMinutes" type="number" /></template>
        </AppFormField>
        <AppFormField :label="$t('recipes.detail.runTimePerUnitSeconds')">
          <template #default="{ id }"><AppInput :id="id" v-model.number="opForm.runTimePerUnitSeconds" type="number" /></template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="savingOp" @click="opModalOpen = false">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="op-form" variant="primary" :loading="savingOp">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>
  </div>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch, onMounted } from 'vue';
import { useI18n } from 'vue-i18n';
import AppButton from '../ui/AppButton.vue';
import AppInput from '../ui/AppInput.vue';
import AppModal from '../ui/AppModal.vue';
import AppFormField from '../ui/AppFormField.vue';
import AppAutocomplete, { type AutocompleteOption } from '../ui/AppAutocomplete.vue';
import AttachmentsPanel from './AttachmentsPanel.vue';
import {
  recipeVersionService,
  type RecipeVersionDetailResponse,
  type OperationNodeDto,
  type DependencyEntry,
  BomQuantityType,
  OperationDependencyType,
  OperationOutputType,
  RunTimeMode
} from '../../services/recipeVersionService';
import { RecipeVersionStatus } from '../../services/recipeService';
import { productService, type ProductResponse } from '../../services/productService';
import { skillService, type SkillResponse } from '../../services/skillService';
import { operationTemplateService, type OperationTemplateResponse } from '../../services/operationTemplateService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const props = defineProps<{
  version: RecipeVersionDetailResponse;
  recipeId: string;
}>();
const emit = defineEmits<{
  (e: 'refresh'): void;
  (e: 'version-deleted', deletedVersionId: string): void;
}>();

const { t } = useI18n();
const toast = useToastStore();

const releasing = ref(false);

// ─── lookup data ──────────────────────────────────────────────────────────────
const products = ref<ProductResponse[]>([]);
const skills = ref<SkillResponse[]>([]);
const operationTemplates = ref<OperationTemplateResponse[]>([]);

const productOptions = computed<AutocompleteOption[]>(() =>
  products.value.map(p => ({ value: p.id, label: `${p.code} — ${p.name}` })));

const skillOptions = computed<AutocompleteOption[]>(() =>
  skills.value.map(s => ({ value: s.id, label: `${s.code} — ${s.name}` })));

const templateOptions = computed<AutocompleteOption[]>(() =>
  operationTemplates.value.map(t => ({ value: t.id, label: `${t.code} — ${t.name}` })));

function productLabel(id: string): string {
  const p = products.value.find(x => x.id === id);
  return p ? `${p.code} — ${p.name}` : id;
}

async function loadLookups() {
  try {
    const [prods, skls, tpls] = await Promise.all([
      productService.browse({ pageSize: 500 }),
      skillService.browse({ pageSize: 500 }),
      operationTemplateService.browse({ pageSize: 500, isActive: true })
    ]);
    products.value = prods.items;
    skills.value = skls.items;
    operationTemplates.value = tpls.items;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  }
}

onMounted(loadLookups);

type TabKey = 'dependencies' | 'bom' | 'outputs' | 'resources' | 'attachments';
const tabs: { key: TabKey }[] = [
  { key: 'dependencies' }, { key: 'bom' }, { key: 'outputs' }, { key: 'resources' }, { key: 'attachments' }
];
const activeTab = ref<TabKey>('dependencies');

const selectedOperationId = ref<string | null>(null);

const sortedOperations = computed(() =>
  [...props.version.operations].sort((a, b) => a.sortIndex - b.sortIndex));

const selectedOperation = computed<OperationNodeDto | null>(() =>
  sortedOperations.value.find(o => o.id === selectedOperationId.value) ?? null);

const otherOperations = computed(() =>
  sortedOperations.value.filter(o => o.id !== selectedOperationId.value));

const isDraft = computed(() => props.version.status === RecipeVersionStatus.Draft);
const statusKey = computed(() => {
  if (props.version.status === RecipeVersionStatus.Draft) return 'draft';
  if (props.version.status === RecipeVersionStatus.Released) return 'released';
  return 'obsolete';
});
const statusPillClass = computed(() => {
  if (props.version.status === RecipeVersionStatus.Released) return 'pill--ok';
  if (props.version.status === RecipeVersionStatus.Draft) return 'pill--info';
  return 'pill--muted';
});
const canDeleteVersion = computed(() => props.version.status === RecipeVersionStatus.Draft);

watch(() => props.version.id, () => {
  selectedOperationId.value = sortedOperations.value[0]?.id ?? null;
}, { immediate: true });

function predecessorName(id: string): string {
  const o = sortedOperations.value.find(x => x.id === id);
  return o ? `${o.code} — ${o.name}` : id;
}
function depTypeKey(t: OperationDependencyType): string {
  return ({ 1: 'finishToStart', 2: 'startToStart', 3: 'finishToFinish', 4: 'startToFinish' } as const)[t];
}
function qtyTypeKey(t: BomQuantityType): string {
  return ({ 1: 'perUnit', 2: 'perBatch', 3: 'fixed' } as const)[t];
}
function outputTypeKey(t: OperationOutputType): string {
  return ({ 1: 'product', 2: 'byProduct', 3: 'waste', 4: 'sample' } as const)[t];
}

// operation add/edit
const opModalOpen = ref(false);
const editingOperation = ref<OperationNodeDto | null>(null);
const savingOp = ref(false);
const selectedTemplateId = ref<string | null>(null);
const opForm = reactive({
  code: '', name: '', description: '' as string | null,
  setupTimeMinutes: null as number | null,
  runTimePerUnitSeconds: null as number | null
});

function openAddOperation() {
  editingOperation.value = null;
  selectedTemplateId.value = null;
  Object.assign(opForm, { code: '', name: '', description: '', setupTimeMinutes: null, runTimePerUnitSeconds: null });
  opModalOpen.value = true;
}

watch(selectedTemplateId, (id) => {
  if (!id || editingOperation.value) return;
  const tpl = operationTemplates.value.find(t => t.id === id);
  if (tpl) {
    Object.assign(opForm, {
      code: tpl.code,
      name: tpl.name,
      description: tpl.description ?? '',
      setupTimeMinutes: tpl.setupTimeMinutes,
      runTimePerUnitSeconds: tpl.runTimePerUnitSeconds
    });
  }
});
function openEditOperation() {
  if (!selectedOperation.value) return;
  editingOperation.value = selectedOperation.value;
  Object.assign(opForm, {
    code: selectedOperation.value.code,
    name: selectedOperation.value.name,
    description: selectedOperation.value.description ?? '',
    setupTimeMinutes: selectedOperation.value.setupTimeMinutes,
    runTimePerUnitSeconds: selectedOperation.value.runTimePerUnitSeconds
  });
  opModalOpen.value = true;
}

async function saveOperation() {
  savingOp.value = true;
  try {
    if (editingOperation.value) {
      await recipeVersionService.updateOperation(editingOperation.value.id, {
        operationId: editingOperation.value.id,
        code: opForm.code,
        name: opForm.name,
        description: opForm.description || null,
        operationType: editingOperation.value.operationType ?? null,
        sortIndex: editingOperation.value.sortIndex,
        setupTimeMinutes: opForm.setupTimeMinutes,
        runTimeMode: editingOperation.value.runTimeMode,
        runTimePerUnitSeconds: opForm.runTimePerUnitSeconds,
        runTimePerBatchMinutes: editingOperation.value.runTimePerBatchMinutes,
        teardownTimeMinutes: editingOperation.value.teardownTimeMinutes,
        queueTimeMinutes: editingOperation.value.queueTimeMinutes,
        isOptional: editingOperation.value.isOptional,
        allowParallelExecution: editingOperation.value.allowParallelExecution,
        expectedQuantity: editingOperation.value.expectedQuantity
      });
      toast.success(t('toasts.updated'));
    } else {
      await recipeVersionService.addOperation({
        versionId: props.version.id,
        code: opForm.code,
        name: opForm.name,
        description: opForm.description || null,
        runTimeMode: RunTimeMode.PerUnitSeconds,
        setupTimeMinutes: opForm.setupTimeMinutes,
        runTimePerUnitSeconds: opForm.runTimePerUnitSeconds,
        isOptional: false,
        allowParallelExecution: false
      });
      toast.success(t('toasts.created'));
    }
    opModalOpen.value = false;
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    savingOp.value = false;
  }
}

async function deleteOperation() {
  if (!selectedOperation.value) return;
  if (!confirm(t('recipes.detail.confirmDeleteOperation'))) return;
  try {
    await recipeVersionService.deleteOperation(selectedOperation.value.id);
    toast.success(t('toasts.deleted'));
    selectedOperationId.value = null;
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  }
}

// release
async function releaseVersion() {
  releasing.value = true;
  try {
    await recipeVersionService.release(props.version.id);
    toast.success(t('recipes.detail.releasedToast'));
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    releasing.value = false;
  }
}

async function onDeleteVersion() {
  if (!confirm(t('recipes.detail.confirmDeleteVersion'))) return;
  try {
    const deletedId = props.version.id;
    await recipeVersionService.remove(deletedId);
    toast.success(t('toasts.deleted'));
    // The parent view decides where to navigate next (previous version, last
    // existing, or — when no versions remain — opens a delete/deactivate prompt
    // for the entire recipe).
    emit('version-deleted', deletedId);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  }
}

// dependencies
const newDep = reactive<DependencyEntry>({
  predecessorOperationId: '',
  dependencyType: OperationDependencyType.FinishToStart,
  lagMinutes: null
});
async function addDependency() {
  if (!selectedOperation.value || !newDep.predecessorOperationId) return;
  const updated: DependencyEntry[] = [...selectedOperation.value.dependencies, { ...newDep }];
  await sendDependencies(updated);
  newDep.predecessorOperationId = '';
}
async function removeDependency(idx: number) {
  if (!selectedOperation.value) return;
  const updated = selectedOperation.value.dependencies.filter((_, i) => i !== idx);
  await sendDependencies(updated);
}
async function sendDependencies(deps: DependencyEntry[]) {
  if (!selectedOperation.value) return;
  try {
    await recipeVersionService.setDependencies(selectedOperation.value.id, deps);
    toast.success(t('toasts.updated'));
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}

// bom items
const newBom = reactive({
  productId: '', quantity: 1, quantityType: BomQuantityType.PerUnit
});
async function addBomItem() {
  if (!selectedOperation.value || !newBom.productId) return;
  try {
    await recipeVersionService.addBomItem(selectedOperation.value.id, {
      operationId: selectedOperation.value.id,
      productId: newBom.productId,
      measureUnitId: null,
      quantity: newBom.quantity,
      quantityType: newBom.quantityType,
      scrapPercentage: null,
      isOptional: false,
      preferredWarehouseId: null,
      consumptionTiming: 2,
      notes: null,
      sortIndex: selectedOperation.value.bomItems.length
    });
    toast.success(t('toasts.created'));
    newBom.productId = ''; newBom.quantity = 1;
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}
async function removeBomItem(id: string) {
  try {
    await recipeVersionService.removeBomItem(id);
    toast.success(t('toasts.deleted'));
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  }
}

// outputs
const newOutput = reactive({
  productId: '', quantity: 1, outputType: OperationOutputType.Product
});
async function addOutput() {
  if (!selectedOperation.value || !newOutput.productId) return;
  try {
    await recipeVersionService.addOutput(selectedOperation.value.id, {
      operationId: selectedOperation.value.id,
      productId: newOutput.productId,
      measureUnitId: null,
      quantity: newOutput.quantity,
      quantityType: BomQuantityType.PerUnit,
      outputType: newOutput.outputType,
      preferredWarehouseId: null,
      notes: null,
      sortIndex: selectedOperation.value.outputs.length
    });
    toast.success(t('toasts.created'));
    newOutput.productId = ''; newOutput.quantity = 1;
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}
async function removeOutput(id: string) {
  try {
    await recipeVersionService.removeOutput(id);
    toast.success(t('toasts.deleted'));
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  }
}

// resources
const newResource = reactive({
  selectedSkillId: null as string | null,
  requiredOperatorCount: 1,
  requiredRole: '' as string | null
});
async function addResource() {
  if (!selectedOperation.value) return;
  try {
    const skill = newResource.selectedSkillId ? skills.value.find(s => s.id === newResource.selectedSkillId) : null;
    await recipeVersionService.addResource(selectedOperation.value.id, {
      operationId: selectedOperation.value.id,
      preferredDepartmentId: null,
      preferredMachineId: null,
      requiredCapability: skill ? `${skill.code} — ${skill.name}` : null,
      requiredOperatorCount: newResource.requiredOperatorCount,
      requiredRole: newResource.requiredRole || null,
      notes: null
    });
    toast.success(t('toasts.created'));
    newResource.selectedSkillId = null; newResource.requiredOperatorCount = 1; newResource.requiredRole = '';
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}
async function removeResource(id: string) {
  try {
    await recipeVersionService.removeResource(id);
    toast.success(t('toasts.deleted'));
    emit('refresh');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  }
}
</script>

<style scoped>
.rv-editor { border: 1px solid var(--color-border, #e5e7eb); border-radius: var(--radius-lg, 8px); background: var(--color-surface, #fff); }
.rv-editor__header { display: flex; justify-content: space-between; align-items: center; padding: var(--space-3) var(--space-4); border-bottom: 1px solid var(--color-border, #e5e7eb); }
.rv-editor__title { display: flex; align-items: center; gap: var(--space-2); }
.rv-editor__title h2 { margin: 0; font-size: 1.25rem; }
.rv-editor__actions { display: flex; gap: var(--space-2); }
.rv-editor__layout { display: grid; grid-template-columns: 260px 1fr; min-height: 500px; }
.rv-editor__sidebar { border-right: 1px solid var(--color-border, #e5e7eb); padding: var(--space-3); }
.rv-editor__sidebar-header { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-2); }
.op-list { list-style: none; padding: 0; margin: 0; display: flex; flex-direction: column; gap: 4px; }
.op-list__item { display: flex; align-items: center; gap: 8px; padding: 8px 10px; border-radius: 6px; cursor: pointer; }
.op-list__item:hover { background: var(--color-hover, #f3f4f6); }
.op-list__item--active { background: var(--color-primary-soft, #dbeafe); }
.op-list__idx { display: inline-flex; align-items: center; justify-content: center; width: 24px; height: 24px; background: var(--color-primary, #2563eb); color: white; border-radius: 50%; font-size: 12px; font-weight: 600; flex-shrink: 0; }
.op-list__info { display: flex; flex-direction: column; min-width: 0; }
.op-list__code { font-weight: 600; font-size: 0.85rem; }
.op-list__name { font-size: 0.8rem; color: var(--color-text-muted, #6b7280); overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.op-list__empty { color: var(--color-text-muted, #6b7280); padding: var(--space-3); text-align: center; font-size: 0.9rem; }
.rv-editor__main { padding: var(--space-4); }
.placeholder { color: var(--color-text-muted, #6b7280); text-align: center; padding: var(--space-6); }
.op-panel__header { display: flex; justify-content: space-between; align-items: center; margin-bottom: var(--space-3); }
.op-panel__header h3 { margin: 0; }
.op-panel__actions { display: flex; gap: var(--space-2); }
.tabs { display: flex; border-bottom: 1px solid var(--color-border, #e5e7eb); margin-bottom: var(--space-3); }
.tab { background: none; border: none; padding: 10px 16px; cursor: pointer; font: inherit; color: var(--color-text-muted, #6b7280); border-bottom: 2px solid transparent; }
.tab--active { color: var(--color-primary, #2563eb); border-bottom-color: var(--color-primary, #2563eb); }
.tab-body { padding-top: var(--space-2); }
.items { list-style: none; padding: 0; margin: 0 0 var(--space-3) 0; display: flex; flex-direction: column; gap: 6px; }
.item { display: flex; align-items: center; gap: var(--space-2); padding: 6px 10px; background: var(--color-hover, #f9fafb); border-radius: 4px; }
.item__label { font-weight: 500; flex: 1; }
.muted { color: var(--color-text-muted, #6b7280); }
.small { font-size: 0.9rem; }
.add-row { display: flex; gap: 8px; align-items: center; margin-top: var(--space-2); }
.add-row.wrap { flex-wrap: wrap; }
.add-row select, .add-row input { padding: 6px 10px; border: 1px solid var(--color-border, #e5e7eb); border-radius: 4px; }
.grid { width: 100%; border-collapse: collapse; }
.grid th, .grid td { padding: 8px 10px; text-align: left; border-bottom: 1px solid var(--color-border, #e5e7eb); }
.grid th { font-weight: 600; font-size: 0.85rem; color: var(--color-text-muted, #6b7280); }
.mono { font-family: ui-monospace, monospace; font-size: 0.85rem; }
.pill { display: inline-block; padding: 2px 10px; border-radius: 12px; font-size: 12px; }
.pill--ok { background: #d1fae5; color: #065f46; }
.pill--info { background: #dbeafe; color: #1e40af; }
.pill--muted { background: #e5e7eb; color: #374151; }
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
</style>
