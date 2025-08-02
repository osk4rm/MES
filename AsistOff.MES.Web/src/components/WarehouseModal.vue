<template>
  <div v-if="isVisible" class="modal-overlay" @click="closeModal">
    <div class="modal-container" @click.stop>
      <div class="modal-header">
        <h2 class="modal-title">
          <i class="pi pi-building"></i>
          {{ isEdit ? 'Edit Warehouse' : 'Add New Warehouse' }}
        </h2>
        <button class="modal-close" @click="closeModal">
          <i class="pi pi-times"></i>
        </button>
      </div>

      <form @submit.prevent="handleSubmit" class="modal-form">
        <div class="form-group">
          <label for="name" class="form-label">
            Name <span class="required">*</span>
          </label>
          <input
            id="name"
            v-model="form.name"
            type="text"
            class="form-input"
            :class="{ 'error': errors.name }"
            placeholder="Enter warehouse name"
            required
          />
          <span v-if="errors.name" class="error-message">{{ errors.name }}</span>
        </div>

        <div class="form-group">
          <label for="externalId" class="form-label">
            External ID
          </label>
          <input
            id="externalId"
            v-model="form.externalId"
            type="text"
            class="form-input"
            :class="{ 'error': errors.externalId }"
            placeholder="Enter external ID (optional)"
          />
          <span v-if="errors.externalId" class="error-message">{{ errors.externalId }}</span>
        </div>

        <div class="modal-actions">
          <button type="button" class="btn-secondary" @click="closeModal" :disabled="loading">
            Cancel
          </button>
          <button type="submit" class="btn-primary" :disabled="loading || !form.name.trim()">
            <i v-if="loading" class="pi pi-spin pi-spinner"></i>
            <i v-else :class="isEdit ? 'pi pi-check' : 'pi pi-plus'"></i>
            {{ loading ? 'Saving...' : (isEdit ? 'Update' : 'Create') }}
          </button>
        </div>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, nextTick, computed } from 'vue';
import type { WarehouseResponse } from '../services/warehouseService';

export interface WarehouseFormData {
  name: string;
  externalId: string;
}

interface Props {
  isVisible: boolean;
  warehouse?: WarehouseResponse | null;
  loading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  warehouse: null,
  loading: false
});

const emit = defineEmits<{
  'close': [];
  'save': [data: WarehouseFormData];
}>();

const form = ref<WarehouseFormData>({
  name: '',
  externalId: ''
});

const errors = ref<Partial<WarehouseFormData>>({});

const isEdit = computed(() => !!props.warehouse);

// Reset form when modal opens/closes or warehouse changes
watch([() => props.isVisible, () => props.warehouse], async () => {
  if (props.isVisible) {
    resetForm();
    await nextTick();
    // Focus on name input when modal opens
    const nameInput = document.getElementById('name') as HTMLInputElement;
    nameInput?.focus();
  }
});

const resetForm = () => {
  if (props.warehouse) {
    form.value = {
      name: props.warehouse.name,
      externalId: props.warehouse.externalId || ''
    };
  } else {
    form.value = {
      name: '',
      externalId: ''
    };
  }
  errors.value = {};
};

const validateForm = (): boolean => {
  errors.value = {};
  let isValid = true;

  if (!form.value.name.trim()) {
    errors.value.name = 'Name is required';
    isValid = false;
  } else if (form.value.name.trim().length < 2) {
    errors.value.name = 'Name must be at least 2 characters';
    isValid = false;
  }

  if (form.value.externalId && form.value.externalId.length > 50) {
    errors.value.externalId = 'External ID must be less than 50 characters';
    isValid = false;
  }

  return isValid;
};

const handleSubmit = () => {
  if (!validateForm()) {
    return;
  }

  const formData: WarehouseFormData = {
    name: form.value.name.trim(),
    externalId: form.value.externalId.trim() || ''
  };

  emit('save', formData);
};

const closeModal = () => {
  if (!props.loading) {
    emit('close');
  }
};

// Handle escape key
const handleKeydown = (event: KeyboardEvent) => {
  if (event.key === 'Escape' && props.isVisible && !props.loading) {
    closeModal();
  }
};

