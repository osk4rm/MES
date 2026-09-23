<template>
  <AppModal :open="open" :title="title" size="lg" @close="onClose">
    <div v-if="loading" class="calendar-loading">
      <AppSpinner />
    </div>
    <div v-else class="calendar-editor">
      <p class="calendar-hint">{{ $t('calendar.hint') }}</p>
      <section v-for="day in weekDays" :key="day.value" class="day-block">
        <div class="day-header">
          <strong>{{ $t(`calendar.days.${day.key}`) }}</strong>
          <AppButton variant="ghost" icon="pi pi-plus" @click="addEntry(day.value)">{{ $t('common.add') }}</AppButton>
        </div>
        <div v-if="entriesFor(day.value).length === 0" class="day-empty">{{ $t('common.empty') }}</div>
        <div v-for="row in entriesFor(day.value)" :key="row.uid" class="entry-row">
          <AppInput v-model="row.startTime" type="time" required />
          <span class="entry-sep">–</span>
          <AppInput v-model="row.endTime" type="time" required />
          <AppSelect v-model="row.shiftId" :options="shiftOptions" allow-empty :empty-label="$t('calendar.noShift')" />
          <label class="entry-working">
            <input type="checkbox" v-model="row.isWorking" />
            {{ $t('calendar.working') }}
          </label>
          <AppButton variant="ghost" icon="pi pi-trash" @click="removeEntry(row.uid)" />
        </div>
      </section>
    </div>
    <template #footer>
      <AppButton variant="ghost" :disabled="saving" @click="onClose">{{ $t('common.cancel') }}</AppButton>
      <AppButton variant="primary" :loading="saving" :disabled="loading" @click="onSave">{{ $t('common.save') }}</AppButton>
    </template>
  </AppModal>
</template>

<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue';
import { useI18n } from 'vue-i18n';
import AppModal from '../../components/ui/AppModal.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppSelect, { type SelectOption } from '../../components/ui/AppSelect.vue';
import AppSpinner from '../../components/ui/AppSpinner.vue';
import { machineService } from '../../services/machineService';
import { shiftService, weekDays, type WorkCenterCalendarEntry } from '../../services/shiftService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

interface CalendarRow {
  uid: number;
  dayOfWeek: number;
  startTime: string;
  endTime: string;
  shiftId: string | null;
  isWorking: boolean;
}

const props = defineProps<{
  machineId: string | null;
  machineName: string;
  open: boolean;
}>();

const emit = defineEmits<{
  (e: 'close'): void;
  (e: 'saved'): void;
}>();

const { t } = useI18n();
const toast = useToastStore();

const loading = ref(false);
const saving = ref(false);
const rows = reactive<CalendarRow[]>([]);
const shifts = ref<{ id: string; code: string; name: string }[]>([]);
let uidSeq = 1;

const title = computed(() =>
  props.machineName ? `${t('calendar.title')}: ${props.machineName}` : t('calendar.title'));

const shiftOptions = computed<SelectOption[]>(() =>
  shifts.value.map((s) => ({ value: s.id, label: `${s.code} – ${s.name}` })));

function toShortTime(value: string): string {
  return value.length >= 5 ? value.slice(0, 5) : value;
}

function entriesFor(day: number): CalendarRow[] {
  return rows.filter((r) => r.dayOfWeek === day);
}

function toRow(entry: WorkCenterCalendarEntry): CalendarRow {
  return {
    uid: uidSeq++,
    dayOfWeek: entry.dayOfWeek,
    startTime: toShortTime(entry.startTime),
    endTime: toShortTime(entry.endTime),
    shiftId: entry.shiftId ?? null,
    isWorking: entry.isWorking
  };
}

function addEntry(day: number): void {
  rows.push({ uid: uidSeq++, dayOfWeek: day, startTime: '06:00', endTime: '14:00', shiftId: null, isWorking: true });
}

function removeEntry(uid: number): void {
  const index = rows.findIndex((r) => r.uid === uid);
  if (index >= 0) rows.splice(index, 1);
}

async function load(): Promise<void> {
  if (!props.machineId) return;
  loading.value = true;
  try {
    const [calendar, shiftList] = await Promise.all([
      machineService.getCalendar(props.machineId),
      shiftService.list({})
    ]);
    rows.splice(0, rows.length, ...calendar.entries.map(toRow));
    shifts.value = shiftList.map((s) => ({ id: s.id, code: s.code, name: s.name }));
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    loading.value = false;
  }
}

async function onSave(): Promise<void> {
  if (!props.machineId) return;
  saving.value = true;
  try {
    await machineService.saveCalendar(props.machineId, rows.map((r) => ({
      dayOfWeek: r.dayOfWeek,
      startTime: r.startTime,
      endTime: r.endTime,
      shiftId: r.shiftId,
      isWorking: r.isWorking
    })));
    toast.success(t('toasts.updated'));
    emit('saved');
    emit('close');
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

function onClose(): void {
  if (!saving.value) emit('close');
}

watch(() => props.open, (isOpen) => {
  if (isOpen) void load();
});
</script>

<style scoped>
.calendar-loading { display: flex; justify-content: center; padding: var(--space-5); }
.calendar-hint { color: var(--color-text-muted, #6b7280); margin-bottom: var(--space-3); }
.day-block { border-top: 1px solid var(--color-border, #e5e7eb); padding: var(--space-2) 0; }
.day-header { display: flex; align-items: center; justify-content: space-between; }
.day-empty { color: var(--color-text-muted, #6b7280); font-size: 13px; padding: var(--space-1) 0; }
.entry-row { display: flex; align-items: center; gap: var(--space-2); padding: var(--space-1) 0; }
.entry-sep { color: var(--color-text-muted, #6b7280); }
.entry-working { display: flex; align-items: center; gap: var(--space-1); font-size: 13px; white-space: nowrap; }
</style>
