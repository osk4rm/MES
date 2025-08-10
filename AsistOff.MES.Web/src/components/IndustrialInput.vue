<template>
  <div class="industrial-input-wrapper">
    <label v-if="label" :for="inputId" class="input-label">
      {{ label }}
      <span v-if="required" class="required-indicator">*</span>
    </label>
    
    <div class="input-container">
      <i v-if="prefixIcon" :class="prefixIcon" class="prefix-icon"></i>
      
      <input
        :id="inputId"
        :type="type"
        :placeholder="placeholder"
        :value="modelValue"
        :disabled="disabled"
        :readonly="readonly"
        :class="inputClasses"
        @input="handleInput"
        @focus="handleFocus"
        @blur="handleBlur"
        @keydown="handleKeydown"
      />
      
      <i v-if="suffixIcon" :class="suffixIcon" class="suffix-icon"></i>
      
      <div v-if="loading" class="loading-indicator">
        <i class="pi pi-spin pi-spinner"></i>
      </div>
    </div>
    
    <div v-if="error" class="input-error">
      <i class="pi pi-exclamation-triangle"></i>
      {{ error }}
    </div>
    
    <div v-else-if="helper" class="input-helper">
      {{ helper }}
    </div>
  </div>
</template>

<script setup lang="ts">
import { computed, ref } from 'vue';

interface Props {
  modelValue?: string | number;
  type?: 'text' | 'email' | 'password' | 'number' | 'tel' | 'url' | 'search';
  label?: string;
  placeholder?: string;
  prefixIcon?: string;
  suffixIcon?: string;
  error?: string;
  helper?: string;
  disabled?: boolean;
  readonly?: boolean;
  loading?: boolean;
  required?: boolean;
  size?: 'small' | 'medium' | 'large';
}

const props = withDefaults(defineProps<Props>(), {
  type: 'text',
  size: 'medium',
  disabled: false,
  readonly: false,
  loading: false,
  required: false
});

const emit = defineEmits<{
  'update:modelValue': [value: string | number];
  focus: [event: FocusEvent];
  blur: [event: FocusEvent];
  keydown: [event: KeyboardEvent];
}>();

const inputId = ref(`input-${Math.random().toString(36).substr(2, 9)}`);
const isFocused = ref(false);

const inputClasses = computed(() => [
  'industrial-input',
  `industrial-input--${props.size}`,
  {
    'industrial-input--error': props.error,
    'industrial-input--disabled': props.disabled,
    'industrial-input--readonly': props.readonly,
    'industrial-input--focused': isFocused.value,
    'industrial-input--with-prefix': props.prefixIcon,
    'industrial-input--with-suffix': props.suffixIcon || props.loading
  }
]);

const handleInput = (event: Event) => {
  const target = event.target as HTMLInputElement;
  const value = props.type === 'number' ? parseFloat(target.value) || 0 : target.value;
  emit('update:modelValue', value);
};

const handleFocus = (event: FocusEvent) => {
  isFocused.value = true;
  emit('focus', event);
};

const handleBlur = (event: FocusEvent) => {
  isFocused.value = false;
  emit('blur', event);
};

const handleKeydown = (event: KeyboardEvent) => {
  emit('keydown', event);
};
</script>

<style scoped>
.industrial-input-wrapper {
  display: flex;
  flex-direction: column;
  gap: 6px;
  width: 100%;
}

.input-label {
  display: flex;
  align-items: center;
  gap: 4px;
  font-size: 14px;
  font-weight: 500;
  color: #e2e8f0;
  cursor: pointer;
}

.required-indicator {
  color: #ef4444;
  font-weight: 600;
}

.input-container {
  position: relative;
  display: flex;
  align-items: center;
}

.industrial-input {
  width: 100%;
  padding: 12px 16px;
  background: #1e293b;
  border: 2px solid #475569;
  border-radius: 8px;
  color: #f1f5f9;
  font-size: 14px;
  font-family: inherit;
  transition: all 0.2s ease;
  box-shadow: inset 0 2px 4px rgba(0, 0, 0, 0.1);
}

.industrial-input::placeholder {
  color: #94a3b8;
}

.industrial-input:focus {
  outline: none;
  border-color: #fc913a;
  box-shadow: 0 0 0 3px rgba(139, 92, 246, 0.1), inset 0 2px 4px rgba(0, 0, 0, 0.1);
  background: #0f172a;
}

/* Sizes */
.industrial-input--small {
  padding: 8px 12px;
  font-size: 12px;
}

.industrial-input--medium {
  padding: 12px 16px;
  font-size: 14px;
}

.industrial-input--large {
  padding: 16px 20px;
  font-size: 16px;
}

/* States */
.industrial-input--error {
  border-color: #ef4444;
  background: rgba(239, 68, 68, 0.05);
}

.industrial-input--error:focus {
  border-color: #ef4444;
  box-shadow: 0 0 0 3px rgba(239, 68, 68, 0.1), inset 0 2px 4px rgba(0, 0, 0, 0.1);
}

.industrial-input--disabled {
  opacity: 0.6;
  cursor: not-allowed;
  background: #374151;
}

.industrial-input--readonly {
  background: #374151;
  cursor: default;
}

/* With icons */
.industrial-input--with-prefix {
  padding-left: 44px;
}

.industrial-input--with-suffix {
  padding-right: 44px;
}

.prefix-icon,
.suffix-icon {
  position: absolute;
  top: 50%;
  transform: translateY(-50%);
  color: #94a3b8;
  font-size: 16px;
  pointer-events: none;
  z-index: 1;
}

.prefix-icon {
  left: 14px;
}

.suffix-icon {
  right: 14px;
}

.loading-indicator {
  position: absolute;
  right: 14px;
  top: 50%;
  transform: translateY(-50%);
  color: #fc913a;
  font-size: 14px;
}

.input-error {
  display: flex;
  align-items: center;
  gap: 6px;
  font-size: 12px;
  color: #ef4444;
  font-weight: 500;
}

.input-helper {
  font-size: 12px;
  color: #94a3b8;
  font-weight: 400;
}

/* Focus glow effect */
.industrial-input--focused {
  background: #0f172a;
  box-shadow: 0 0 20px rgba(139, 92, 246, 0.2), inset 0 2px 4px rgba(0, 0, 0, 0.1);
}

/* Industrial theme enhancement */
.industrial-input {
  position: relative;
}

.industrial-input::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  height: 1px;
  background: linear-gradient(135deg, transparent 0%, rgba(139, 92, 246, 0.3) 50%, transparent 100%);
  opacity: 0;
  transition: opacity 0.2s ease;
}

.industrial-input:focus::before {
  opacity: 1;
}
</style>
