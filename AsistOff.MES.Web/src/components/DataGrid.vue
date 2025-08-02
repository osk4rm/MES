<template>
  <div class="data-grid">
    <!-- Filter Row -->
    <div class="filter-row" v-if="showFilters">
      <slot name="filters">
        <div class="default-filters">
          <input
            v-model="searchQuery"
            type="text"
            placeholder="Search..."
            class="filter-input"
          />
        </div>
      </slot>
    </div>

    <!-- Data Grid -->
    <div class="grid-container">
      <table class="grid-table">
        <thead>
          <tr>
            <th
              v-for="column in columns"
              :key="column.key"
              :class="{ sortable: column.sortable }"
              @click="column.sortable && handleSort(column.key)"
            >
              <div class="header-content">
                {{ column.label }}
                <i
                  v-if="column.sortable"
                  :class="getSortIcon(column.key)"
                  class="sort-icon"
                ></i>
              </div>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr
            v-for="(item, index) in filteredData"
            :key="getRowKey(item, index)"
            class="grid-row"
            @click="$emit('row-click', item)"
          >
            <td v-for="column in columns" :key="column.key" class="grid-cell">
              <slot :name="`cell-${column.key}`" :item="item" :value="item[column.key]">
                {{ formatCellValue(item[column.key], column) }}
              </slot>
            </td>
          </tr>
          <tr v-if="loading" class="loading-row">
            <td :colspan="columns.length" class="loading-cell">
              <div class="loading-content">
                <i class="pi pi-spin pi-spinner"></i>
                Loading...
              </div>
            </td>
          </tr>
          <tr v-else-if="filteredData.length === 0" class="empty-row">
            <td :colspan="columns.length" class="empty-cell">
              No data available
            </td>
          </tr>
        </tbody>
      </table>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, watch } from 'vue';

export interface GridColumn {
  key: string;
  label: string;
  sortable?: boolean;
  type?: 'text' | 'number' | 'date' | 'actions';
  width?: string;
}

interface Props {
  data: any[];
  columns: GridColumn[];
  loading?: boolean;
  showFilters?: boolean;
  rowKey?: string;
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  showFilters: true,
  rowKey: 'id'
});

const emit = defineEmits<{
  'row-click': [item: any];
  'sort-change': [sortKey: string, sortOrder: 'asc' | 'desc' | null];
  'action-click': [action: string, item: any];
}>();

const searchQuery = ref('');
const sortKey = ref<string | null>(null);
const sortOrder = ref<'asc' | 'desc' | null>(null);

const filteredData = computed(() => {
  let result = [...props.data];

  // Apply search filter
  if (searchQuery.value) {
    const query = searchQuery.value.toLowerCase();
    result = result.filter(item =>
      props.columns.some(column =>
        String(item[column.key] || '').toLowerCase().includes(query)
      )
    );
  }

  // Apply sorting
  if (sortKey.value && sortOrder.value) {
    result.sort((a, b) => {
      const aVal = a[sortKey.value!];
      const bVal = b[sortKey.value!];
      
      if (aVal === bVal) return 0;
      
      const comparison = aVal < bVal ? -1 : 1;
      return sortOrder.value === 'asc' ? comparison : -comparison;
    });
  }

  return result;
});

const handleSort = (key: string) => {
  if (sortKey.value === key) {
    // Cycle through: asc -> desc -> null
    if (sortOrder.value === 'asc') {
      sortOrder.value = 'desc';
    } else if (sortOrder.value === 'desc') {
      sortKey.value = null;
      sortOrder.value = null;
    } else {
      sortOrder.value = 'asc';
    }
  } else {
    sortKey.value = key;
    sortOrder.value = 'asc';
  }
  
  emit('sort-change', sortKey.value!, sortOrder.value);
};

const getSortIcon = (key: string) => {
  if (sortKey.value !== key) return 'pi pi-sort';
  if (sortOrder.value === 'asc') return 'pi pi-sort-up';
  if (sortOrder.value === 'desc') return 'pi pi-sort-down';
  return 'pi pi-sort';
};

const getRowKey = (item: any, index: number) => {
  return item[props.rowKey] || index;
};

const formatCellValue = (value: any, column: GridColumn) => {
  if (value === null || value === undefined) return '';
  
  switch (column.type) {
    case 'date':
      return new Date(value).toLocaleDateString();
    case 'number':
      return Number(value).toLocaleString();
    default:
      return String(value);
  }
};

// Watch for external search query changes
watch(searchQuery, () => {
  // Could emit search event if needed
});
</script>

