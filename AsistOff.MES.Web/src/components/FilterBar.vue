<template>
  <div class="filter-bar">
    <div class="filter-inputs">
      <slot></slot>
    </div>
    <div v-if="showClearButton" class="filter-actions">
      <IndustrialButton
        variant="secondary"
        size="small"
        icon="pi pi-times"
        @click="handleClear"
      >
        Clear
      </IndustrialButton>
    </div>
  </div>
</template>

<script setup lang="ts">
import IndustrialButton from './IndustrialButton.vue';

interface Props {
  showClearButton?: boolean;
}

withDefaults(defineProps<Props>(), {
  showClearButton: true
});

const emit = defineEmits<{
  clear: [];
}>();

const handleClear = () => {
  emit('clear');
};
</script>

<style scoped>
.filter-bar {
  display: flex;
  align-items: center;
  gap: 16px;
  padding: 16px;
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
  border: 1px solid #475569;
  border-radius: 8px;
  margin-bottom: 20px;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.15);
  position: relative;
}

.filter-bar::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 2px;
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  border-radius: 8px 8px 0 0;
}

.filter-inputs {
  display: flex;
  align-items: center;
  gap: 12px;
  flex: 1;
  flex-wrap: nowrap;
}

.filter-inputs :deep(.industrial-input-wrapper) {
  width: 200px;
  min-width: 180px;
  max-width: 250px;
}

.filter-actions {
  display: flex;
  align-items: center;
  gap: 8px;
  flex-shrink: 0;
}

/* Responsive design */
@media (max-width: 768px) {
  .filter-bar {
    flex-direction: column;
    align-items: stretch;
    gap: 12px;
  }

  .filter-inputs {
    flex-direction: row;
    flex-wrap: wrap;
    align-items: center;
    gap: 8px;
  }

  .filter-inputs :deep(.industrial-input-wrapper) {
    width: 150px;
    min-width: 120px;
  }

  .filter-actions {
    justify-content: center;
  }
}
</style>
