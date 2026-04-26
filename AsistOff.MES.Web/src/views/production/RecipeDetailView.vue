<template>
  <div v-if="recipe" class="recipe-detail">
    <AppPageHeader :title="`${recipe.code} — ${recipe.name}`" :subtitle="$t('recipes.detail.subtitle')" icon="pi pi-book">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-arrow-left" @click="$router.push({ name: 'production-recipes' })">
          {{ $t('common.back') }}
        </AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="createVersion">{{ $t('recipes.detail.newVersion') }}</AppButton>
      </template>
    </AppPageHeader>

    <section class="versions-section">
      <h3>{{ $t('recipes.detail.versions') }}</h3>
      <div class="versions">
        <button
          v-for="v in recipe.versions || []"
          :key="v.id"
          type="button"
          :class="['version-chip', selectedVersionId === v.id && 'version-chip--active']"
          @click="loadVersion(v.id)"
        >
          <span class="version-chip__num">v{{ v.versionNumber }}</span>
          <span :class="['pill', statusPillClass(v.status)]">{{ $t(`recipes.versionStatus.${statusKey(v.status)}`) }}</span>
        </button>
      </div>
    </section>

    <RecipeVersionEditor
      v-if="selectedVersion"
      :version="selectedVersion"
      :recipe-id="recipe.id"
      @refresh="reloadAll"
      @version-deleted="onVersionDeleted"
    />

    <AppModal
      :open="cleanupModalOpen"
      :title="$t('recipes.detail.noVersionsLeftTitle')"
      @close="cleanupModalOpen = false"
    >
      <p>{{ $t('recipes.detail.noVersionsLeftPrompt') }}</p>
      <p class="cleanup-hint">
        {{ wasEverReleased
          ? $t('recipes.detail.noVersionsLeftDeactivateHint')
          : $t('recipes.detail.noVersionsLeftDeleteHint') }}
      </p>
      <template #footer>
        <AppButton variant="ghost" :disabled="cleanupBusy" @click="cleanupModalOpen = false">
          {{ $t('recipes.detail.keepRecipe') }}
        </AppButton>
        <AppButton
          v-if="wasEverReleased"
          variant="primary"
          :loading="cleanupBusy"
          @click="deactivateRecipe"
        >
          {{ $t('recipes.detail.deactivateRecipe') }}
        </AppButton>
        <AppButton
          v-else
          variant="danger"
          :loading="cleanupBusy"
          @click="deleteRecipe"
        >
          {{ $t('recipes.detail.deleteRecipe') }}
        </AppButton>
      </template>
    </AppModal>
  </div>
  <div v-else-if="loading" class="loading"><i class="pi pi-spin pi-spinner" /> {{ $t('common.loading') }}</div>
  <div v-else class="loading">{{ $t('common.notFound') }}</div>
</template>

<script setup lang="ts">
import { onMounted, ref, computed } from 'vue';
import { useRoute, useRouter } from 'vue-router';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppModal from '../../components/ui/AppModal.vue';
import RecipeVersionEditor from '../../components/production/RecipeVersionEditor.vue';
import { recipeService, type RecipeResponse, type RecipeVersionSummary, RecipeVersionStatus } from '../../services/recipeService';
import { recipeVersionService, type RecipeVersionDetailResponse } from '../../services/recipeVersionService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const route = useRoute();
const router = useRouter();
const { t } = useI18n();
const toast = useToastStore();

const recipe = ref<RecipeResponse | null>(null);
const selectedVersionId = ref<string | null>(null);
const selectedVersion = ref<RecipeVersionDetailResponse | null>(null);
const loading = ref(false);

// "No versions left" cleanup prompt state.
const cleanupModalOpen = ref(false);
const cleanupBusy = ref(false);
// Whether the recipe was ever released — used to decide between "Delete" and
// "Deactivate" as the primary action in the cleanup prompt. Captured at the
// moment the last version is deleted (the recipe response after delete may
// still surface this via currentVersionId, but we snapshot it eagerly to be
// resilient to backend changes).
const wasEverReleased = ref(false);

const recipeId = computed(() => String(route.params.id));

function statusKey(status: RecipeVersionStatus): string {
  if (status === RecipeVersionStatus.Draft) return 'draft';
  if (status === RecipeVersionStatus.Released) return 'released';
  return 'obsolete';
}
function statusPillClass(status: RecipeVersionStatus): string {
  if (status === RecipeVersionStatus.Released) return 'pill--ok';
  if (status === RecipeVersionStatus.Draft) return 'pill--info';
  return 'pill--muted';
}

async function fetchRecipeData() {
  loading.value = true;
  try {
    recipe.value = await recipeService.get(recipeId.value);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
    recipe.value = null;
  } finally {
    loading.value = false;
  }
}

async function loadRecipe() {
  await fetchRecipeData();
  if (recipe.value && (recipe.value.versions?.length ?? 0) > 0) {
    const targetId = recipe.value.currentVersionId ?? recipe.value.versions![recipe.value.versions!.length - 1].id;
    await loadVersion(targetId);
  } else {
    selectedVersionId.value = null;
    selectedVersion.value = null;
  }
}

