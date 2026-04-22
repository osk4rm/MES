<template>
  <div class="industrial-pagination">
    <div class="pagination-info-section">
      <div class="entries-info">
        <span class="info-text">
          Showing <strong>{{ startItem }}</strong> to <strong>{{ endItem }}</strong> of <strong>{{ totalCount }}</strong> entries
        </span>
      </div>
      
      <div class="page-size-control">
        <label class="size-label">Show:</label>
        <select 
          v-model="selectedPageSize" 
          @change="handlePageSizeChange"
          class="industrial-select"
        >
          <option v-for="size in pageSizeOptions" :key="size" :value="size">
            {{ size }} per page
          </option>
        </select>
      </div>
    </div>

    <div class="pagination-navigation">
      <button
        @click="goToPage(1)"
        :disabled="currentPage === 1"
        class="industrial-nav-btn"
        :class="{ 'disabled': currentPage === 1 }"
        title="First page"
      >
        <i class="pi pi-angle-double-left"></i>
      </button>
      
      <button
        @click="goToPage(currentPage - 1)"
        :disabled="currentPage === 1"
        class="industrial-nav-btn"
        :class="{ 'disabled': currentPage === 1 }"
        title="Previous page"
      >
        <i class="pi pi-angle-left"></i>
      </button>

      <div class="page-numbers">
        <template v-for="page in visiblePages" :key="page">
          <button
            v-if="page !== '...'"
            @click="goToPage(page)"
            class="industrial-page-btn"
            :class="{ 'active': page === currentPage }"
          >
            {{ page }}
          </button>
          <span v-else class="page-ellipsis">...</span>
        </template>
      </div>

      <button
        @click="goToPage(currentPage + 1)"
        :disabled="currentPage === totalPages"
        class="industrial-nav-btn"
        :class="{ 'disabled': currentPage === totalPages }"
        title="Next page"
      >
        <i class="pi pi-angle-right"></i>
      </button>
      
      <button
        @click="goToPage(totalPages)"
        :disabled="currentPage === totalPages"
        class="industrial-nav-btn"
        :class="{ 'disabled': currentPage === totalPages }"
        title="Last page"
      >
        <i class="pi pi-angle-double-right"></i>
      </button>
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
    for (let i = 1; i <= props.totalPages; i++) {
      pages.push(i);
    }
  } else {
    pages.push(1);
    
    let start = Math.max(2, props.currentPage - halfVisible);
    let end = Math.min(props.totalPages - 1, props.currentPage + halfVisible);
    
    if (props.currentPage <= halfVisible) {
      end = maxVisiblePages - 1;
    }
    
    if (props.currentPage > props.totalPages - halfVisible) {
      start = props.totalPages - maxVisiblePages + 2;
    }
    
    if (start > 2) {
      pages.push('...');
    }
    
    for (let i = start; i <= end; i++) {
      pages.push(i);
    }
    
    if (end < props.totalPages - 1) {
      pages.push('...');
    }
    
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
.industrial-pagination {
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
  padding: 1.5rem;
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
  border-radius: 12px;
  border: 1px solid #475569;
  box-shadow: 
    0 4px 16px rgba(0, 0, 0, 0.15),
    0 0 0 1px rgba(71, 85, 105, 0.3),
    inset 0 1px 0 rgba(148, 163, 184, 0.1);
}

@media (min-width: 768px) {
  .industrial-pagination {
    flex-direction: row;
    justify-content: space-between;
    align-items: center;
  }
}

.pagination-info-section {
  display: flex;
  flex-direction: column;
  gap: 1rem;
  align-items: flex-start;
}

@media (min-width: 768px) {
  .pagination-info-section {
    flex-direction: row;
    align-items: center;
    gap: 2rem;
  }
}

.entries-info {
  display: flex;
  align-items: center;
}

.info-text {
  font-size: 14px;
  color: #cbd5e1;
  font-weight: 400;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

.info-text strong {
  color: #fc913a;
  font-weight: 600;
}

.page-size-control {
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.size-label {
  font-size: 14px;
  color: #cbd5e1;
  font-weight: 500;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

.industrial-select {
  padding: 8px 40px 8px 12px;
  background: linear-gradient(135deg, #475569 0%, #64748b 100%);
  border: 1px solid #64748b;
  border-radius: 8px;
  color: #e2e8f0;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 
    0 2px 4px rgba(0, 0, 0, 0.1),
    inset 0 1px 0 rgba(148, 163, 184, 0.1);
  -webkit-appearance: none;
  -moz-appearance: none;
  appearance: none;
  background-image: url("data:image/svg+xml;charset=utf-8,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%23cbd5e1' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 12px center;
  background-size: 16px;
  min-width: 160px;
}

.industrial-select option {
  background: #475569;
  color: #e2e8f0;
  border: none;
}

.industrial-select:hover {
  background: linear-gradient(135deg, #64748b 0%, #94a3b8 100%);
  border-color: #94a3b8;
  transform: translateY(-1px);
  box-shadow: 
    0 4px 8px rgba(0, 0, 0, 0.15),
    inset 0 1px 0 rgba(148, 163, 184, 0.2);
  background-image: url("data:image/svg+xml;charset=utf-8,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%23f1f5f9' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 12px center;
  background-size: 16px;
}

.industrial-select:focus {
  outline: none;
  border-color: #fc913a;
  box-shadow: 
    0 0 0 2px rgba(252, 145, 58, 0.3),
    0 4px 8px rgba(0, 0, 0, 0.15);
  background-image: url("data:image/svg+xml;charset=utf-8,%3Csvg xmlns='http://www.w3.org/2000/svg' fill='none' viewBox='0 0 20 20'%3E%3Cpath stroke='%23fc913a' stroke-linecap='round' stroke-linejoin='round' stroke-width='1.5' d='M6 8l4 4 4-4'/%3E%3C/svg%3E");
  background-repeat: no-repeat;
  background-position: right 12px center;
  background-size: 16px;
}

.pagination-navigation {
  display: flex;
  align-items: center;
  gap: 0.5rem;
  flex-wrap: wrap;
  justify-content: center;
}

@media (min-width: 768px) {
  .pagination-navigation {
    justify-content: flex-end;
  }
}

.industrial-nav-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  width: 40px;
  height: 40px;
  background: linear-gradient(135deg, #475569 0%, #64748b 100%);
  border: 1px solid #64748b;
  border-radius: 8px;
  color: #e2e8f0;
  font-size: 16px;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 
    0 2px 4px rgba(0, 0, 0, 0.1),
    inset 0 1px 0 rgba(148, 163, 184, 0.1);
}

.industrial-nav-btn:hover:not(.disabled) {
  background: linear-gradient(135deg, #64748b 0%, #94a3b8 100%);
  border-color: #94a3b8;
  transform: translateY(-1px);
  box-shadow: 
    0 4px 8px rgba(0, 0, 0, 0.15),
    inset 0 1px 0 rgba(148, 163, 184, 0.2);
}

.industrial-nav-btn:active:not(.disabled) {
  transform: translateY(0);
  box-shadow: 
    0 2px 4px rgba(0, 0, 0, 0.15),
    inset 0 1px 2px rgba(0, 0, 0, 0.1);
}

.industrial-nav-btn.disabled {
  opacity: 0.4;
  cursor: not-allowed;
  background: linear-gradient(135deg, #374151 0%, #4b5563 100%);
  border-color: #4b5563;
  color: #9ca3af;
}

.page-numbers {
  display: flex;
  align-items: center;
  gap: 0.25rem;
  margin: 0 0.5rem;
}

.industrial-page-btn {
  display: flex;
  align-items: center;
  justify-content: center;
  min-width: 40px;
  height: 40px;
  padding: 0 12px;
  background: linear-gradient(135deg, #475569 0%, #64748b 100%);
  border: 1px solid #64748b;
  border-radius: 8px;
  color: #e2e8f0;
  font-size: 14px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  box-shadow: 
    0 2px 4px rgba(0, 0, 0, 0.1),
    inset 0 1px 0 rgba(148, 163, 184, 0.1);
}

.industrial-page-btn:hover {
  background: linear-gradient(135deg, #64748b 0%, #94a3b8 100%);
  border-color: #94a3b8;
  transform: translateY(-1px);
  box-shadow: 
    0 4px 8px rgba(0, 0, 0, 0.15),
    inset 0 1px 0 rgba(148, 163, 184, 0.2);
}

.industrial-page-btn.active {
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  border-color: #e88b2f;
  color: #fff;
  box-shadow: 
    0 4px 12px rgba(252, 145, 58, 0.4),
    inset 0 1px 0 rgba(255, 255, 255, 0.2);
}

.industrial-page-btn.active:hover {
  background: linear-gradient(135deg, #e88b2f 0%, #f5c842 100%);
  transform: translateY(-1px);
  box-shadow: 
    0 6px 16px rgba(252, 145, 58, 0.5),
    inset 0 1px 0 rgba(255, 255, 255, 0.3);
}

.page-ellipsis {
  padding: 0 8px;
  color: #94a3b8;
  font-size: 16px;
  font-weight: 600;
  text-shadow: 0 1px 2px rgba(0, 0, 0, 0.3);
}

@media (max-width: 640px) {
  .industrial-pagination {
    padding: 1rem;
    gap: 1rem;
  }
  
  .pagination-info-section {
    text-align: center;
  }
  
  .pagination-navigation {
    flex-wrap: wrap;
    justify-content: center;
  }
  
  .industrial-nav-btn,
  .industrial-page-btn {
    width: 36px;
    height: 36px;
    min-width: 36px;
    font-size: 13px;
  }
  
  .page-numbers {
    margin: 0 0.25rem;
    gap: 0.125rem;
  }
}
</style>
