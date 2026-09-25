import { beforeEach, describe, expect, it, vi } from 'vitest';
import http from './http';
import {
  roleService,
  type PermissionResponse,
  type RoleDetailResponse,
  type RoleResponse
} from './roleService';

vi.mock('./http', () => ({
  default: {
    get: vi.fn(),
    post: vi.fn(),
    put: vi.fn(),
    delete: vi.fn()
  }
}));

const getMock = vi.mocked(http.get);
const postMock = vi.mocked(http.post);
const putMock = vi.mocked(http.put);
const deleteMock = vi.mocked(http.delete);

function role(overrides: Partial<RoleResponse> = {}): RoleResponse {
  return {
    id: 'role-1',
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
    id: 'perm-1',
    code: 'users.write',
    name: 'Write users',
    category: 'users',
    ...overrides
  };
}

function detail(overrides: Partial<RoleDetailResponse> = {}): RoleDetailResponse {
  return {
    id: 'role-1',
    code: 'operator',
    name: 'Operator',
    description: null,
    permissions: [permission()],
    members: [{ userId: 'user-1', email: 'operator@example.com' }],
    ...overrides
  };
}

// Covers AC4 of issue #210 (RBAC slice 3/3): the Roles view lists roles,
// edits membership, and re-fetches after each mutation. The view itself is
// click-tested in the e2e stage; this spec pins the typed service contract
// the view depends on (routes + payloads), so a backend route regression
// fails fast in CI without booting the stack.
describe('roleService', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('browse lists roles from GET /api/roles', async () => {
    const roles = [role(), role({ id: 'role-2', code: 'supervisor', name: 'Supervisor' })];
    getMock.mockResolvedValue({ data: roles });

    const result = await roleService.browse();

    expect(result).toEqual(roles);
    expect(getMock).toHaveBeenCalledWith('/api/roles');
  });

  it('get loads role detail with members from GET /api/roles/:id', async () => {
    const expected = detail();
    getMock.mockResolvedValue({ data: expected });

    const result = await roleService.get('role-1');

    expect(result).toEqual(expected);
    expect(getMock).toHaveBeenCalledWith('/api/roles/role-1');
  });

  it('create posts the new role to POST /api/roles', async () => {
    const created = role({ id: 'role-9', code: 'line-lead', name: 'Line lead' });
    postMock.mockResolvedValue({ data: created });

    const result = await roleService.create({ code: 'line-lead', name: 'Line lead', description: null });

    expect(result).toEqual(created);
    expect(postMock).toHaveBeenCalledWith('/api/roles', {
      code: 'line-lead',
      name: 'Line lead',
      description: null
    });
  });

  it('update puts role metadata to PUT /api/roles/:id', async () => {
    putMock.mockResolvedValue({ data: undefined });

    await roleService.update('role-1', { id: 'role-1', name: 'Updated', description: 'Updated' });

    expect(putMock).toHaveBeenCalledWith('/api/roles/role-1', {
      id: 'role-1',
      name: 'Updated',
      description: 'Updated'
    });
  });

  it('setPermissions replaces the permission collection via PUT /api/roles/:id/permissions', async () => {
    putMock.mockResolvedValue({ data: undefined });

    await roleService.setPermissions('role-1', { roleId: 'role-1', permissionIds: ['perm-1'] });

    expect(putMock).toHaveBeenCalledWith('/api/roles/role-1/permissions', {
      roleId: 'role-1',
      permissionIds: ['perm-1']
    });
  });

  it('assignMember posts membership to POST /api/roles/:id/members', async () => {
    postMock.mockResolvedValue({ data: undefined });

    await roleService.assignMember('role-1', { roleId: 'role-1', userId: 'user-1' });

    expect(postMock).toHaveBeenCalledWith('/api/roles/role-1/members', {
      roleId: 'role-1',
      userId: 'user-1'
    });
  });

  it('unassignMember deletes membership via DELETE /api/roles/:id/members/:userId', async () => {
    deleteMock.mockResolvedValue({ data: undefined });

    await roleService.unassignMember('role-1', 'user-1');

    expect(deleteMock).toHaveBeenCalledWith('/api/roles/role-1/members/user-1');
  });

  it('browsePermissions lists the matrix columns from GET /api/permissions', async () => {
    const permissions = [permission(), permission({ id: 'perm-2', code: 'tenant.admin' })];
    getMock.mockResolvedValue({ data: permissions });

    const result = await roleService.browsePermissions();

    expect(result).toEqual(permissions);
    expect(getMock).toHaveBeenCalledWith('/api/permissions');
  });

  it('propagates API errors (409 duplicate code, 404 cross-tenant) to the caller', async () => {
    const conflict = new Error('Request failed with status code 409');
    postMock.mockRejectedValue(conflict);

    await expect(
      roleService.create({ code: 'operator', name: 'Operator', description: null })
    ).rejects.toBe(conflict);

    const notFound = new Error('Request failed with status code 404');
    getMock.mockRejectedValue(notFound);

    await expect(roleService.get('foreign-role')).rejects.toBe(notFound);
  });
});
