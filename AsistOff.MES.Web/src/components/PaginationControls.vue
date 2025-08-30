<template>
  <div class="pagination-container" v-if="totalPages > 1">
    <div class="pagination-info">
      <span class="text-sm text-gray-600">
        Showing {{ startItem }} to {{ endItem }} of {{ totalCount }} entries
      </span>
    </div>
    
    <div class="pagination-controls">
      <!-- Page size selector -->
      <div class="page-size-selector">
        <label class="text-sm text-gray-600 mr-2">Show:</label>
        <select 
          v-model="selectedPageSize" 
          @change="handlePageSizeChange"
          class="border border-gray-300 rounded px-2 py-1 text-sm"
        >
          <option v-for="size in pageSizeOptions" :key="size" :value="size">
            {{ size }}
          </option>
        </select>
      </div>

      <!-- Pagination buttons -->
      <div class="pagination-buttons">
        <button
          @click="goToPage(1)"
          :disabled="currentPage === 1"
          class="pagination-btn"
          :class="{ 'disabled': currentPage === 1 }"
        >
          First
        </button>
        
        <button
          @click="goToPage(currentPage - 1)"
          :disabled="currentPage === 1"
          class="pagination-btn"
          :class="{ 'disabled': currentPage === 1 }"
        >
          Previous
        </button>

        <!-- Page numbers -->
        <template v-for="page in visiblePages" :key="page">
          <button
            v-if="page !== '...'"
            @click="goToPage(page)"
            class="pagination-btn"
            :class="{ 'active': page === currentPage }"
          >
            {{ page }}
          </button>
          <span v-else class="pagination-ellipsis">...</span>
        </template>

        <button
          @click="goToPage(currentPage + 1)"
          :disabled="currentPage === totalPages"
          class="pagination-btn"
          :class="{ 'disabled': currentPage === totalPages }"
        >
          Next
        </button>
        
        <button
          @click="goToPage(totalPages)"
          :disabled="currentPage === totalPages"
          class="pagination-btn"
          :class="{ 'disabled': currentPage === totalPages }"
        >
          Last
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, defineEmits, defineProps, ref, watch } from 'vue';

interface Props {
  currentPage: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  pageSizeOptions?: number[];
}

interface Emits {
  (e: 'page-change', page: number): void;
  (e: 'page-size-change', pageSize: number): void;
}

const props = withDefaults(defineProps<Props>(), {
  pageSizeOptions: () => [5, 10, 25, 50, 100]
});

const emit = defineEmits<Emits>();

const selectedPageSize = ref(props.pageSize);

watch(() => props.pageSize, (newPageSize) => {
  selectedPageSize.value = newPageSize;
});

const startItem = computed(() => {
  if (props.totalCount === 0) return 0;
  return (props.currentPage - 1) * props.pageSize + 1;
});

const endItem = computed(() => {
  const end = props.currentPage * props.pageSize;
  return Math.min(end, props.totalCount);
});

const visiblePages = computed(() => {
  const pages: (number | string)[] = [];
  const maxVisiblePages = 7;
  const halfVisible = Math.floor(maxVisiblePages / 2);
  
  if (props.totalPages <= maxVisiblePages) {
    // Show all pages if total is less than max visible
    for (let i = 1; i <= props.totalPages; i++) {
      pages.push(i);
    }
  } else {
    // Always show first page
    pages.push(1);
    
    let start = Math.max(2, props.currentPage - halfVisible);
    let end = Math.min(props.totalPages - 1, props.currentPage + halfVisible);
    
    // Adjust if we're near the beginning
    if (props.currentPage <= halfVisible) {
      end = maxVisiblePages - 1;
    }
    
    // Adjust if we're near the end
    if (props.currentPage > props.totalPages - halfVisible) {
      start = props.totalPages - maxVisiblePages + 2;
    }
    
    // Add ellipsis if there's a gap after first page
    if (start > 2) {
      pages.push('...');
    }
    
    // Add middle pages
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    
    // Add ellipsis if there's a gap before last page
    if (end < props.totalPages - 1) {
      pages.push('...');
    }
    
    // Always show last page
    if (props.totalPages > 1) {
      pages.push(props.totalPages);
    }
  }
  
  return pages;
});

function goToPage(page: number | string) {
  if (typeof page === 'number' && page !== props.currentPage && page >= 1 && page <= props.totalPages) {
    emit('page-change', page);
  }
}

function handlePageSizeChange() {
  emit('page-size-change', selectedPageSize.value);
}
</script>

<style scoped>
.pagination-container {
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  align-items: center;
  gap: 1rem;
  padding: 1rem;
  background-color: white;
  border-top: 1px solid #e5e7eb;
}

@media (min-width: 640px) {
  .pagination-container {
    flex-direction: row;
  }
}

.pagination-info {
  font-size: 0.875rem;
  color: #6b7280;
}

.pagination-controls {
  display: flex;
  align-items: center;
  gap: 1rem;
}

.page-size-selector {
  display: flex;
  align-items: center;
}

.pagination-buttons {
  display: flex;
  align-items: center;
  gap: 0.25rem;
}

.pagination-btn {
  padding: 0.25rem 0.75rem;
  font-size: 0.875rem;
  border: 1px solid #d1d5db;
  background-color: white;
  color: #374151;
  border-radius: 0.375rem;
  transition: background-color 0.2s, color 0.2s;
  cursor: pointer;
}

.pagination-btn:hover {
  background-color: #f9fafb;
}

.pagination-btn.active {
  background-color: #2563eb;
  color: white;
  border-color: #2563eb;
}

.pagination-btn.active:hover {
  background-color: #1d4ed8;
}

.pagination-btn.disabled {
  opacity: 0.5;
  cursor: not-allowed;
}

.pagination-btn.disabled:hover {
  background-color: white;
}

.pagination-ellipsis {
  padding: 0.25rem 0.5rem;
  color: #6b7280;
}
</style>
