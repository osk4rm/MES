<template>
  <div class="floating-input-container">
    <input
      :id="id"
      :type="type"
      :value="modelValue"
      @input="handleInput"
      @focus="handleFocus"
      @blur="handleBlur"
      :placeholder="placeholder"
      :class="[
        'floating-input',
        size && `floating-input--${size}`,
        { 'floating-input--focused': isFocused || hasValue }
      ]"
    />
    <label 
      :for="id" 
      :class="[
        'floating-label',
        { 'floating-label--focused': isFocused || hasValue }
      ]"
    >
      {{ label }}
    </label>
    <div v-if="prefixIcon" class="input-prefix-icon">
      <i :class="prefixIcon"></i>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed } from 'vue';

interface Props {
  id?: string;
  modelValue: string;
  label: string;
  type?: string;
  placeholder?: string;
  size?: 'small' | 'medium' | 'large';
  prefixIcon?: string;
}

interface Emits {
  (e: 'update:modelValue', value: string): void;
}

const props = withDefaults(defineProps<Props>(), {
  type: 'text',
  placeholder: '',
  size: 'medium'
});

const emit = defineEmits<Emits>();

const isFocused = ref(false);

const hasValue = computed(() => {
  return props.modelValue && props.modelValue.length > 0;
});

const handleInput = (event: Event) => {
  const target = event.target as HTMLInputElement;
  emit('update:modelValue', target.value);
};

const handleFocus = () => {
  isFocused.value = true;
};

const handleBlur = () => {
  isFocused.value = false;
};
</script>

<style scoped>
.floating-input-container {
  position: relative;
  display: flex;
  flex-direction: column;
}

.floating-input {
  background: rgba(30, 41, 59, 0.8);
  border: 1px solid #475569;
  border-radius: 8px;
  padding: 16px 12px 8px 12px;
  font-size: 14px;
  color: #f1f5f9;
  transition: all 0.2s ease;
  outline: none;
  font-family: inherit;
}

.floating-input--small {
  padding: 12px 10px 6px 10px;
  font-size: 12px;
}

.floating-input--large {
  padding: 20px 16px 10px 16px;
  font-size: 16px;
}

.floating-input:focus {
  border-color: #fc913a;
  box-shadow: 0 0 0 2px rgba(252, 145, 58, 0.2);
}

.floating-input--focused {
  border-color: #fc913a;
}

.floating-input::placeholder {
  color: transparent;
}

.floating-label {
  position: absolute;
  left: 12px;
  top: 50%;
  transform: translateY(-50%);
  font-size: 14px;
  color: #94a3b8;
  transition: all 0.2s ease;
  pointer-events: none;
  background: transparent;
  padding: 0 4px;
}

.floating-input--small + .floating-label {
  left: 10px;
  font-size: 12px;
}

.floating-input--large + .floating-label {
  left: 16px;
  font-size: 16px;
}

.floating-label--focused {
  top: 0;
  transform: translateY(-50%);
  font-size: 10px;
  color: #fc913a;
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
  font-weight: 600;
}

.floating-input--small + .floating-label--focused {
  font-size: 9px;
}

.floating-input--large + .floating-label--focused {
  font-size: 12px;
}

.input-prefix-icon {
  position: absolute;
  right: 12px;
  top: 50%;
  transform: translateY(-50%);
  color: #94a3b8;
  pointer-events: none;
  font-size: 14px;
}

.floating-input--small ~ .input-prefix-icon {
  right: 10px;
  font-size: 12px;
}

.floating-input--large ~ .input-prefix-icon {
  right: 16px;
  font-size: 16px;
}

.floating-input:focus ~ .input-prefix-icon {
  color: #fc913a;
}

/* Adjust padding when prefix icon is present */
.floating-input-container:has(.input-prefix-icon) .floating-input {
  padding-right: 40px;
}

.floating-input-container:has(.input-prefix-icon) .floating-input--small {
  padding-right: 32px;
}

.floating-input-container:has(.input-prefix-icon) .floating-input--large {
  padding-right: 48px;
}
</style>
