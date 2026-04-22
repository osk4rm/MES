<template>
  <div class="page-header">
    <div class="header-left">
      <h1 class="page-title">
        <i v-if="icon" :class="icon"></i>
        {{ title }}
      </h1>
      <p v-if="subtitle" class="page-subtitle">{{ subtitle }}</p>
    </div>
    <div v-if="$slots.actions" class="header-actions">
      <slot name="actions"></slot>
    </div>
  </div>
</template>

<script setup lang="ts">
interface Props {
  title: string;
  subtitle?: string;
  icon?: string;
}

defineProps<Props>();
</script>

<style scoped>
.page-header {
  display: flex;
  justify-content: space-between;
  align-items: flex-start;
  padding: 24px 0;
  margin-bottom: 32px;
  border-bottom: 2px solid #334155;
  background: linear-gradient(135deg, rgba(99, 102, 241, 0.05) 0%, rgba(139, 92, 246, 0.03) 100%);
  border-radius: 8px 8px 0 0;
  position: relative;
}

.page-header::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 3px;
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  border-radius: 8px 8px 0 0;
}

.header-left {
  flex: 1;
}

.page-title {
  display: flex;
  align-items: center;
  gap: 12px;
  margin: 0;
  font-size: 28px;
  font-weight: 700;
  color: #f1f5f9;
  text-shadow: 0 2px 8px rgba(0, 0, 0, 0.3);
  letter-spacing: -0.025em;
}

.page-title i {
  font-size: 32px;
  color: #fc913a;
  filter: drop-shadow(0 2px 8px rgba(139, 92, 246, 0.4));
}

.page-subtitle {
  margin: 8px 0 0 44px;
  font-size: 16px;
  color: #94a3b8;
  line-height: 1.5;
  font-weight: 400;
}

.header-actions {
  display: flex;
  gap: 12px;
  align-items: center;
  flex-shrink: 0;
}

/* Responsive design */
@media (max-width: 768px) {
  .page-header {
    flex-direction: column;
    gap: 20px;
    align-items: stretch;
  }

  .page-title {
    font-size: 24px;
  }

  .page-title i {
    font-size: 28px;
  }

  .page-subtitle {
    margin-left: 40px;
    font-size: 14px;
  }

  .header-actions {
    justify-content: flex-start;
    flex-wrap: wrap;
  }
}
</style>
