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
      :data="filteredWarehouses"
      :columns="columns"
      :loading="loading"
      :show-filters="true"
      row-key="id"
    >
      <template #filters>
        <FilterBar @clear="clearFilters">
          <IndustrialInput
            v-model="nameFilter"
            placeholder="Filter by name..."
            prefix-icon="pi pi-search"
            size="small"
          />
          <IndustrialInput
            v-model="externalIdFilter"
            placeholder="Filter by external ID..."
            prefix-icon="pi pi-filter"
            size="small"
          />
        </FilterBar>
      </template>

      <template #cell-name="{ value }">
        <div class="name-cell">
          <strong>{{ value }}</strong>
        </div>
      </template>

      <template #cell-externalId="{ value }">
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
import { ref, onMounted, computed } from 'vue';
import DataGrid, { type GridColumn } from '../components/DataGrid.vue';
import WarehouseModal, { type WarehouseFormData } from '../components/WarehouseModal.vue';
import ConfirmDialog from '../components/ConfirmDialog.vue';
import PageHeader from '../components/PageHeader.vue';
import IndustrialButton from '../components/IndustrialButton.vue';
import IndustrialInput from '../components/IndustrialInput.vue';
import FilterBar from '../components/FilterBar.vue';
import ActionButtons from '../components/ActionButtons.vue';
import { WarehouseService, type WarehouseResponse, type CreateWarehouseRequest, type UpdateWarehouseRequest } from '../services/warehouseService';
import { useToast } from 'vue-toastification';

const toast = useToast();

const warehouses = ref<WarehouseResponse[]>([]);
const loading = ref(false);
const nameFilter = ref('');
const externalIdFilter = ref('');

// Modal state
const showModal = ref(false);
const modalLoading = ref(false);
const selectedWarehouse = ref<WarehouseResponse | null>(null);
const deletingId = ref<string | null>(null);

// Confirmation dialog state
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
    key: 'externalId',
    label: 'External ID',
    sortable: true,
    type: 'text'
  },
  {
    key: 'actions',
    label: 'Actions',
    sortable: false,
    type: 'actions'
  }
];

const filteredWarehouses = computed(() => {
  let filtered = warehouses.value;
  
  if (nameFilter.value.trim()) {
    filtered = filtered.filter(w => 
      w.name.toLowerCase().includes(nameFilter.value.toLowerCase())
    );
  }
  
  if (externalIdFilter.value.trim()) {
    filtered = filtered.filter(w => 
      w.externalId?.toLowerCase().includes(externalIdFilter.value.toLowerCase())
    );
  }
  
  return filtered;
});

const loadWarehouses = async () => {
  loading.value = true;
  try {
    warehouses.value = await WarehouseService.getWarehouses();
  } catch (error: any) {
    const errorMessage = error.message || 'Failed to load warehouses';
    toast.error(errorMessage);
  } finally {
    loading.value = false;
  }
};

const refreshData = () => {
  loadWarehouses();
};

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
      // Update existing warehouse
      const updateRequest: UpdateWarehouseRequest = {
        id: selectedWarehouse.value.id,
        name: formData.name
      };
      await WarehouseService.updateWarehouse(updateRequest);
      toast.success('Warehouse updated successfully');
    } else {
      // Create new warehouse
      const createRequest: CreateWarehouseRequest = {
        name: formData.name,
        syncId: formData.externalId || undefined
      };
      await WarehouseService.createWarehouse(createRequest);
      toast.success('Warehouse created successfully');
    }
    
    closeModal();
    await loadWarehouses();
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
    await loadWarehouses();
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

const clearFilters = () => {
  nameFilter.value = '';
  externalIdFilter.value = '';
};

onMounted(() => {
  loadWarehouses();
});
</script>

<style scoped>
.warehouses-view {
  padding: 0;
  min-height: 100vh;
  color: #f1f5f9;
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
