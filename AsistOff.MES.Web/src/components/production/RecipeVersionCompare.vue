<template>
  <div class="compare">
    <div v-if="isEmpty" class="muted">{{ $t('recipes.compare.noChanges') }}</div>
    <template v-else>
      <section v-if="result.added.length > 0" class="compare__section">
        <h4>{{ $t('recipes.compare.added') }}</h4>
        <div class="compare__badges">
          <AppBadge v-for="o in result.added" :key="o.code" variant="success" icon="pi pi-plus">
            {{ o.code }} — {{ o.name }}
          </AppBadge>
        </div>
      </section>
      <section v-if="result.removed.length > 0" class="compare__section">
        <h4>{{ $t('recipes.compare.removed') }}</h4>
        <div class="compare__badges">
          <AppBadge v-for="o in result.removed" :key="o.code" variant="danger" icon="pi pi-minus">
            {{ o.code }} — {{ o.name }}
          </AppBadge>
        </div>
      </section>
      <section v-if="result.changed.length > 0" class="compare__section">
        <h4>{{ $t('recipes.compare.changed') }}</h4>
        <div v-for="change in result.changed" :key="change.code" class="compare__op">
          <strong>{{ change.code }} — {{ change.name }}</strong>
          <table
            v-for="group in groupsOf(change)"
            :key="group.key"
            class="compare__grid"
          >
            <thead>
              <tr>
                <th>{{ group.label }}</th>
                <th>{{ $t('recipes.compare.before') }}</th>
                <th>{{ $t('recipes.compare.after') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr v-for="row in group.rows" :key="row.field">
                <td>{{ fieldLabel(row.field) }}</td>
                <td>{{ row.before }}</td>
                <td>{{ row.after }}</td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
      <section v-if="result.unchanged.length > 0" class="compare__section">
        <h4>{{ $t('recipes.compare.unchanged') }}</h4>
        <div class="compare__badges">
          <AppBadge v-for="o in result.unchanged" :key="o.code" variant="idle">
            {{ o.code }} — {{ o.name }}
          </AppBadge>
        </div>
      </section>
    </template>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import AppBadge from '../ui/AppBadge.vue';
import type { FieldChange, OperationChange, VersionCompareResult } from '../../services/versionCompare';

const props = defineProps<{
  result: VersionCompareResult;
}>();

const { t } = useI18n();

const isEmpty = computed(
  () =>
    props.result.added.length === 0 &&
    props.result.removed.length === 0 &&
    props.result.changed.length === 0
);

interface ChangeGroup {
  key: string;
  label: string;
  rows: FieldChange[];
}

function groupsOf(change: OperationChange): ChangeGroup[] {
  const groups: ChangeGroup[] = [];
  const push = (key: string, sectionKey: string, rows: FieldChange[]) => {
    if (rows.length > 0) {
      groups.push({ key, label: t(`recipes.compare.sections.${sectionKey}`), rows });
    }
  };
  push('fields', 'fields', change.fields);
  push('bom', 'bom', change.bom);
  push('outputs', 'outputs', change.outputs);
  push('resources', 'resources', change.resources);
  push('dependencies', 'dependencies', change.dependencies);
  return groups;
}

function fieldLabel(field: string): string {
  const prefixed = field.match(/^(bom|output|resource):(.*)$/);
  if (prefixed) {
    const section = prefixed[1] === 'output' ? 'outputs' : prefixed[1] === 'resource' ? 'resources' : 'bom';
    return `${t(`recipes.compare.sections.${section}`)} · ${prefixed[2]}`;
  }
  return t(`recipes.compare.fields.${field}`);
}
</script>

<style scoped>
.compare { display: flex; flex-direction: column; gap: var(--space-3); }
.compare__section h4 { margin: 0 0 var(--space-2) 0; font-size: 0.85rem; text-transform: uppercase; letter-spacing: 0.05em; color: var(--color-text-muted); }
.compare__badges { display: flex; flex-wrap: wrap; gap: var(--space-2); }
.compare__op { display: flex; flex-direction: column; gap: var(--space-2); padding: var(--space-3); border: 1px solid var(--color-border, #e5e7eb); border-radius: var(--radius-md, 6px); }
.compare__grid { width: 100%; border-collapse: collapse; }
.compare__grid th, .compare__grid td { padding: 6px 10px; text-align: left; border-bottom: 1px solid var(--color-border, #e5e7eb); font-size: 0.9rem; }
.compare__grid th { font-weight: 600; color: var(--color-text-muted, #6b7280); }
.muted { color: var(--color-text-muted, #6b7280); }
</style>
