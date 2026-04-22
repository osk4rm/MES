<template>
  <div :class="cardClasses" @click="handleClick">
    <div v-if="loading" class="widget-loading">
      <i class="pi pi-spin pi-spinner"></i>
    </div>
    
    <div v-else class="widget-content">
      <div class="widget-header">
        <div class="widget-icon-container">
          <i v-if="icon" :class="icon" class="widget-icon"></i>
        </div>
        <div class="widget-info">
          <h3 class="widget-title">{{ title }}</h3>
          <p v-if="subtitle" class="widget-subtitle">{{ subtitle }}</p>
        </div>
        <StatusBadge 
          v-if="status" 
          :variant="status.variant" 
          :icon="status.icon"
          :pulse="status.pulse"
        >
          {{ status.label }}
        </StatusBadge>
      </div>
      
      <div v-if="value !== undefined" class="widget-value">
        <span class="value-number">{{ formattedValue }}</span>
        <span v-if="unit" class="value-unit">{{ unit }}</span>
      </div>
      
      <div v-if="$slots.default" class="widget-body">
        <slot></slot>
      </div>
      
      <div v-if="actions.length > 0" class="widget-actions">
        <ActionButtons :actions="actions" @action="handleAction" />
      </div>
    </div>
    
    <div class="widget-glow"></div>
  </div>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import StatusBadge from './StatusBadge.vue';
import ActionButtons from './ActionButtons.vue';

interface WidgetStatus {
  variant: 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'active' | 'inactive';
  label: string;
  icon?: string;
  pulse?: boolean;
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

interface Props {
  title: string;
  subtitle?: string;
  icon?: string;
  value?: number | string;
  unit?: string;
  status?: WidgetStatus;
  loading?: boolean;
  clickable?: boolean;
  variant?: 'default' | 'primary' | 'success' | 'warning' | 'danger';
  actions?: ActionItem[];
}

const props = withDefaults(defineProps<Props>(), {
  loading: false,
  clickable: false,
  variant: 'default',
  actions: () => []
});

const emit = defineEmits<{
  click: [];
  action: [action: ActionItem];
}>();

const cardClasses = computed(() => [
  'widget-card',
  `widget-card--${props.variant}`,
  {
    'widget-card--loading': props.loading,
    'widget-card--clickable': props.clickable
  }
]);

const formattedValue = computed(() => {
  if (typeof props.value === 'number') {
    return new Intl.NumberFormat().format(props.value);
  }
  return props.value;
});

const handleClick = () => {
  if (props.clickable && !props.loading) {
    emit('click');
  }
};

const handleAction = (action: ActionItem) => {
  emit('action', action);
};
</script>

<style scoped>
.widget-card {
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
  border: 1px solid #475569;
  border-radius: 12px;
  padding: 24px;
  position: relative;
  transition: all 0.3s ease;
  overflow: hidden;
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.1);
}

.widget-card::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: linear-gradient(135deg, #64748b 0%, #fc913a 100%);
  border-radius: 12px 12px 0 0;
}

.widget-card--clickable {
  cursor: pointer;
}

.widget-card--clickable:hover {
  transform: translateY(-2px);
  box-shadow: 0 8px 24px rgba(0, 0, 0, 0.15);
  border-color: #64748b;
}

/* Variants */
.widget-card--primary::before {
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
}

.widget-card--success::before {
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
}

.widget-card--warning::before {
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
}

.widget-card--danger::before {
  background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%);
}

.widget-loading {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100px;
  color: #fc913a;
  font-size: 24px;
}

.widget-content {
  position: relative;
  z-index: 1;
}

.widget-header {
  display: flex;
  align-items: flex-start;
  gap: 16px;
  margin-bottom: 16px;
}

.widget-icon-container {
  flex-shrink: 0;
}

.widget-icon {
  font-size: 32px;
  color: #fc913a;
  filter: drop-shadow(0 2px 8px rgba(252, 145, 58, 0.3));
}

.widget-info {
  flex: 1;
  min-width: 0;
}

.widget-title {
  margin: 0 0 4px 0;
  font-size: 18px;
  font-weight: 600;
  color: #f1f5f9;
  line-height: 1.3;
}

.widget-subtitle {
  margin: 0;
  font-size: 14px;
  color: #94a3b8;
  line-height: 1.4;
}

.widget-value {
  display: flex;
  align-items: baseline;
  gap: 8px;
  margin-bottom: 16px;
}

.value-number {
  font-size: 36px;
  font-weight: 700;
  color: #f1f5f9;
  line-height: 1;
  background: linear-gradient(135deg, #f1f5f9 0%, #fc913a 100%);
  background-clip: text;
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.value-unit {
  font-size: 16px;
  font-weight: 500;
  color: #94a3b8;
  text-transform: uppercase;
  letter-spacing: 0.5px;
}

.widget-body {
  margin-bottom: 16px;
  color: #cbd5e1;
  line-height: 1.5;
}

.widget-actions {
  padding-top: 16px;
  border-top: 1px solid #475569;
}

.widget-glow {
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: radial-gradient(circle at 50% 0%, rgba(139, 92, 246, 0.1) 0%, transparent 50%);
  pointer-events: none;
  opacity: 0;
  transition: opacity 0.3s ease;
}

.widget-card:hover .widget-glow {
  opacity: 1;
}

/* Loading state */
.widget-card--loading {
  min-height: 200px;
}

.widget-card--loading::before {
  animation: loadingGlow 2s ease-in-out infinite;
}

@keyframes loadingGlow {
  0%, 100% {
    opacity: 0.5;
  }
  50% {
    opacity: 1;
  }
}

/* Responsive design */
@media (max-width: 768px) {
  .widget-card {
    padding: 16px;
  }

  .widget-header {
    flex-direction: column;
    gap: 12px;
  }

  .widget-icon {
    font-size: 28px;
  }

  .widget-title {
    font-size: 16px;
  }

  .value-number {
    font-size: 28px;
  }
}
</style>
