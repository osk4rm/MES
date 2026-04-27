<template>
  <div class="attachments-panel">
    <div class="attachments-panel__header">
      <strong>{{ $t('attachments.title') }}</strong>
      <label class="upload-btn">
        <i class="pi pi-upload" />
        {{ $t('attachments.upload') }}
        <input type="file" @change="onFileChange" :disabled="uploading" />
      </label>
    </div>

    <ul class="attachments-list" v-if="items.length > 0">
      <li v-for="a in items" :key="a.id" class="attachment">
        <i class="pi pi-file" />
        <div class="attachment__info">
          <div class="attachment__name">{{ a.fileName }}</div>
          <div class="attachment__meta">{{ formatSize(a.sizeBytes) }} · {{ formatDate(a.createdAt) }}</div>
        </div>
        <a :href="downloadHref(a.id)" target="_blank" rel="noopener" class="attachment__action">
          <i class="pi pi-download" />
        </a>
        <button type="button" class="attachment__action attachment__action--danger" @click="remove(a.id)">
          <i class="pi pi-times" />
        </button>
      </li>
    </ul>
    <div v-else class="muted small">{{ $t('attachments.empty') }}</div>
  </div>
</template>

<script setup lang="ts">
import { onMounted, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import { attachmentService, type AttachmentResponse } from '../../services/attachmentService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const props = defineProps<{ ownerType: string; ownerId: string }>();
const { t } = useI18n();
const toast = useToastStore();

const items = ref<AttachmentResponse[]>([]);
const uploading = ref(false);

async function load() {
  try {
    items.value = await attachmentService.list(props.ownerType, props.ownerId);
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  }
}

async function onFileChange(e: Event) {
  const input = e.target as HTMLInputElement;
  const file = input.files?.[0];
  if (!file) return;
  uploading.value = true;
  try {
    await attachmentService.upload(props.ownerType, props.ownerId, file);
    toast.success(t('toasts.created'));
    input.value = '';
    await load();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    uploading.value = false;
  }
}

async function remove(id: string) {
  if (!confirm(t('attachments.confirmDelete'))) return;
  try {
    await attachmentService.remove(id);
    toast.success(t('toasts.deleted'));
    await load();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  }
}

function downloadHref(id: string) { return attachmentService.downloadUrl(id); }
function formatSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  return `${(bytes / 1024 / 1024).toFixed(2)} MB`;
}
function formatDate(d: string): string {
  return new Date(d).toLocaleString();
}

watch(() => [props.ownerType, props.ownerId], load);
onMounted(load);
</script>

<style scoped>
.attachments-panel { display: flex; flex-direction: column; gap: var(--space-2); }
.attachments-panel__header { display: flex; justify-content: space-between; align-items: center; }
.upload-btn { display: inline-flex; align-items: center; gap: 6px; padding: 6px 12px; border: 1px solid var(--color-border, #e5e7eb); border-radius: 4px; cursor: pointer; font-size: 0.9rem; }
.upload-btn:hover { background: var(--color-hover, #f3f4f6); }
.upload-btn input { display: none; }
.attachments-list { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 4px; }
.attachment { display: flex; align-items: center; gap: 10px; padding: 8px 10px; background: var(--color-hover, #f9fafb); border-radius: 4px; }
.attachment__info { flex: 1; min-width: 0; }
.attachment__name { font-weight: 500; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
.attachment__meta { font-size: 0.8rem; color: var(--color-text-muted, #6b7280); }
.attachment__action { background: none; border: none; cursor: pointer; padding: 6px; color: var(--color-text-muted, #6b7280); border-radius: 4px; }
.attachment__action:hover { background: var(--color-hover, #e5e7eb); color: var(--color-text, #111827); }
.attachment__action--danger:hover { color: var(--color-danger, #dc2626); }
.muted { color: var(--color-text-muted, #6b7280); }
.small { font-size: 0.85rem; }
</style>
