<template>
  <div class="custom-float-label">
    <input
      :id="id"
      :type="type"
      :value="modelValue"
      :required="required"
      class="custom-input"
      @input="emits('update:modelValue', ($event.target as HTMLInputElement)?.value)"
      @focus="focused = true"
      @blur="focused = false"
    />
    <label
      :for="id"
      :class="{ active: focused || modelValue }"
    >{{ label }}</label>
  </div>
</template>

<script setup lang="ts">
import { ref, watch, defineProps, defineEmits } from 'vue'
const props = defineProps({
  id: String,
  label: String,
  type: { type: String, default: 'text' },
  required: { type: Boolean, default: false },
  modelValue: String
})
const emits = defineEmits(['update:modelValue'])
const focused = ref(false)


</script>

<style scoped>
.custom-float-label {
  position: relative;
  width: 100%;
  margin-bottom: 1.2rem;
}
.custom-input {
  width: 100%;
  background: #2c3136;
  border: 1px solid #444;
  color: #fff;
  border-radius: 6px;
  padding: 0.75rem 1rem 0.75rem 1rem;
  font-size: 1rem;
  outline: none;
  transition: border 0.2s;
}
.custom-input:focus {
  border-color: #f9d423;
}
.custom-float-label label {
  position: absolute;
  left: 1rem;
  top: 50%;
  transform: translateY(-50%);
  color: #ffe066;
  font-size: 1rem;
  font-weight: 500;
  pointer-events: none;
  background: transparent;
  transition: all 0.2s;
}
.custom-float-label label.active {
  top: -0.2rem;
  left: 0.8rem;
  font-size: 0.85rem;
  color: #f9d423;
  background: transparent;
  padding: 0 0.3rem;
  z-index: 1;
}
.custom-float-label label.active::before {
  content: '';
  position: absolute;
  left: 0;
  top: 50%;
  transform: translateY(-50%);
  width: 100%;
  height: 1.2em;
  background: #2c3136;
  z-index: -1;
  border-radius: 2px;
}
</style>
