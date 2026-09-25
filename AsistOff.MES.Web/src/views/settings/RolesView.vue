<template>
  <div>
    <AppPageHeader :title="$t('roles.title')" :subtitle="$t('roles.subtitle')" icon="pi pi-lock">
      <template #actions>
        <AppButton variant="secondary" icon="pi pi-refresh" @click="refresh">{{ $t('common.refresh') }}</AppButton>
        <AppButton variant="primary" icon="pi pi-plus" @click="openCreate">{{ $t('roles.create') }}</AppButton>
      </template>
    </AppPageHeader>

    <AppTable
      :items="roles"
      :columns="columns"
      :loading="loading"
    >
      <template #cell-code="{ item }">
        <code>{{ item.code }}</code>
      </template>
      <template #cell-permissions="{ item }">
        <AppBadge variant="success" dot>{{ item.permissionCodes.length }}</AppBadge>
      </template>
      <template #cell-members="{ item }">
        <AppBadge variant="success" dot>{{ item.memberCount }}</AppBadge>
      </template>
      <template #cell-actions="{ item }">
        <AppRowActions
          :actions="[
            { key: 'edit', label: $t('common.edit'), icon: 'pi-pencil' }
          ]"
          @action="(k) => onRowAction(k, item)"
        />
      </template>
      <template #empty>
        <AppEmptyState :title="$t('roles.emptyRoles')" icon="pi pi-lock" />
      </template>
    </AppTable>

    <AppCard :title="$t('roles.permissionMatrix')" :subtitle="$t('roles.matrixSubtitle')">
      <div v-if="matrixLoading" class="matrix-state">{{ $t('common.loading') }}</div>
      <table v-else class="matrix-table">
        <thead>
          <tr>
            <th class="matrix-table__head">{{ $t('roles.permissions') }}</th>
            <th v-for="role in roles" :key="role.id" class="matrix-table__head matrix-table__head--role">
              <code>{{ role.code }}</code>
            </th>
          </tr>
        </thead>
        <tbody>
          <tr v-for="permission in permissions" :key="permission.id">
            <td class="matrix-table__cell">
              <code>{{ permission.code }}</code>
              <span class="matrix-table__category">{{ permission.category }}</span>
            </td>
            <td v-for="role in roles" :key="role.id" class="matrix-table__cell matrix-table__cell--check">
              <AppCheckbox
                :model-value="hasPermission(role, permission.code)"
                :disabled="matrixSaving"
                @update:model-value="(v) => togglePermission(role, permission, v)"
              />
            </td>
          </tr>
        </tbody>
      </table>
    </AppCard>

    <AppCard :title="$t('roles.membersTitle')" :subtitle="$t('roles.matrixSubtitle')">
      <div class="members-toolbar">
        <AppSelect
          v-model="selectedRoleId"
          :options="roleOptions"
          :placeholder="$t('roles.selectRoleHint')"
          @change="onRoleChange"
        />
      </div>
      <AppTable
        :items="members"
        :columns="memberColumns"
        :loading="membersLoading"
      >
        <template #cell-actions="{ item }">
          <AppRowActions
            :actions="[
              { key: 'unassign', label: $t('roles.unassign'), icon: 'pi-user-minus', variant: 'danger' }
            ]"
            @action="(k) => onMemberAction(k, item)"
          />
        </template>
        <template #empty>
          <AppEmptyState :title="$t('roles.emptyMembers')" icon="pi pi-users" />
        </template>
      </AppTable>
      <form class="assign-row" @submit.prevent="onAssign">
        <AppInput v-model="assignUserId" :placeholder="$t('roles.userIdPlaceholder')" prefix-icon="pi pi-user" clearable />
        <AppButton type="submit" variant="primary" icon="pi pi-plus" :loading="assigning" :disabled="!selectedRoleId || !assignUserId">
          {{ $t('roles.assign') }}
        </AppButton>
      </form>
    </AppCard>

    <AppModal :open="modalOpen" :title="editing ? $t('roles.edit') : $t('roles.create')" @close="closeModal">
      <form id="role-form" class="form-grid" @submit.prevent="onSave">
        <AppFormField :label="$t('roles.code')" required class="form-grid__full">
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.code" required :invalid="invalid" :disabled="editing !== null" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('roles.name')" required class="form-grid__full">
          <template #default="{ id, invalid }">
            <AppInput :id="id" v-model="form.name" required :invalid="invalid" />
          </template>
        </AppFormField>
        <AppFormField :label="$t('roles.description')" class="form-grid__full">
          <template #default="{ id }">
            <AppTextarea :id="id" v-model="form.description" :rows="2" />
          </template>
        </AppFormField>
      </form>
      <template #footer>
        <AppButton variant="ghost" :disabled="saving" @click="closeModal">{{ $t('common.cancel') }}</AppButton>
        <AppButton type="submit" form="role-form" variant="primary" :loading="saving">{{ $t('common.save') }}</AppButton>
      </template>
    </AppModal>

    <AppConfirmDialog
      :open="confirmOpen"
      :title="$t('roles.unassign')"
      :message="unassignMessage"
      :loading="unassigning"
      @confirm="confirmUnassign"
      @cancel="cancelUnassign"
    />
  </div>
