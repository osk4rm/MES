import http from './http';

export interface PermissionResponse {
  id: string;
  code: string;
  name: string;
  category: string;
}

export interface RoleMemberResponse {
  userId: string;
  email: string;
}

export interface RoleResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  permissionCodes: string[];
  memberCount: number;
}

export interface RoleDetailResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  permissions: PermissionResponse[];
  members: RoleMemberResponse[];
}

export interface CreateRoleRequest {
  code: string;
  name: string;
  description?: string | null;
}

export interface UpdateRoleRequest {
  id: string;
  name: string;
  description?: string | null;
}

export interface SetRolePermissionsRequest {
  roleId: string;
  permissionIds: string[];
}

export interface AssignUserToRoleRequest {
  roleId: string;
  userId: string;
}

const BASE = '/api/roles';
const PERMISSIONS_BASE = '/api/permissions';

export const roleService = {
  async browse(): Promise<RoleResponse[]> {
    const { data } = await http.get<RoleResponse[]>(BASE);
    return data;
  },
  async get(id: string): Promise<RoleDetailResponse> {
    const { data } = await http.get<RoleDetailResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateRoleRequest): Promise<RoleResponse> {
    const { data } = await http.post<RoleResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateRoleRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async setPermissions(id: string, req: SetRolePermissionsRequest): Promise<void> {
    await http.put(`${BASE}/${id}/permissions`, req);
  },
  async assignMember(id: string, req: AssignUserToRoleRequest): Promise<void> {
    await http.post(`${BASE}/${id}/members`, req);
  },
  async unassignMember(id: string, userId: string): Promise<void> {
    await http.delete(`${BASE}/${id}/members/${userId}`);
  },
  async browsePermissions(): Promise<PermissionResponse[]> {
    const { data } = await http.get<PermissionResponse[]>(PERMISSIONS_BASE);
    return data;
  }
};