<style scoped>
.data-grid {
  width: 100%;
  background: rgba(35, 39, 43, 0.8);
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.3);
}

.filter-row {
  padding: 1rem;
  background: rgba(35, 39, 43, 0.9);
  border-bottom: 1px solid rgba(252, 145, 58, 0.2);
}

.default-filters {
  display: flex;
  gap: 1rem;
  align-items: center;
}

.filter-input {
  padding: 0.5rem 0.75rem;
  border: 1px solid rgba(252, 145, 58, 0.3);
  border-radius: 6px;
  background: rgba(35, 39, 43, 0.7);
  color: #ffe066;
  font-size: 0.9rem;
  transition: all 0.2s ease;
}

.filter-input:focus {
  outline: none;
  border-color: #fc913a;
  box-shadow: 0 0 0 2px rgba(252, 145, 58, 0.2);
}

.filter-input::placeholder {
  color: rgba(255, 224, 102, 0.6);
}

.grid-container {
  overflow-x: auto;
}

.grid-table {
  width: 100%;
  border-collapse: collapse;
  font-size: 0.9rem;
}

.grid-table thead th {
  background: linear-gradient(135deg, rgba(252, 145, 58, 0.15) 0%, rgba(249, 212, 35, 0.15) 100%);
  color: #fc913a;
  padding: 1rem;
  text-align: left;
  font-weight: 600;
  border-bottom: 2px solid rgba(252, 145, 58, 0.3);
  position: sticky;
  top: 0;
  z-index: 10;
}

.grid-table thead th.sortable {
  cursor: pointer;
  user-select: none;
  transition: background-color 0.2s ease;
}

.grid-table thead th.sortable:hover {
  background: linear-gradient(135deg, rgba(252, 145, 58, 0.25) 0%, rgba(249, 212, 35, 0.25) 100%);
}

.header-content {
  display: flex;
  align-items: center;
  justify-content: space-between;
  gap: 0.5rem;
}

.sort-icon {
  font-size: 0.8rem;
  opacity: 0.7;
  transition: opacity 0.2s ease;
}

.grid-table thead th.sortable:hover .sort-icon {
  opacity: 1;
}

.grid-row {
  transition: background-color 0.2s ease;
  cursor: pointer;
}

.grid-row:nth-child(even) {
  background: rgba(35, 39, 43, 0.3);
}

.grid-row:hover {
  background: rgba(252, 145, 58, 0.1);
}

.grid-cell {
  padding: 0.75rem 1rem;
  border-bottom: 1px solid rgba(255, 224, 102, 0.1);
  color: #ffe066;
  vertical-align: middle;
}

.loading-row,
.empty-row {
  background: rgba(35, 39, 43, 0.5);
}

.loading-cell,
.empty-cell {
  padding: 2rem;
  text-align: center;
  color: rgba(255, 224, 102, 0.7);
  font-style: italic;
}

.loading-content {
  display: flex;
  align-items: center;
  justify-content: center;
  gap: 0.5rem;
}

.loading-content i {
  color: #fc913a;
}

/* Responsive design */
@media (max-width: 768px) {
  .filter-row {
    padding: 0.75rem;
  }
  
  .grid-cell {
    padding: 0.5rem 0.75rem;
    font-size: 0.8rem;
  }
  
  .grid-table thead th {
    padding: 0.75rem;
    font-size: 0.8rem;
  }
}

/* Action buttons styling */
.action-buttons {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.action-btn {
  padding: 0.4rem 0.6rem;
  border: none;
  border-radius: 6px;
  cursor: pointer;
  transition: all 0.2s ease;
  font-size: 0.8rem;
  display: flex;
  align-items: center;
  justify-content: center;
}

.action-btn:hover {
  transform: translateY(-1px);
}

.action-btn.edit {
  background: rgba(52, 152, 219, 0.2);
  color: #3498db;
  border: 1px solid rgba(52, 152, 219, 0.3);
}

.action-btn.edit:hover {
  background: rgba(52, 152, 219, 0.3);
  box-shadow: 0 2px 8px rgba(52, 152, 219, 0.3);
}

.action-btn.delete {
  background: rgba(231, 76, 60, 0.2);
  color: #e74c3c;
  border: 1px solid rgba(231, 76, 60, 0.3);
}

.action-btn.delete:hover {
  background: rgba(231, 76, 60, 0.3);
  box-shadow: 0 2px 8px rgba(231, 76, 60, 0.3);
}
</style>
