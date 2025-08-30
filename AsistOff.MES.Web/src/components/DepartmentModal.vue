<template>
  <div v-if="isVisible" class="modal-overlay" @click="closeModal">
    <div class="modal-container" @click.stop>
      <div class="modal-header">
        <h2 class="modal-title">
          <i class="pi pi-sitemap"></i>
          {{ isEdit ? 'Edit Department' : 'Add New Department' }}
        </h2>
        <button class="modal-close" @click="closeModal">
          <i class="pi pi-times"></i>
        </button>
      </div>

      <form @submit.prevent="handleSubmit" class="modal-form">
        <div class="form-group">
          <label for="code" class="form-label">
            Code <span class="required">*</span>
          </label>
          <input
            id="code"
            v-model="form.code"
            type="text"
            class="form-input"
            :class="{ 'error': errors.code }"
            placeholder="Enter department code"
            required
          />
          <span v-if="errors.code" class="error-message">{{ errors.code }}</span>
        </div>

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
            placeholder="Enter department name"
            required
          />
          <span v-if="errors.name" class="error-message">{{ errors.name }}</span>
        </div>

        <div class="modal-actions">
          <button type="button" class="btn-secondary" @click="closeModal" :disabled="loading">
            Cancel
          </button>
          <button type="submit" class="btn-primary" :disabled="loading || !form.code.trim() || !form.name.trim()">
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
import type { DepartmentResponse } from '../services/departmentService';

export interface DepartmentFormData {
  code: string;
  name: string;
}

interface Props {
  isVisible: boolean;
  department?: DepartmentResponse | null;
  loading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  department: null,
  loading: false
});

const emit = defineEmits<{
  'close': [];
  'save': [data: DepartmentFormData];
}>();

const form = ref<DepartmentFormData>({
  code: '',
  name: ''
});

const errors = ref<Partial<DepartmentFormData>>({});

const isEdit = computed(() => !!props.department);

// Reset form when modal opens/closes or department changes
watch([() => props.isVisible, () => props.department], async () => {
  if (props.isVisible) {
    resetForm();
    await nextTick();
    // Focus on code input when modal opens
    const codeInput = document.getElementById('code');
    if (codeInput) {
      codeInput.focus();
    }
  }
});

const resetForm = () => {
  if (props.department) {
    // Edit mode - populate with existing data
    form.value = {
      code: props.department.code || '',
      name: props.department.name || ''
    };
  } else {
    // Add mode - clear form
    form.value = {
      code: '',
      name: ''
    };
  }
  errors.value = {};
};

const validateForm = (): boolean => {
  errors.value = {};
  let isValid = true;

  if (!form.value.code.trim()) {
    errors.value.code = 'Code is required';
    isValid = false;
  } else if (form.value.code.trim().length < 2) {
    errors.value.code = 'Code must be at least 2 characters';
    isValid = false;
  } else if (form.value.code.trim().length > 10) {
    errors.value.code = 'Code must be no more than 10 characters';
    isValid = false;
  }

  if (!form.value.name.trim()) {
    errors.value.name = 'Name is required';
    isValid = false;
  } else if (form.value.name.trim().length < 2) {
    errors.value.name = 'Name must be at least 2 characters';
    isValid = false;
  } else if (form.value.name.trim().length > 100) {
    errors.value.name = 'Name must be no more than 100 characters';
    isValid = false;
  }

  return isValid;
};

const handleSubmit = () => {
  if (!validateForm()) {
    return;
  }

  const formData: DepartmentFormData = {
    code: form.value.code.trim(),
    name: form.value.name.trim()
  };

  emit('save', formData);
};

const closeModal = () => {
  if (!props.loading) {
    emit('close');
  }
};
</script>

<style scoped>
.modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.6);
  backdrop-filter: blur(3px);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  padding: 1rem;
}

.modal-container {
  background: linear-gradient(135deg, #23272b 0%, #1a1e22 100%);
  border: 1px solid #374151;
  border-radius: 12px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
  min-width: 400px;
  max-width: 90vw;
  max-height: 90vh;
  overflow: hidden;
}

.modal-header {
  display: flex;
  align-items: center;
  justify-content: space-between;
  padding: 1.5rem 2rem;
  border-bottom: 1px solid #374151;
  background: rgba(252, 145, 58, 0.1);
}

.modal-title {
  margin: 0;
  font-size: 1.25rem;
  font-weight: 600;
  color: #f1f5f9;
  display: flex;
  align-items: center;
  gap: 0.5rem;
}

.modal-title i {
  color: #fc913a;
}

.modal-close {
  background: none;
  border: none;
  color: #94a3b8;
  font-size: 1.25rem;
  cursor: pointer;
  padding: 0.5rem;
  border-radius: 8px;
  transition: all 0.2s ease;
}

.modal-close:hover {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
}

.modal-form {
  padding: 2rem;
}

.form-group {
  margin-bottom: 1.5rem;
}

.form-label {
  display: block;
  margin-bottom: 0.5rem;
  color: #f1f5f9;
  font-weight: 500;
  font-size: 0.9rem;
}

.required {
  color: #ef4444;
}

.form-input {
  width: 100%;
  padding: 0.75rem;
  background: #1e293b;
  border: 1px solid #475569;
  border-radius: 8px;
  color: #f1f5f9;
  font-size: 0.9rem;
  box-sizing: border-box;
  transition: all 0.2s ease;
}

.form-input:focus {
  outline: none;
  border-color: #fc913a;
  box-shadow: 0 0 0 3px rgba(252, 145, 58, 0.1);
}

.form-input.error {
  border-color: #ef4444;
  box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.1);
}

.error-message {
  display: block;
  margin-top: 0.5rem;
  color: #ef4444;
  font-size: 0.8rem;
}

.modal-actions {
  display: flex;
  gap: 1rem;
  justify-content: flex-end;
  margin-top: 2rem;
  padding-top: 1.5rem;
  border-top: 1px solid #374151;
}

.btn-secondary,
.btn-primary {
  padding: 0.75rem 1.5rem;
  border-radius: 8px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  min-width: 100px;
  justify-content: center;
}

.btn-secondary {
  background: transparent;
  border: 1px solid #475569;
  color: #94a3b8;
}

.btn-secondary:hover:not(:disabled) {
  background: #374151;
  color: #f1f5f9;
}

.btn-primary {
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  border: none;
  color: #1a1e22;
  font-weight: 600;
}

.btn-primary:hover:not(:disabled) {
  transform: translateY(-1px);
  box-shadow: 0 4px 12px rgba(252, 145, 58, 0.4);
}

.btn-secondary:disabled,
.btn-primary:disabled {
  opacity: 0.5;
  cursor: not-allowed;
  transform: none;
}
</style>
