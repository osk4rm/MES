import { beforeEach, describe, expect, it, vi } from 'vitest';
import { flushPromises, mount, type VueWrapper } from '@vue/test-utils';
import { createPinia, setActivePinia } from 'pinia';
import RolesView from './RolesView.vue';
import { roleService, type PermissionResponse, type RoleDetailResponse, type RoleResponse } from '../../services/roleService';

// Slice (3/3) F-15: the Roles assign flow offers user lookup (suggestions
// from members already seen in the session — no backend users endpoint,
// which is out of scope) and the permission matrix is filterable.
// Pagination stays out: role counts do not justify it.

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

function role(overrides: Partial<RoleResponse> = {}): RoleResponse {
  return {
    id: 'role-operator',
    code: 'operator',
    name: 'Operator',
    description: null,
    permissionCodes: ['users.read'],
    memberCount: 2,
    ...overrides
  };
}

function permission(overrides: Partial<PermissionResponse> = {}): PermissionResponse {
  return {
    id: 'perm-users-read',
    code: 'users.read',
    name: 'Read users',
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
    members: [
      { userId: 'user-1', email: 'operator@example.com' },
      { userId: 'user-2', email: 'supervisor@example.com' }
    ],
    ...overrides
  };
}

function seed(): void {
  browseMock.mockResolvedValue([role()]);
  browsePermissionsMock.mockResolvedValue([
    permission(),
    permission({ id: 'perm-orders-write', code: 'orders.write', name: 'Write orders', category: 'orders' })
  ]);
  getMock.mockResolvedValue(detail());
}

function mountRoles(): VueWrapper {
  return mount(RolesView, {
    global: {
      plugins: [createPinia()],
      mocks: { $t: (key: string): string => key }
    }
  }) as unknown as VueWrapper;
}

describe('RolesView lookup and matrix filter (F-15)', () => {
  beforeEach(() => {
    setActivePinia(createPinia());
    vi.clearAllMocks();
    localStorage.clear();
    seed();
  });

  it('offers known-user suggestions in the assign flow', async () => {
    const wrapper = mountRoles();
    await flushPromises();

    // The assign input is labelled and backed by a datalist of known users.
    expect(wrapper.text()).toContain('roles.userLookupLabel');
    const input = wrapper.find('.assign-row input');
    expect(input.exists()).toBe(true);
    const listId = input.attributes('list');
    expect(listId).toBeTruthy();

    const datalist = wrapper.find(`datalist#${listId}`);
    expect(datalist.exists()).toBe(true);
    const options = datalist.findAll('option');
    expect(options.length).toBe(2);
    expect(options.map((o) => o.attributes('value'))).toContain('user-1');
    expect(options.map((o) => o.attributes('value'))).toContain('user-2');
  });

  it('filters the permission matrix by code, name or category', async () => {
    const wrapper = mountRoles();
    await flushPromises();

    expect(wrapper.findAll('.matrix-table tbody tr').length).toBe(2);

    const filter = wrapper.find('.matrix-toolbar input');
    expect(filter.exists()).toBe(true);
    await filter.setValue('orders');
    await flushPromises();

    const rows = wrapper.findAll('.matrix-table tbody tr');
    expect(rows.length).toBe(1);
    expect(rows[0]?.text()).toContain('orders.write');

    await filter.setValue('');
    await flushPromises();
    expect(wrapper.findAll('.matrix-table tbody tr').length).toBe(2);
  });
});
