<template>
  <div v-if="isVisible" class="confirm-overlay" @click="handleOverlayClick">
    <div class="confirm-dialog" @click.stop>
      <div class="confirm-header">
        <i class="pi pi-exclamation-triangle confirm-icon"></i>
        <h3 class="confirm-title">{{ title }}</h3>
      </div>
      
      <div class="confirm-body">
        <p class="confirm-message">{{ message }}</p>
        <p v-if="details" class="confirm-details">{{ details }}</p>
      </div>
      
      <div class="confirm-actions">
        <button 
          type="button" 
          class="btn btn-secondary" 
          @click="handleCancel"
          :disabled="loading"
        >
          {{ cancelText }}
        </button>
        <button 
          type="button" 
          class="btn btn-danger" 
          @click="handleConfirm"
          :disabled="loading"
        >
          <i v-if="loading" class="pi pi-spin pi-spinner"></i>
          {{ confirmText }}
        </button>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { defineProps, defineEmits } from 'vue';

interface Props {
  isVisible: boolean;
  title?: string;
  message: string;
  details?: string;
  confirmText?: string;
  cancelText?: string;
  loading?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  title: 'Confirm Action',
  confirmText: 'Confirm',
  cancelText: 'Cancel',
  loading: false
});

const emit = defineEmits<{
  confirm: [];
  cancel: [];
}>();

const handleConfirm = () => {
  if (!props.loading) {
    emit('confirm');
  }
};

const handleCancel = () => {
  if (!props.loading) {
    emit('cancel');
  }
};

const handleOverlayClick = () => {
  if (!props.loading) {
    emit('cancel');
  }
};
</script>

<style scoped>
.confirm-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background-color: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
  animation: fadeIn 0.2s ease-out;
}

.confirm-dialog {
  background: #1e293b;
  border-radius: 12px;
  box-shadow: 0 20px 40px rgba(0, 0, 0, 0.3);
  max-width: 480px;
  width: 90%;
  max-height: 90vh;
  overflow: hidden;
  animation: slideIn 0.3s ease-out;
  border: 1px solid #334155;
}

.confirm-header {
  display: flex;
  align-items: center;
  gap: 12px;
  padding: 24px 24px 16px;
  border-bottom: 1px solid #334155;
}

.confirm-icon {
  font-size: 24px;
  color: #f59e0b;
  flex-shrink: 0;
}

.confirm-title {
  margin: 0;
  font-size: 20px;
  font-weight: 600;
  color: #f1f5f9;
}

.confirm-body {
  padding: 16px 24px 24px;
}

.confirm-message {
  margin: 0 0 8px 0;
  font-size: 16px;
  color: #cbd5e1;
  line-height: 1.5;
}

.confirm-details {
  margin: 0;
  font-size: 14px;
  color: #94a3b8;
  line-height: 1.4;
}

.confirm-actions {
  display: flex;
  gap: 12px;
  padding: 0 24px 24px;
  justify-content: flex-end;
}

.btn {
  padding: 10px 20px;
  border: none;
  border-radius: 6px;
  font-size: 14px;
  font-weight: 500;
  cursor: pointer;
  transition: all 0.2s ease;
  display: flex;
  align-items: center;
  gap: 6px;
  min-width: 80px;
  justify-content: center;
}

.btn:disabled {
  opacity: 0.6;
  cursor: not-allowed;
}

.btn-secondary {
  background-color: #475569;
  color: #e2e8f0;
  border: 1px solid #64748b;
}

.btn-secondary:hover:not(:disabled) {
  background-color: #64748b;
  color: #f1f5f9;
}

.btn-danger {
  background-color: #dc2626;
  color: white;
}

.btn-danger:hover:not(:disabled) {
  background-color: #b91c1c;
}

@keyframes fadeIn {
  from {
    opacity: 0;
  }
  to {
    opacity: 1;
  }
}

@keyframes slideIn {
  from {
    opacity: 0;
    transform: translateY(-20px) scale(0.95);
  }
  to {
    opacity: 1;
    transform: translateY(0) scale(1);
  }
}
</style>
