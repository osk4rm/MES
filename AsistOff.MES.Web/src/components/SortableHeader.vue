<template>
  <th 
    :class="[
      'sortable-header',
      { 'sorted': isSorted, 'sorted-asc': isSorted && sortDirection === 'asc', 'sorted-desc': isSorted && sortDirection === 'desc' }
    ]"
    @click="handleSort"
  >
    <div class="header-content">
      <span>{{ label }}</span>
      <div class="sort-icons" v-if="sortable">
        <svg 
          class="sort-icon sort-up" 
          :class="{ 'active': isSorted && sortDirection === 'asc' }"
          width="12" height="12" viewBox="0 0 12 12"
        >
          <path d="M6 3l3 3H3z" fill="currentColor"/>
        </svg>
        <svg 
          class="sort-icon sort-down" 
          :class="{ 'active': isSorted && sortDirection === 'desc' }"
          width="12" height="12" viewBox="0 0 12 12"
        >
          <path d="M6 9L3 6h6z" fill="currentColor"/>
        </svg>
      </div>
    </div>
  </th>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import type { SortField } from '../models/pagedModels';

interface Props {
  field: string;
  label: string;
  sortable?: boolean;
  currentSort?: SortField[];
}

interface Emits {
  (e: 'sort', field: string, direction: 'asc' | 'desc'): void;
}

const props = withDefaults(defineProps<Props>(), {
  sortable: true,
  currentSort: () => []
});

const emit = defineEmits<Emits>();

const currentSortForField = computed(() => {
  return props.currentSort?.find(s => s.field === props.field);
});

const isSorted = computed(() => {
  return !!currentSortForField.value;
});

const sortDirection = computed(() => {
  return currentSortForField.value?.direction || 'asc';
});

function handleSort() {
  if (!props.sortable) return;
  
  let newDirection: 'asc' | 'desc' = 'asc';
  
  if (isSorted.value) {
    newDirection = sortDirection.value === 'asc' ? 'desc' : 'asc';
  }
  
  emit('sort', props.field, newDirection);
}
</script>

<style scoped>
.sortable-header {
  cursor: pointer;
  user-select: none;
  padding: 0.75rem;
  text-align: left;
  font-weight: 600;
  color: #374151;
  border-bottom: 2px solid #e5e7eb;
  transition: background-color 0.2s;
}

.sortable-header:hover {
  background-color: #f9fafb;
}

.header-content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.sort-icons {
  display: flex;
  flex-direction: column;
  gap: 1px;
}

.sort-icon {
  color: #9ca3af;
  transition: color 0.2s;
}

.sort-icon.active {
  color: #2563eb;
}

.sorted {
  background-color: #f0f9ff;
}
</style>
