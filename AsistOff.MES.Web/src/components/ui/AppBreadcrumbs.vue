<template>
  <nav class="app-breadcrumbs" aria-label="Breadcrumb">
    <ol>
      <li v-for="(crumb, idx) in items" :key="idx" class="app-breadcrumbs__item">
        <router-link v-if="crumb.to && idx < items.length - 1" :to="crumb.to">{{ crumb.label }}</router-link>
        <span v-else>{{ crumb.label }}</span>
        <i v-if="idx < items.length - 1" class="pi pi-angle-right app-breadcrumbs__sep" aria-hidden="true"></i>
      </li>
    </ol>
  </nav>
</template>

<script setup lang="ts">
export interface Crumb { label: string; to?: string }
defineProps<{ items: Crumb[] }>();
</script>

<style scoped>
.app-breadcrumbs { font-size: var(--font-size-sm); color: var(--color-text-muted); margin-bottom: var(--space-1); }
.app-breadcrumbs ol { display: flex; align-items: center; gap: var(--space-1); flex-wrap: wrap; }
.app-breadcrumbs__item { display: inline-flex; align-items: center; gap: var(--space-1); }
.app-breadcrumbs__item a { color: var(--color-text-muted); }
.app-breadcrumbs__item a:hover { color: var(--color-primary); text-decoration: none; }
.app-breadcrumbs__item span { color: var(--color-text); }
.app-breadcrumbs__sep { font-size: 0.7em; color: var(--color-text-subtle); }
</style>