async function loadVersion(id: string) {
  try {
    selectedVersionId.value = id;
    selectedVersion.value = await recipeVersionService.get(id);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  }
}

function clearSelection() {
  selectedVersionId.value = null;
  selectedVersion.value = null;
}

async function createVersion() {
  if (!recipe.value) return;
  // If we have a released version, clone it; otherwise create blank.
  try {
    const releasedVersion = (recipe.value.versions || []).find(v => v.status === RecipeVersionStatus.Released);
    const result = releasedVersion
      ? await recipeVersionService.clone({ sourceVersionId: releasedVersion.id })
      : await recipeVersionService.create({ recipeId: recipe.value.id });
    toast.success(t('toasts.created'));
    await loadRecipe();
    await loadVersion(result.id);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  }
}

async function reloadAll() {
  if (!selectedVersionId.value) return;
  const versionToKeep = selectedVersionId.value;
  await fetchRecipeData();
  await loadVersion(versionToKeep);
}

/// <summary>
/// After a version is deleted in the editor, navigate to the most appropriate
/// remaining version:
///   1) the previous version (highest VersionNumber strictly less than deleted),
///   2) otherwise the last existing version (highest VersionNumber),
///   3) otherwise — no versions remain — open the cleanup prompt to delete or
///      deactivate the recipe.
/// </summary>
async function onVersionDeleted(deletedId: string) {
  // Snapshot release history *before* refetching, in case the backend updates
  // currentVersionId during cascade or the recipe row is still under a stale
  // representation. Fall back to the version list if needed.
  const before = recipe.value;
  const everReleasedSnapshot = !!(
    before?.currentVersionId ||
    (before?.versions ?? []).some(v => v.status === RecipeVersionStatus.Released)
  );
  const deletedVersionNumber = (before?.versions ?? []).find(v => v.id === deletedId)?.versionNumber ?? null;

  await fetchRecipeData();

  const remaining = (recipe.value?.versions ?? []).slice().sort(byVersionNumberDesc);
  if (remaining.length === 0) {
    clearSelection();
    wasEverReleased.value = everReleasedSnapshot;
    cleanupModalOpen.value = true;
    return;
  }

  // Prefer the largest VersionNumber that is strictly less than the deleted one.
  let next: RecipeVersionSummary | undefined;
  if (deletedVersionNumber !== null) {
    next = remaining.find(v => v.versionNumber < deletedVersionNumber);
  }
  // Fallback: the latest remaining version (e.g. when we deleted the oldest).
  next ??= remaining[0];

  await loadVersion(next.id);
}

function byVersionNumberDesc(a: RecipeVersionSummary, b: RecipeVersionSummary): number {
  return b.versionNumber - a.versionNumber;
}

async function deleteRecipe() {
  if (!recipe.value) return;
  cleanupBusy.value = true;
  try {
    await recipeService.remove(recipe.value.id);
    toast.success(t('recipes.detail.recipeDeleted'));
    cleanupModalOpen.value = false;
    await router.push({ name: 'production-recipes' });
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally {
    cleanupBusy.value = false;
  }
}

async function deactivateRecipe() {
  if (!recipe.value) return;
  cleanupBusy.value = true;
  try {
    await recipeService.update(recipe.value.id, {
      id: recipe.value.id,
      code: recipe.value.code,
      name: recipe.value.name,
      description: recipe.value.description ?? null,
      isActive: false,
      primaryProductId: recipe.value.primaryProductId ?? null
    });
    toast.success(t('recipes.detail.recipeDeactivated'));
    cleanupModalOpen.value = false;
    await router.push({ name: 'production-recipes' });
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    cleanupBusy.value = false;
  }
}

onMounted(loadRecipe);
</script>

<style scoped>
.recipe-detail { display: flex; flex-direction: column; gap: var(--space-4); }
.versions-section h3 { margin: 0 0 var(--space-2) 0; font-size: 0.95rem; color: var(--color-text-muted, #6b7280); text-transform: uppercase; letter-spacing: 0.05em; }
.versions { display: flex; flex-wrap: wrap; gap: var(--space-2); }
.version-chip { display: inline-flex; align-items: center; gap: 8px; padding: 6px 12px; border: 1px solid var(--color-border, #e5e7eb); background: var(--color-surface, #fff); border-radius: 999px; cursor: pointer; font: inherit; }
.version-chip:hover { border-color: var(--color-primary, #2563eb); }
.version-chip--active { border-color: var(--color-primary, #2563eb); background: var(--color-primary-soft, #dbeafe); }
.version-chip__num { font-weight: 600; }
.pill { display: inline-block; padding: 2px 8px; border-radius: 12px; font-size: 11px; }
.pill--ok { background: #d1fae5; color: #065f46; }
.pill--info { background: #dbeafe; color: #1e40af; }
.pill--muted { background: #e5e7eb; color: #374151; }
.loading { padding: var(--space-6); text-align: center; color: var(--color-text-muted, #6b7280); }
.cleanup-hint { color: var(--color-text-muted, #6b7280); margin-top: var(--space-2); }
</style>
