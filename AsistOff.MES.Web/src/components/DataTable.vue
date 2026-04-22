<template>
  <div class="data-table-container">
    <!-- Filters slot -->
    <div v-if="$slots.filters" class="table-filters">
      <slot name="filters"></slot>
    </div>

    <!-- Table -->
    <div class="table-wrapper" :class="{ 'loading': loading }">
      <table class="data-table">
        <thead>
          <tr>
            <slot name="headers" :sort="handleSort" :currentSort="currentSort"></slot>
          </tr>
        </thead>
        <tbody>
          <template v-if="!loading && items.length > 0">
            <tr v-for="(item, index) in items" :key="getItemKey(item, index)">
              <slot name="row" :item="item" :index="index"></slot>
            </tr>
          </template>
          <tr v-else-if="!loading && items.length === 0">
            <td :colspan="columnCount" class="no-data">
              <slot name="no-data">
                <div class="no-data-content">
                  <p>No data available</p>
                </div>
              </slot>
            </td>
          </tr>
          <tr v-if="loading">
            <td :colspan="columnCount" class="loading-cell">
              <div class="loading-content">
                <IndustrialLoader size="sm" />
                <span>Loading...</span>
              </div>
            </td>
          </tr>
        </tbody>
      </table>
    </div>

    <!-- Pagination -->
    <PaginationControls
      v-if="showPagination"
      :current-page="currentPage"
      :page-size="pageSize"
      :total-count="totalCount"
      :total-pages="totalPages"
      :page-size-options="pageSizeOptions"
      @page-change="handlePageChange"
      @page-size-change="handlePageSizeChange"
    />
  </div>
</template>

<script setup lang="ts">
import { defineProps, defineEmits } from 'vue';
import PaginationControls from './PaginationControls.vue';
import IndustrialLoader from './IndustrialLoader.vue';
import type { SortField } from '../models/pagedModels';

interface Props {
  items: any[];
  loading?: boolean;
  currentPage: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  currentSort?: SortField[];
  showPagination?: boolean;
  pageSizeOptions?: number[];
  columnCount?: number;
  itemKey?: string | ((item: any) => string);
}

interface Emits {
  (e: 'page-change', page: number): void;
  (e: 'page-size-change', pageSize: number): void;
  (e: 'sort', field: string, direction: 'asc' | 'desc'): void;
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  currentSort: () => [],
  showPagination: true,
  pageSizeOptions: () => [5, 10, 25, 50, 100],
  columnCount: 1,
  itemKey: 'id'
});

const emit = defineEmits<Emits>();

function getItemKey(item: any, index: number): string {
  if (typeof props.itemKey === 'function') {
    return props.itemKey(item);
  }
  return item[props.itemKey] || index.toString();
}

function handlePageChange(page: number) {
  emit('page-change', page);
}

function handlePageSizeChange(pageSize: number) {
  emit('page-size-change', pageSize);
}

function handleSort(field: string, direction: 'asc' | 'desc') {
  emit('sort', field, direction);
}
</script>

<style scoped>
.data-table-container {
  background: white;
  border-radius: 8px;
  box-shadow: 0 1px 3px rgba(0, 0, 0, 0.1);
  overflow: hidden;
}

.table-filters {
  padding: 1rem;
  border-bottom: 1px solid #e5e7eb;
  background-color: #f9fafb;
}

.table-wrapper {
  overflow-x: auto;
  position: relative;
}

.table-wrapper.loading {
  opacity: 0.7;
}

.data-table {
  width: 100%;
  border-collapse: collapse;
}

.data-table th,
.data-table td {
  text-align: left;
  vertical-align: middle;
  border-bottom: 1px solid #e5e7eb;
}

.data-table th {
  background-color: #f9fafb;
  font-weight: 600;
  color: #374151;
}

.data-table td {
  padding: 1rem 0.75rem;
}

.data-table tbody tr:hover {
  background-color: #f9fafb;
}

.no-data {
  text-align: center;
  padding: 3rem 1rem;
}

.no-data-content {
  color: #6b7280;
}

.loading-cell {
  text-align: center;
  padding: 2rem 1rem;
}

.loading-content {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
  color: #6b7280;
}
</style>
