<template>
  <div class="app-table-wrap">
    <table class="app-table">
      <thead>
        <tr>
          <th
            v-for="col in columns"
            :key="col.key"
            :class="['app-table__th', { 'app-table__th--sortable': col.sortable, 'app-table__th--numeric': col.align === 'right' }]"
            :style="col.width ? { width: col.width } : undefined"
            @click="col.sortable ? toggleSort(col.key) : undefined"
          >
            <span class="app-table__th-content">
              {{ col.label }}
              <span v-if="col.sortable" class="app-table__sort-icon" aria-hidden="true">
                <i v-if="sortKey === col.key && sortDirection === 'asc'" class="pi pi-sort-up-fill"></i>
                <i v-else-if="sortKey === col.key && sortDirection === 'desc'" class="pi pi-sort-down-fill"></i>
                <i v-else class="pi pi-sort-alt"></i>
              </span>
            </span>
          </th>
        </tr>
      </thead>
      <tbody>
        <tr v-if="loading">
          <td :colspan="columns.length" class="app-table__state">
            <div class="app-table__loader"><span class="app-table__spinner"></span>{{ loadingLabel }}</div>
          </td>
        </tr>
        <tr v-else-if="!items.length">
          <td :colspan="columns.length" class="app-table__state">
            <slot name="empty">
              <div class="app-table__empty">
                <i class="pi pi-inbox app-table__empty-icon"></i>
                <p>{{ emptyLabel }}</p>
              </div>
            </slot>
          </td>
        </tr>
        <tr v-for="(item, idx) in items" v-else :key="getRowKey(item, idx)" class="app-table__row" @click="emit('row-click', item)">
          <td
            v-for="col in columns"
            :key="col.key"
            :class="['app-table__td', { 'app-table__td--numeric': col.align === 'right' }]"
          >
            <slot :name="`cell-${col.key}`" :item="item" :value="(item as any)[col.key]">
              {{ formatCell(item, col) }}
            </slot>
          </td>
        </tr>
      </tbody>
    </table>
  </div>
</template>

<script setup lang="ts" generic="T extends Record<string, any>">
export interface TableColumn {
  key: string;
  label: string;
  sortable?: boolean;
  align?: 'left' | 'right' | 'center';
  width?: string;
}

const props = withDefaults(defineProps<{
  items: T[];
  columns: TableColumn[];
  loading?: boolean;
  rowKey?: string;
  emptyLabel?: string;
  loadingLabel?: string;
  sortKey?: string | null;
  sortDirection?: 'asc' | 'desc' | null;
}>(), { loading: false, rowKey: 'id', emptyLabel: 'No data', loadingLabel: 'Loading…' });

const emit = defineEmits<{
  (e: 'row-click', item: T): void;
  (e: 'sort-change', key: string | null, direction: 'asc' | 'desc' | null): void;
}>();

function getRowKey(item: T, idx: number) {
  return (item as any)[props.rowKey] ?? idx;
}

function formatCell(item: T, col: TableColumn) {
  const v = (item as any)[col.key];
  if (v === null || v === undefined || v === '') return '—';
  return v;
}

function toggleSort(key: string) {
  let nextDir: 'asc' | 'desc' | null = 'asc';
  if (props.sortKey === key) {
    if (props.sortDirection === 'asc') nextDir = 'desc';
    else if (props.sortDirection === 'desc') nextDir = null;
  }
  emit('sort-change', nextDir === null ? null : key, nextDir);
}
</script>

<style scoped>
.app-table-wrap {
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-radius: var(--radius-lg);
  overflow: auto;
}

.app-table {
  width: 100%;
  border-collapse: separate;
  border-spacing: 0;
  font-size: var(--font-size-md);
}

.app-table__th {
  position: sticky;
  top: 0;
  background: var(--color-surface-sunken);
  color: var(--color-text-muted);
  font-weight: var(--font-weight-semibold);
  font-size: var(--font-size-xs);
  text-transform: uppercase;
  letter-spacing: 0.04em;
  text-align: left;
  padding: var(--space-2) var(--space-4);
  border-bottom: 1px solid var(--color-border);
  white-space: nowrap;
  user-select: none;
}
.app-table__th--sortable { cursor: pointer; }
.app-table__th--sortable:hover { color: var(--color-text); background: var(--color-surface-muted); }
.app-table__th--numeric { text-align: right; }
.app-table__th-content { display: inline-flex; align-items: center; gap: var(--space-1); }
.app-table__sort-icon { font-size: 0.75em; color: var(--color-text-subtle); }

.app-table__row { transition: background var(--transition-fast); }
.app-table__row:hover { background: var(--color-primary-soft); }

.app-table__td {
  padding: var(--space-2) var(--space-4);
  border-bottom: 1px solid var(--color-divider);
  height: 36px;
  vertical-align: middle;
  color: var(--color-text);
}
.app-table__td--numeric { text-align: right; font-variant-numeric: tabular-nums; }

.app-table__row:last-child .app-table__td { border-bottom: 0; }

.app-table__state { padding: var(--space-10); text-align: center; color: var(--color-text-muted); }

.app-table__loader {
  display: inline-flex;
  align-items: center;
  gap: var(--space-2);
}

.app-table__spinner {
  width: 14px;
  height: 14px;
  border: 2px solid var(--color-border-strong);
  border-right-color: var(--color-primary);
  border-radius: 50%;
  animation: app-table-spin 0.7s linear infinite;
}

.app-table__empty { display: flex; flex-direction: column; align-items: center; gap: var(--space-2); color: var(--color-text-muted); }
.app-table__empty-icon { font-size: 32px; color: var(--color-text-subtle); }

@keyframes app-table-spin { to { transform: rotate(360deg); } }
</style>
