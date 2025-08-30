<template>
  <div class="action-buttons">
    <IndustrialButton
      v-for="action in actions"
      :key="action.key"
      :variant="action.variant || 'secondary'"
      :size="action.size || 'small'"
      :icon="action.icon"
      :loading="action.loading"
      :disabled="action.disabled"
      :title="action.tooltip"
      @click="handleAction(action)"
      class="action-button"
    >
      {{ action.label }}
    </IndustrialButton>
  </div>
</template>

<script setup lang="ts">
import IndustrialButton from './IndustrialButton.vue';

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

interface Props {
  actions: ActionItem[];
}

defineProps<Props>();

const emit = defineEmits<{
  action: [action: ActionItem];
}>();

const handleAction = (action: ActionItem) => {
  if (!action.disabled && !action.loading) {
    emit('action', action);
  }
};
</script>

<style scoped>
.action-buttons {
  display: flex;
  gap: 8px;
  align-items: center;
  flex-wrap: wrap;
}

.action-button {
  flex-shrink: 0;
}

@media (max-width: 480px) {
  .action-buttons {
    flex-direction: column;
    align-items: stretch;
    gap: 6px;
  }
}
</style>