</template>

<script setup lang="ts">
import { computed, onMounted, reactive, ref } from 'vue';
import { useI18n } from 'vue-i18n';
import AppPageHeader from '../../components/ui/AppPageHeader.vue';
import AppTable from '../../components/ui/AppTable.vue';
import AppButton from '../../components/ui/AppButton.vue';
import AppBadge from '../../components/ui/AppBadge.vue';
import AppCard from '../../components/ui/AppCard.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import AppSelect from '../../components/ui/AppSelect.vue';
import AppInput from '../../components/ui/AppInput.vue';
import AppModal from '../../components/ui/AppModal.vue';
import AppFormField from '../../components/ui/AppFormField.vue';
import AppTextarea from '../../components/ui/AppTextarea.vue';
import AppRowActions from '../../components/ui/AppRowActions.vue';
import AppConfirmDialog from '../../components/ui/AppConfirmDialog.vue';
import AppEmptyState from '../../components/ui/AppEmptyState.vue';
import {
  roleService,
  type PermissionResponse,
  type RoleMemberResponse,
  type RoleResponse
} from '../../services/roleService';
import { useToastStore } from '../../stores/toastStore';
import { extractErrorMessage } from '../../services/http';

const { t } = useI18n();
const toast = useToastStore();

const SELECTED_KEY = 'roles.selectedId';

const roles = ref<RoleResponse[]>([]);
const permissions = ref<PermissionResponse[]>([]);
const members = ref<RoleMemberResponse[]>([]);
const loading = ref(false);
const membersLoading = ref(false);
const matrixSaving = ref(false);
const assigning = ref(false);
const unassigning = ref(false);
const saving = ref(false);

const selectedRoleId = ref<string | null>(null);
const assignUserId = ref('');

