<template>
  <AppButton variant="ghost" :title="titleText" @click="toggleDensity">
    {{ $t('shopfloor.density.label') }}: {{ densityLabel }}
  </AppButton>
</template>

<script setup lang="ts">
import { computed } from 'vue';
import { useI18n } from 'vue-i18n';
import AppButton from '../ui/AppButton.vue';
import { ShopfloorDensity, useShopfloorDensity } from '../../composables/useShopfloorDisplay';

// Shared shopfloor density affordance (F-14): one toggle used by every
// operator-facing view so the 44 px touch minimum (comfortable, the
// default) applies consistently. Compact restores desktop-sized targets
// for back-office use on the same views. Density persists per operator
// via localStorage in `useShopfloorDensity`.
const { density, toggleDensity } = useShopfloorDensity();

const { t } = useI18n();

const densityLabel = computed(() => t(density.value === ShopfloorDensity.Compact
  ? 'shopfloor.density.compact'
  : 'shopfloor.density.comfortable'));

const titleText = computed(() => t('shopfloor.density.toggleHint'));

defineExpose({ density, toggleDensity });
</script>
