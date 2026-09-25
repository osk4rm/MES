import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import RolesView from './RolesView.vue';
import AppCheckbox from '../../components/ui/AppCheckbox.vue';
import { roleService, type PermissionResponse, type RoleDetailResponse, type RoleResponse } from '../../services/roleService';

// Verifier gap for #210 / PR #224 (TESTS_INSUFFICIENT): backend unit +
// endpoint suites proved the role claim flow, and roleService.spec pins the
// typed HTTP contract, but the Roles view behaviour itself (list roles, edit
// the permission matrix, edit membership, survive reload via localStorage)
// had no committed coverage and the PR body deferred the click-through to
// the e2e stage. These component tests close that gap by driving the real
// Roles UI with mocked services: open view -> see seeded roles, toggle a
// matrix cell -> setPermissions with resolved ids + refresh, assign a member
// -> assignMember + refresh, reload -> selected role restored from
// localStorage.

vi.mock('../../services/roleService', async (importOriginal) => {
  const actual = await importOriginal<typeof import('../../services/roleService')>();
  return {
    ...actual,
    roleService: {
      ...actual.roleService,
      browse: vi.fn(),
      get: vi.fn(),
      create: vi.fn(),
      update: vi.fn(),
      setPermissions: vi.fn(),
      assignMember: vi.fn(),
      unassignMember: vi.fn(),
      browsePermissions: vi.fn()
    }
  };
});

vi.mock('vue-i18n', () => ({
  useI18n: (): { t: (key: string) => string } => ({
    t: (key: string): string => key
  })
}));

const browseMock = vi.mocked(roleService.browse);
const browsePermissionsMock = vi.mocked(roleService.browsePermissions);
const getMock = vi.mocked(roleService.get);
const setPermissionsMock = vi.mocked(roleService.setPermissions);
const assignMemberMock = vi.mocked(roleService.assignMember);

const SELECTED_KEY = 'roles.selectedId';

function role(overrides: Partial<RoleResponse> = {}): RoleResponse {
  return {
    id: 'role-operator',
    code: 'operator',
    name: 'Operator',
    description: null,
    permissionCodes: ['users.read'],
    memberCount: 1,
    ...overrides
  };
}

function permission(overrides: Partial<PermissionResponse> = {}): PermissionResponse {
  return {
    id: 'perm-users-write',
    code: 'users.write',
    name: 'Write users',
    category: 'users',
    ...overrides
  };
}

function detail(overrides: Partial<RoleDetailResponse> = {}): RoleDetailResponse {
  return {
    id: 'role-operator',
    code: 'operator',
    name: 'Operator',
    description: null,
    permissions: [permission()],
    members: [{ userId: 'user-1', email: 'operator@example.com' }],
    ...overrides
  };
}

function seed(): void {
  const roles = [
    role({ id: 'role-supervisor', code: 'supervisor', name: 'Supervisor', permissionCodes: [], memberCount: 0 }),
    role()
  ];
  const permissions = [
    permission({ id: 'perm-users-read', code: 'users.read', name: 'Read users' }),
    permission()
  ];
  browseMock.mockResolvedValue(roles);
  browsePermissionsMock.mockResolvedValue(permissions);
  getMock.mockImplementation(async (id: string) =>
    detail({ id, code: id === 'role-supervisor' ? 'supervisor' : 'operator', members: id === 'role-supervisor' ? [] : detail().members })
  );
  setPermissionsMock.mockResolvedValue(undefined);
  assignMemberMock.mockResolvedValue(undefined);
}

function mountRoles(): VueWrapper {
  return mount(RolesView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('RolesView', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    localStorage.clear();
    seed();
  });

  it('lists roles on mount with permission and member counts', async () => {
    const wrapper = mountRoles();
    await flushPromises();

    expect(browseMock).toHaveBeenCalledOnce();
    expect(browsePermissionsMock).toHaveBeenCalledOnce();

    const text = wrapper.text();
    expect(text).toContain('operator');
    expect(text).toContain('supervisor');
    expect(text).toContain('users.write');
    // Permission matrix renders one row per permission.
    expect(wrapper.find('.matrix-table').exists()).toBe(true);
  });

  it('toggles a matrix cell via setPermissions with resolved ids then refreshes', async () => {
    const wrapper = mountRoles();
    await flushPromises();

    const checkboxes = wrapper.findAllComponents(AppCheckbox);
    expect(checkboxes.length).toBeGreaterThan(0);

    // Grant users.write to the operator role (currently holds users.read only).
    // Matrix rows are permissions sorted by code, columns are roles sorted by
    // code: row "users.write" x column "operator" is the unchecked cell that
    // must resolve to both permission ids after the toggle.
    const rows = wrapper.findAll('.matrix-table tbody tr');
    const targetRow = rows.find((r) => r.text().includes('users.write'));
    expect(targetRow).toBeDefined();
    const cellBoxes = targetRow?.findAllComponents(AppCheckbox) ?? [];
    expect(cellBoxes.length).toBe(2);
    expect(cellBoxes[0].props('modelValue')).toBe(false);

    const before = setPermissionsMock.mock.calls.length;
    cellBoxes[0].vm.$emit('update:modelValue', true);
    await flushPromises();

    expect(setPermissionsMock.mock.calls.length).toBe(before + 1);
    const [roleId, body] = setPermissionsMock.mock.calls.at(-1) ?? [];
    expect(typeof roleId).toBe('string');
    expect(body).toMatchObject({ roleId });
    // Both users.read and users.write must resolve to known permission ids.
    expect((body as { permissionIds: string[] }).permissionIds).toContain('perm-users-read');
    expect((body as { permissionIds: string[] }).permissionIds).toContain('perm-users-write');
    // Mutation re-fetches the list.
    expect(browseMock.mock.calls.length).toBeGreaterThan(1);
  });

  it('loads members for the selected role and persists the selection', async () => {
    const wrapper = mountRoles();
    await flushPromises();

    // First role (sorted by code: operator < supervisor) is auto-selected.
    expect(getMock).toHaveBeenCalled();
    expect(wrapper.text()).toContain('operator@example.com');
    expect(localStorage.getItem(SELECTED_KEY)).toBe('role-operator');
  });

  it('restores the selected role from localStorage on reload', async () => {
    localStorage.setItem(SELECTED_KEY, 'role-supervisor');

    const wrapper = mountRoles();
    await flushPromises();

    expect(getMock).toHaveBeenCalledWith('role-supervisor');
    expect(localStorage.getItem(SELECTED_KEY)).toBe('role-supervisor');
    expect(wrapper.text()).toContain('supervisor');
  });

  it('assigns a member by user id then refreshes and clears the input', async () => {
    const wrapper = mountRoles();
    await flushPromises();

    const input = wrapper.find('.assign-row input');
    expect(input.exists()).toBe(true);
    await input.setValue('new-user-id');
    await wrapper.find('.assign-row').trigger('submit.prevent');
    await flushPromises();

    expect(assignMemberMock).toHaveBeenCalledOnce();
    expect(assignMemberMock).toHaveBeenCalledWith('role-operator', {
      roleId: 'role-operator',
      userId: 'new-user-id'
    });
    expect(browseMock.mock.calls.length).toBeGreaterThan(1);
  });
});
