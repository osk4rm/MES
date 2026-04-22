<template>
  <span :class="badgeClasses">
    <i v-if="icon" :class="icon"></i>
    <span v-if="$slots.default || label" class="badge-text">
      <slot>{{ label }}</slot>
    </span>
    <i v-if="loading" class="pi pi-spin pi-spinner loading-icon"></i>
  </span>
</template>

<script setup lang="ts">
import { computed, useSlots } from 'vue';

interface Props {
  variant?: 'success' | 'warning' | 'danger' | 'info' | 'neutral' | 'active' | 'inactive';
  size?: 'small' | 'medium' | 'large';
  icon?: string;
  label?: string;
  loading?: boolean;
  pulse?: boolean;
  outlined?: boolean;
}

const props = withDefaults(defineProps<Props>(), {
  variant: 'neutral',
  size: 'medium',
  loading: false,
  pulse: false,
  outlined: false
});

const slots = useSlots();

const badgeClasses = computed(() => [
  'status-badge',
  `status-badge--${props.variant}`,
  `status-badge--${props.size}`,
  {
    'status-badge--loading': props.loading,
    'status-badge--pulse': props.pulse,
    'status-badge--outlined': props.outlined,
    'status-badge--icon-only': props.icon && !props.label && !slots.default
  }
]);
</script>

<style scoped>
.status-badge {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  padding: 6px 12px;
  border-radius: 20px;
  font-size: 12px;
  font-weight: 600;
  text-transform: uppercase;
  letter-spacing: 0.5px;
  border: 1px solid transparent;
  transition: all 0.2s ease;
  position: relative;
  overflow: hidden;
}

/* Sizes */
.status-badge--small {
  padding: 4px 8px;
  font-size: 10px;
  gap: 4px;
}

.status-badge--medium {
  padding: 6px 12px;
  font-size: 12px;
  gap: 6px;
}

.status-badge--large {
  padding: 8px 16px;
  font-size: 14px;
  gap: 8px;
}

/* Success variant */
.status-badge--success {
  background: linear-gradient(135deg, #10b981 0%, #059669 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(16, 185, 129, 0.3);
}

.status-badge--success.status-badge--outlined {
  background: rgba(16, 185, 129, 0.1);
  color: #10b981;
  border-color: #10b981;
}

/* Warning variant */
.status-badge--warning {
  background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(245, 158, 11, 0.3);
}

.status-badge--warning.status-badge--outlined {
  background: rgba(245, 158, 11, 0.1);
  color: #f59e0b;
  border-color: #f59e0b;
}

/* Danger variant */
.status-badge--danger {
  background: linear-gradient(135deg, #ef4444 0%, #dc2626 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(239, 68, 68, 0.3);
}

.status-badge--danger.status-badge--outlined {
  background: rgba(239, 68, 68, 0.1);
  color: #ef4444;
  border-color: #ef4444;
}

/* Info variant */
.status-badge--info {
  background: linear-gradient(135deg, #3b82f6 0%, #2563eb 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(59, 130, 246, 0.3);
}

.status-badge--info.status-badge--outlined {
  background: rgba(59, 130, 246, 0.1);
  color: #3b82f6;
  border-color: #3b82f6;
}

/* Neutral variant */
.status-badge--neutral {
  background: linear-gradient(135deg, #64748b 0%, #475569 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(100, 116, 139, 0.3);
}

.status-badge--neutral.status-badge--outlined {
  background: rgba(100, 116, 139, 0.1);
  color: #64748b;
  border-color: #64748b;
}

/* Active variant */
.status-badge--active {
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  color: white;
  box-shadow: 0 2px 8px rgba(139, 92, 246, 0.3);
}

.status-badge--active.status-badge--outlined {
  background: rgba(139, 92, 246, 0.1);
  color: #fc913a;
  border-color: #fc913a;
}

/* Inactive variant */
.status-badge--inactive {
  background: linear-gradient(135deg, #6b7280 0%, #4b5563 100%);
  color: #d1d5db;
  box-shadow: 0 2px 8px rgba(107, 114, 128, 0.3);
}

.status-badge--inactive.status-badge--outlined {
  background: rgba(107, 114, 128, 0.1);
  color: #6b7280;
  border-color: #6b7280;
}

/* States */
.status-badge--loading {
  animation: loadingShimmer 2s ease-in-out infinite;
}

.status-badge--pulse {
  animation: badgePulse 2s ease-in-out infinite;
}

/* Icon-only badge */
.status-badge--icon-only {
  padding: 8px;
  border-radius: 50%;
}

.status-badge--icon-only.status-badge--small {
  padding: 6px;
}

.status-badge--icon-only.status-badge--large {
  padding: 10px;
}

.badge-text {
  line-height: 1;
}

.loading-icon {
  font-size: 0.9em;
}

/* Animations */
@keyframes loadingShimmer {
  0% {
    opacity: 1;
  }
  50% {
    opacity: 0.7;
  }
  100% {
    opacity: 1;
  }
}

@keyframes badgePulse {
  0% {
    transform: scale(1);
    box-shadow: 0 0 0 0 rgba(139, 92, 246, 0.7);
  }
  70% {
    transform: scale(1.05);
    box-shadow: 0 0 0 10px rgba(139, 92, 246, 0);
  }
  100% {
    transform: scale(1);
    box-shadow: 0 0 0 0 rgba(139, 92, 246, 0);
  }
}

/* Industrial glow effect */
.status-badge::before {
  content: '';
  position: absolute;
  top: 0;
  left: -100%;
  width: 100%;
  height: 100%;
  background: linear-gradient(90deg, transparent, rgba(255, 255, 255, 0.2), transparent);
  transition: left 0.5s;
}

.status-badge:hover::before {
  left: 100%;
}
</style>
