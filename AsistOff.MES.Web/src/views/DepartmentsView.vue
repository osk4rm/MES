<template>
  <div class="departments-view">
    <PageHeader 
      title="Departments" 
      icon="pi pi-sitemap"
      subtitle="Manage your organizational departments"
    >
      <template #actions>
        <IndustrialButton
          variant="secondary"
          icon="pi pi-refresh"
          @click="table.fetch"
        >
          Refresh
        </IndustrialButton>
        <IndustrialButton
          variant="primary"
          icon="pi pi-plus"
          @click="openCreateModal"
        >
          Add Department
        </IndustrialButton>
      </template>
    </PageHeader>

    <DataGrid
      :data="table.items.value"
      :columns="columns"
      :loading="table.loading.value"
      :show-filters="true"
      row-key="id"
    >
      <template #filters>
        <FilterBar @clear="clearFilters">
          <IndustrialInput
            v-model="codeFilter"
            placeholder="Filter by code..."
            prefix-icon="pi pi-search"
            size="small"
            @input="handleCodeFilter"
          />
          <IndustrialInput
            v-model="nameFilter"
            placeholder="Filter by name..."
            prefix-icon="pi pi-search"
            size="small"
            @input="handleNameFilter"
          />
        </FilterBar>
      </template>

      <template #cell-name="{ value }">
        <div class="name-cell">
          <strong>{{ value }}</strong>
        </div>
      </template>

      <template #cell-actions="{ item }">
        <ActionButtons 
          :actions="getRowActions(item)" 
          @action="handleRowAction($event, item)"
        />
      </template>
    </DataGrid>

    <!-- Department Modal -->
    <DepartmentModal
      :is-visible="showModal"
      :department="selectedDepartment"
      :loading="modalLoading"
      @close="closeModal"
      @save="handleSave"
    />

    <!-- Confirm Dialog -->
    <ConfirmDialog
      :is-visible="showConfirmDialog"
      :loading="confirmDialogLoading"
      title="Delete Department"
      :message="`Are you sure you want to delete department '${departmentToDelete?.name}'?`"
      details="This action cannot be undone."
      confirm-text="Delete"
      cancel-text="Cancel"
      @confirm="confirmDelete"
      @cancel="cancelDelete"
    />
  </div>
</template>

<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { useToast } from 'vue-toastification'
import { useCrudTable } from '../composables/useDataTable'
import DataGrid, { type GridColumn } from '../components/DataGrid.vue'
import FilterBar from '../components/FilterBar.vue'
import PageHeader from '../components/PageHeader.vue'
import IndustrialButton from '../components/IndustrialButton.vue'
import IndustrialInput from '../components/IndustrialInput.vue'
import ActionButtons from '../components/ActionButtons.vue'
import DepartmentModal, { type DepartmentFormData } from '../components/DepartmentModal.vue'
import ConfirmDialog from '../components/ConfirmDialog.vue'
import { departmentService, type Department, type DepartmentFilter } from '../services/departmentService'

const toast = useToast()

// Define columns for DataGrid
const columns: GridColumn[] = [
  { key: 'code', label: 'Code', sortable: true },
  { key: 'name', label: 'Name', sortable: true },
  { key: 'actions', label: 'Actions', sortable: false, type: 'actions' }
]

// Table setup with backend pagination and filtering
const table = useCrudTable<Department, any, any, DepartmentFilter>(
  {
    getAll: departmentService.getDepartments.bind(departmentService),
    create: departmentService.createDepartment.bind(departmentService),
    update: (id: string, item: any) => departmentService.updateDepartment(id, { id, ...item }),
    delete: departmentService.deleteDepartment.bind(departmentService)
  },
  {
    pageSize: 10
  }
)

// Local state for modals and filters
const codeFilter = ref('')
const nameFilter = ref('')
const showModal = ref(false)
const selectedDepartment = ref<Department | null>(null)
const modalLoading = ref(false)
const showConfirmDialog = ref(false)
const departmentToDelete = ref<Department | null>(null)
const confirmDialogLoading = ref(false)

// Filter functionality with debouncing
let codeFilterTimeout: number
let nameFilterTimeout: number

function handleCodeFilter() {
  clearTimeout(codeFilterTimeout)
  codeFilterTimeout = setTimeout(() => {
    if (codeFilter.value.trim()) {
      table.setFilter('code', codeFilter.value.trim())
    } else {
      table.clearFilter('code')
    }
  }, 300) as unknown as number
}

function handleNameFilter() {
  clearTimeout(nameFilterTimeout)
  nameFilterTimeout = setTimeout(() => {
    if (nameFilter.value.trim()) {
      table.setFilter('name', nameFilter.value.trim())
    } else {
      table.clearFilter('name')
    }
  }, 300) as unknown as number
}

// DataGrid specific functions
function clearFilters() {
  codeFilter.value = ''
  nameFilter.value = ''
  table.clearFilter('code')
  table.clearFilter('name')
}

interface ActionItem {
  key: string;
  label?: string;
  icon?: string;
  variant?: 'primary' | 'secondary' | 'danger' | 'success' | 'warning';
  size?: 'small' | 'medium' | 'large';
  loading?: boolean;
  disabled?: boolean;
  tooltip?: string;
}

function getRowActions(_item: Department): ActionItem[] {
  return [
    { key: 'edit', label: 'Edit', icon: 'pi pi-pencil' },
    { key: 'delete', label: 'Delete', icon: 'pi pi-trash', variant: 'danger' as const }
  ]
}

function handleRowAction(action: ActionItem, item: Department) {
  if (action.key === 'edit') {
    openEditModal(item)
  } else if (action.key === 'delete') {
    handleDelete(item)
  }
}

// Modal functions
function openCreateModal() {
  selectedDepartment.value = null
  showModal.value = true
}

function openEditModal(department: Department) {
  selectedDepartment.value = department
  showModal.value = true
}

function closeModal() {
  showModal.value = false
  selectedDepartment.value = null
}

async function handleSave(data: DepartmentFormData) {
  modalLoading.value = true
  try {
    if (selectedDepartment.value) {
      // Edit existing department
      await table.update(selectedDepartment.value.id, data)
      toast.success('Department updated successfully')
    } else {
      // Create new department
      await table.create(data)
      toast.success('Department created successfully')
    }
    closeModal()
  } catch (error) {
    console.error('Error saving department:', error)
    toast.error('Failed to save department')
  } finally {
    modalLoading.value = false
  }
}

// Delete functionality
function handleDelete(department: Department) {
  departmentToDelete.value = department
  showConfirmDialog.value = true
}

async function confirmDelete() {
  if (departmentToDelete.value) {
    confirmDialogLoading.value = true
    try {
      await table.remove(departmentToDelete.value.id)
      toast.success('Department deleted successfully')
    } catch (error) {
      console.error('Error deleting department:', error)
      toast.error('Failed to delete department')
    } finally {
      confirmDialogLoading.value = false
    }
  }
  cancelDelete()
}

function cancelDelete() {
  showConfirmDialog.value = false
  departmentToDelete.value = null
}

// Initialize
onMounted(() => {
  table.fetch()
})
</script>

<style scoped>
.departments-view {
  height: 100%;
  display: flex;
  flex-direction: column;
  gap: 1.5rem;
}

.name-cell {
  display: flex;
  align-items: center;
  gap: 0.5rem;
}
</style>
