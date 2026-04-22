<template>
  <div class="warehouses-view">
    <PageHeader 
      title="Warehouses" 
      icon="pi pi-building"
      subtitle="Manage your warehouse locations and settings"
    >
      <template #actions>
        <IndustrialButton
          variant="secondary"
          icon="pi pi-refresh"
          @click="refreshData"
        >
          Refresh
        </IndustrialButton>
        <IndustrialButton
          variant="primary"
          icon="pi pi-plus"
          @click="addWarehouse"
        >
          Add Warehouse
        </IndustrialButton>
      </template>
    </PageHeader>

    <DataGrid
      :data="data"
      :columns="columns"
      :loading="loading"
      :show-filters="true"
      row-key="id"
      @sort-change="handleSortChange"
    >
      <template #filters>
        <FilterBar @clear="clearFilters">
          <IndustrialInput
            v-model="nameFilter"
            placeholder="Filter by name..."
            prefix-icon="pi pi-search"
            size="small"
          />
        </FilterBar>
      </template>

      <template #cell-name="{ value }">
        <div class="name-cell">
          <strong>{{ value }}</strong>
        </div>
      </template>

      <template #cell-syncId="{ value }">
        <span class="external-id-cell">
          {{ value || '-' }}
        </span>
      </template>

      <template #cell-actions="{ item }">
        <ActionButtons 
          :actions="getRowActions(item)" 
          @action="handleRowAction($event, item)"
        />
      </template>
    </DataGrid>

    <PaginationControls
      :current-page="currentPage"
      :total-pages="totalPages"
      :page-size="pageSize"
      :total-count="totalItems"
      :loading="loading"
      @page-change="handlePageChange"
      @page-size-change="handlePageSizeChange"
    />

    <!-- Add/Edit Warehouse Modal -->
    <WarehouseModal
      :is-visible="showModal"
      :warehouse="selectedWarehouse"
      :loading="modalLoading"
      @close="closeModal"
      @save="saveWarehouse"
    />

    <!-- Confirmation Dialog -->
    <ConfirmDialog
      :is-visible="showConfirmDialog"
      :loading="confirmDialogLoading"
      title="Delete Warehouse"
      :message="`Are you sure you want to delete '${warehouseToDelete?.name}'?`"
      details="This action cannot be undone."
      confirm-text="Delete"
      cancel-text="Cancel"
      @confirm="handleDeleteConfirm"
      @cancel="handleDeleteCancel"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted, watch } from 'vue';
import DataGrid, { type GridColumn } from '../components/DataGrid.vue';
import WarehouseModal, { type WarehouseFormData } from '../components/WarehouseModal.vue';
import ConfirmDialog from '../components/ConfirmDialog.vue';
import PageHeader from '../components/PageHeader.vue';
import IndustrialButton from '../components/IndustrialButton.vue';
import IndustrialInput from '../components/IndustrialInput.vue';
import FilterBar from '../components/FilterBar.vue';
import ActionButtons from '../components/ActionButtons.vue';
import PaginationControls from '../components/PaginationControls.vue';
import { WarehouseService, type WarehouseResponse, type CreateWarehouseRequest, type UpdateWarehouseRequest } from '../services/warehouseService';
import { useCrudTable } from '../composables/useDataTable';
import { useToast } from 'vue-toastification';

const toast = useToast();

const table = useCrudTable<WarehouseResponse>(
  {
    getAll: WarehouseService.getWarehouses
  },
  {
    pageSize: 10
  }
);

const {
  currentPage,
  totalPages,
  pageSize,
  totalCount: totalItems,
  items: data,
  loading,
  goToPage,
  changePageSize,
  setSort,
  clearSort,
  fetch,
  setFilter,
  clearFilter
} = table;

const nameFilter = ref('');

// Filter functionality with debouncing
let nameFilterTimeout: number;

function handleNameFilter() {
  clearTimeout(nameFilterTimeout);
  nameFilterTimeout = setTimeout(() => {
    if (nameFilter.value.trim()) {
      setFilter('name', nameFilter.value.trim());
    } else {
      clearFilter('name');
    }
  }, 300) as unknown as number;
}

watch(nameFilter, handleNameFilter);

const handleSortChange = (sortKey: string | null, sortOrder: 'asc' | 'desc' | null) => {
  if (sortKey && sortOrder) {
    setSort(sortKey, sortOrder);
  } else {
    clearSort();
  }
};

