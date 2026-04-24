<template>
  <div v-if="totalCount > 0" class="app-pagination">
    <div class="app-pagination__info">
      <span>{{ $t('common.pagination.showing', { from, to, total: totalCount }) }}</span>
      <label class="app-pagination__size">
        {{ $t('common.pagination.pageSize') }}
        <select :value="pageSize" class="app-pagination__select" @change="onSizeChange">
          <option v-for="s in pageSizeOptions" :key="s" :value="s">{{ s }}</option>
        </select>
      </label>
    </div>
    <div class="app-pagination__controls">
      <button class="app-pagination__btn" :disabled="currentPage <= 1" @click="go(1)" aria-label="First page"><i class="pi pi-angle-double-left"></i></button>
      <button class="app-pagination__btn" :disabled="currentPage <= 1" @click="go(currentPage - 1)" aria-label="Previous page"><i class="pi pi-angle-left"></i></button>
      <span class="app-pagination__current">{{ currentPage }} / {{ totalPages }}</span>
      <button class="app-pagination__btn" :disabled="currentPage >= totalPages" @click="go(currentPage + 1)" aria-label="Next page"><i class="pi pi-angle-right"></i></button>
      <button class="app-pagination__btn" :disabled="currentPage >= totalPages" @click="go(totalPages)" aria-label="Last page"><i class="pi pi-angle-double-right"></i></button>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';

const props = withDefaults(defineProps<{
  currentPage: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  pageSizeOptions?: number[];
}>(), { pageSizeOptions: () => [10, 25, 50, 100] });

const emit = defineEmits<{
  (e: 'page-change', page: number): void;
  (e: 'page-size-change', size: number): void;
}>();

const from = computed(() => props.totalCount === 0 ? 0 : (props.currentPage - 1) * props.pageSize + 1);
const to = computed(() => Math.min(props.currentPage * props.pageSize, props.totalCount));

function go(page: number) {
  if (page < 1 || page > props.totalPages || page === props.currentPage) return;
  emit('page-change', page);
}

function onSizeChange(ev: Event) {
  emit('page-size-change', Number((ev.target as HTMLSelectElement).value));
}
</script>

<style scoped>
.app-pagination {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: var(--space-4);
  padding: var(--space-2) var(--space-4);
  background: var(--color-surface);
  border: 1px solid var(--color-border);
  border-top: 0;
  border-radius: 0 0 var(--radius-lg) var(--radius-lg);
  font-size: var(--font-size-sm);
  color: var(--color-text-muted);
}

.app-pagination__info { display: flex; align-items: center; gap: var(--space-4); }

.app-pagination__size { display: inline-flex; align-items: center; gap: var(--space-2); }

.app-pagination__select {
  height: var(--control-height-sm);
  padding: 0 var(--space-2);
  border: 1px solid var(--color-border-strong);
  border-radius: var(--radius-sm);
  background: var(--color-surface);
  color: var(--color-text);
}

.app-pagination__controls { display: flex; align-items: center; gap: var(--space-1); }

.app-pagination__btn {
  width: var(--control-height-sm);
  height: var(--control-height-sm);
  border-radius: var(--radius-sm);
  color: var(--color-text-muted);
  display: inline-flex;
  align-items: center;
  justify-content: center;
}
.app-pagination__btn:hover:not(:disabled) { background: var(--color-surface-sunken); color: var(--color-text); }
.app-pagination__btn:disabled { opacity: 0.4; cursor: not-allowed; }

.app-pagination__current {
  padding: 0 var(--space-3);
  color: var(--color-text);
  font-weight: var(--font-weight-medium);
  font-variant-numeric: tabular-nums;
}
</style>