// Add/remove global keydown listener
watch(() => props.isVisible, (visible) => {
  if (visible) {
    document.addEventListener('keydown', handleKeydown);
  } else {
    document.removeEventListener('keydown', handleKeydown);
  }
});
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  justify-content: center;
  align-items: center;
  z-index: 1000;
  backdrop-filter: blur(4px);
}

.modal-container {
  background: linear-gradient(135deg, #23272b 0%, #232526 100%);
  border-radius: 16px;
  box-shadow: 
    0 20px 60px rgba(0, 0, 0, 0.6),
    0 0 0 2px rgba(252, 145, 58, 0.3);
  width: 90%;
  max-width: 500px;
  max-height: 90vh;
  overflow: hidden;
  animation: modalSlideIn 0.3s ease-out;
}

@keyframes modalSlideIn {
  from {
    opacity: 0;
    transform: translateY(-20px) scale(0.95);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}

.modal-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 1.5rem 2rem;
  border-bottom: 2px solid rgba(252, 145, 58, 0.2);
  background: linear-gradient(135deg, rgba(252, 145, 58, 0.1) 0%, rgba(249, 212, 35, 0.1) 100%);
}

.modal-title {
  font-size: 1.25rem;
  font-weight: 600;
  color: #ffe066;
  margin: 0;
  display: flex;
  align-items: center;
  gap: 0.75rem;
}

.modal-title i {
  color: #fc913a;
}

.modal-close {
  background: none;
  border: none;
  color: #ffe066;
  font-size: 1.25rem;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 6px;
  transition: all 0.2s ease;
}

.modal-close:hover {
  background: rgba(252, 145, 58, 0.2);
  color: #fc913a;
}

.modal-form {
  padding: 2rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-label {
  display: block;
  font-weight: 600;
  color: #ffe066;
  margin-bottom: 0.5rem;
  font-size: 0.9rem;
}

.required {
  color: #fc913a;
}

.form-input {
  width: 100%;
  padding: 0.75rem 1rem;
  border: 2px solid rgba(252, 145, 58, 0.3);
  border-radius: 8px;
  background: rgba(35, 39, 43, 0.7);
  color: #ffe066;
  font-size: 1rem;
  transition: all 0.2s ease;
  box-sizing: border-box;
}

.form-input:focus {
  outline: none;
  border-color: #fc913a;
  box-shadow: 0 0 0 3px rgba(252, 145, 58, 0.2);
}

.form-input.error {
  border-color: #e74c3c;
  box-shadow: 0 0 0 3px rgba(231, 76, 60, 0.2);
}

.form-input::placeholder {
  color: rgba(255, 224, 102, 0.5);
}

.error-message {
  display: block;
  color: #e74c3c;
  font-size: 0.8rem;
  margin-top: 0.5rem;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  margin-top: 2rem;
  padding-top: 1.5rem;
  border-top: 1px solid rgba(255, 224, 102, 0.1);
}

.btn-primary,
.btn-secondary {
  padding: 0.75rem 1.5rem;
  border: none;
  border-radius: 8px;
  font-weight: 600;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-size: 0.9rem;
  min-width: 120px;
  justify-content: center;
}

.btn-primary {
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  color: #23272b;
}

.btn-primary:hover:not(:disabled) {
  transform: translateY(-2px);
  box-shadow: 0 4px 12px rgba(252, 145, 58, 0.4);
}

.btn-primary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
  transform: none;
}

.btn-secondary {
  background: rgba(35, 39, 43, 0.8);
  color: #ffe066;
  border: 2px solid rgba(252, 145, 58, 0.3);
}

.btn-secondary:hover:not(:disabled) {
  background: rgba(252, 145, 58, 0.1);
  border-color: #fc913a;
}

.btn-secondary:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

/* Responsive design */
@media (max-width: 576px) {
  .modal-container {
    width: 95%;
    margin: 1rem;
  }

  .modal-header {
    padding: 1rem 1.5rem;
  }

  .modal-form {
    padding: 1.5rem;
  }

  .modal-actions {
    flex-direction: column-reverse;
  }

  .btn-primary,
  .btn-secondary {
    width: 100%;
    min-width: auto;
  }
}
</style>