const columns = computed(() => [
  { key: 'code', label: t('roles.code'), sortable: true },
  { key: 'name', label: t('roles.name'), sortable: true },
  { key: 'permissions', label: t('roles.permissions'), align: 'right' as const, width: '130px' },
  { key: 'members', label: t('roles.members'), align: 'right' as const, width: '130px' },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const memberColumns = computed(() => [
  { key: 'email', label: t('auth.email'), sortable: true },
  { key: 'actions', label: t('common.actions'), width: '90px' }
]);

const roleOptions = computed(() =>
  roles.value.map(r => ({ value: r.id, label: `${r.code} — ${r.name}` }))
);

const matrixLoading = computed(() => loading.value && roles.value.length === 0);

function hasPermission(role: RoleResponse, code: string): boolean {
  return role.permissionCodes.includes(code);
}

function permissionIdByCode(code: string): string | null {
  return permissions.value.find(p => p.code === code)?.id ?? null;
}

async function refresh(): Promise<void> {
  loading.value = true;
  try {
    const [fetchedRoles, fetchedPermissions] = await Promise.all([
      roleService.browse(),
      roleService.browsePermissions()
    ]);
    roles.value = [...fetchedRoles].sort((a, b) => a.code.localeCompare(b.code));
    permissions.value = [...fetchedPermissions].sort((a, b) => a.code.localeCompare(b.code));

    if (!roles.value.some(r => r.id === selectedRoleId.value)) {
      selectedRoleId.value = roles.value[0]?.id ?? null;
      persistSelection();
    }
    await loadMembers();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    loading.value = false;
  }
}

async function loadMembers(): Promise<void> {
  if (!selectedRoleId.value) {
    members.value = [];
    return;
  }
  membersLoading.value = true;
  try {
    const detail = await roleService.get(selectedRoleId.value);
    members.value = detail.members;
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.loadFailed')));
  } finally {
    membersLoading.value = false;
  }
}

function persistSelection(): void {
  try {
    if (selectedRoleId.value) localStorage.setItem(SELECTED_KEY, selectedRoleId.value);
    else localStorage.removeItem(SELECTED_KEY);
  } catch { /* ignore */ }
}

function onRoleChange(): void {
  persistSelection();
  void loadMembers();
}

async function togglePermission(role: RoleResponse, permission: PermissionResponse, granted: boolean): Promise<void> {
  if (matrixSaving.value) return;
  matrixSaving.value = true;
  try {
    const current = new Set(role.permissionCodes);
    if (granted) current.add(permission.code);
    else current.delete(permission.code);

    const ids: string[] = [];
    for (const code of current) {
      const id = permissionIdByCode(code);
      if (id) ids.push(id);
    }
    await roleService.setPermissions(role.id, { roleId: role.id, permissionIds: ids });
    toast.success(t('toasts.updated'));
    await refresh();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    matrixSaving.value = false;
  }
}

async function onAssign(): Promise<void> {
  if (!selectedRoleId.value || !assignUserId.value || assigning.value) return;
  assigning.value = true;
  try {
    const roleId = selectedRoleId.value;
    await roleService.assignMember(roleId, { roleId, userId: assignUserId.value.trim() });
    assignUserId.value = '';
    toast.success(t('toasts.updated'));
    await refresh();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    assigning.value = false;
  }
}

const confirmOpen = ref(false);
const toUnassign = ref<RoleMemberResponse | null>(null);
const unassignMessage = computed(() =>
  toUnassign.value ? `${t('roles.unassign')}: ${toUnassign.value.email}` : ''
);

function onMemberAction(key: string, item: RoleMemberResponse): void {
  if (key === 'unassign') {
    toUnassign.value = item;
    confirmOpen.value = true;
  }
}

async function confirmUnassign(): Promise<void> {
  if (!toUnassign.value || !selectedRoleId.value) return;
  unassigning.value = true;
  try {
    await roleService.unassignMember(selectedRoleId.value, toUnassign.value.userId);
    toast.success(t('toasts.updated'));
    confirmOpen.value = false;
    toUnassign.value = null;
    await refresh();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.deleteFailed')));
  } finally {
    unassigning.value = false;
  }
}

function cancelUnassign(): void {
  confirmOpen.value = false;
  toUnassign.value = null;
}

const modalOpen = ref(false);
const editing = ref<RoleResponse | null>(null);
const form = reactive({ code: '', name: '', description: '' });

function openCreate(): void {
  editing.value = null;
  Object.assign(form, { code: '', name: '', description: '' });
  modalOpen.value = true;
}

function openEdit(item: RoleResponse): void {
  editing.value = item;
  Object.assign(form, { code: item.code, name: item.name, description: item.description ?? '' });
  modalOpen.value = true;
}

function closeModal(): void {
  if (saving.value) return;
  modalOpen.value = false;
  editing.value = null;
}

async function onSave(): Promise<void> {
  saving.value = true;
  try {
    const description = form.description.trim() ? form.description.trim() : null;
    if (editing.value) {
      await roleService.update(editing.value.id, {
        id: editing.value.id,
        name: form.name.trim(),
        description
      });
      toast.success(t('toasts.updated'));
    } else {
      await roleService.create({
        code: form.code.trim().toLowerCase(),
        name: form.name.trim(),
        description
      });
      toast.success(t('toasts.created'));
    }
    modalOpen.value = false;
    editing.value = null;
    await refresh();
  } catch (err) {
    toast.error(extractErrorMessage(err, t('errors.saveFailed')));
  } finally {
    saving.value = false;
  }
}

function onRowAction(key: string, item: RoleResponse): void {
  if (key === 'edit') openEdit(item);
}

onMounted(() => {
  try {
    selectedRoleId.value = localStorage.getItem(SELECTED_KEY);
  } catch { /* ignore */ }
  void refresh();
});
</script>

<style scoped>
.form-grid { display: grid; grid-template-columns: 1fr 1fr; gap: var(--space-3); }
.form-grid__full { grid-column: 1 / -1; }
.matrix-state { padding: var(--space-4); color: var(--color-text-muted); }
.matrix-table { width: 100%; border-collapse: collapse; font-size: var(--font-size-md); }
.matrix-table__head {
  text-align: left;
  padding: var(--space-2) var(--space-3);
  color: var(--color-text-muted);
  font-weight: var(--font-weight-semibold);
  border-bottom: 1px solid var(--color-border);
}
.matrix-table__head--role { text-align: center; }
.matrix-table__cell { padding: var(--space-2) var(--space-3); border-bottom: 1px solid var(--color-divider); }
.matrix-table__cell--check { text-align: center; }
.matrix-table__category { margin-left: var(--space-2); font-size: var(--font-size-sm); color: var(--color-text-subtle); }
.members-toolbar { display: flex; gap: var(--space-3); margin-bottom: var(--space-3); max-width: 420px; }
.assign-row { display: flex; gap: var(--space-3); margin-top: var(--space-3); max-width: 560px; }
</style>