const handlePageChange = (page: number) => {
  goToPage(page);
};

const handlePageSizeChange = (size: number) => {
  changePageSize(size);
};

const refreshData = () => {
  fetch();
};

const clearFilters = () => {
  nameFilter.value = '';
  clearFilter('name');
};

const showModal = ref(false);
const modalLoading = ref(false);
const selectedWarehouse = ref<WarehouseResponse | null>(null);
const deletingId = ref<string | null>(null);

const showConfirmDialog = ref(false);
const confirmDialogLoading = ref(false);
const warehouseToDelete = ref<WarehouseResponse | null>(null);

const columns: GridColumn[] = [
  {
    key: 'name',
    label: 'Name',
    sortable: true,
    type: 'text'
  },
  {
    key: 'syncId',
    label: 'External ID',
    sortable: false,
    type: 'text'
  },
  {
    key: 'actions',
    label: 'Actions',
    sortable: false,
    type: 'actions'
  }
];

const addWarehouse = () => {
  selectedWarehouse.value = null;
  showModal.value = true;
};

const editWarehouse = (warehouse: WarehouseResponse) => {
  selectedWarehouse.value = warehouse;
  showModal.value = true;
};

const getRowActions = (warehouse: WarehouseResponse) => [
  {
    key: 'edit',
    icon: 'pi pi-pencil',
    variant: 'secondary' as const,
    tooltip: 'Edit warehouse'
  },
  {
    key: 'delete',
    icon: 'pi pi-trash',
    variant: 'danger' as const,
    tooltip: 'Delete warehouse',
    loading: deletingId.value === warehouse.id,
    disabled: deletingId.value === warehouse.id
  }
];

const handleRowAction = (action: any, warehouse: WarehouseResponse) => {
  switch (action.key) {
    case 'edit':
      editWarehouse(warehouse);
      break;
    case 'delete':
      confirmDelete(warehouse);
      break;
  }
};

const closeModal = () => {
  showModal.value = false;
  selectedWarehouse.value = null;
  modalLoading.value = false;
};

const saveWarehouse = async (formData: WarehouseFormData) => {
  modalLoading.value = true;
  try {
    if (selectedWarehouse.value) {
      const updateRequest: UpdateWarehouseRequest = {
        id: selectedWarehouse.value.id,
        name: formData.name
      };
      await WarehouseService.updateWarehouse(updateRequest);
      toast.success('Warehouse updated successfully');
    } else {
      const createRequest: CreateWarehouseRequest = {
        name: formData.name,
        syncId: formData.externalId || undefined
      };
      await WarehouseService.createWarehouse(createRequest);
      toast.success('Warehouse created successfully');
    }
    
    closeModal();
    await fetch();
  } catch (error: any) {
    const errorMessage = error.message || 'Failed to save warehouse';
    toast.error(errorMessage);
  } finally {
    modalLoading.value = false;
  }
};

const confirmDelete = (warehouse: WarehouseResponse) => {
  warehouseToDelete.value = warehouse;
  showConfirmDialog.value = true;
};

const handleDeleteConfirm = async () => {
  if (!warehouseToDelete.value) return;
  
  confirmDialogLoading.value = true;
  const warehouse = warehouseToDelete.value;
  
  try {
    await WarehouseService.deleteWarehouse(warehouse.id);
    toast.success('Warehouse deleted successfully');
    await fetch();
    showConfirmDialog.value = false;
    warehouseToDelete.value = null;
  } catch (error: any) {
    const errorMessage = error.message || 'Failed to delete warehouse';
    toast.error(errorMessage);
  } finally {
    confirmDialogLoading.value = false;
  }
};

const handleDeleteCancel = () => {
  showConfirmDialog.value = false;
  warehouseToDelete.value = null;
  confirmDialogLoading.value = false;
};

onMounted(() => {
  fetch();
});
</script>

<style scoped>
.warehouses-view {
  display: flex;
  flex-direction: column;
  min-height: calc(100vh - 48px);
}

.warehouses-view > *:not(:last-child) {
  margin-bottom: 1.5rem;
}

.warehouses-view > *:nth-last-child(2) {
  margin-bottom: 1rem;
}

.warehouses-view > *:last-child {
  margin-top: auto;
}

.name-cell {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.name-cell strong {
  color: #f1f5f9;
  font-weight: 600;
}

.external-id-cell {
  color: #94a3b8;
  font-style: italic;
}
</style>
