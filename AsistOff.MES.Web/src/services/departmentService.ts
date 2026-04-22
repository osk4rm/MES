import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';

export interface Department {
  id: string;
  code: string;
  name: string;
}

export interface CreateDepartmentRequest {
  code?: string;
  name?: string;
}

export interface UpdateDepartmentRequest {
  id: string;
  code?: string;
  name?: string;
}

export interface DepartmentFilter extends IPagedRequest {
  code?: string;
  name?: string;
}

class DepartmentService {
  private readonly basePath = '/api/departments';

  async getDepartments(params: DepartmentFilter = {}): Promise<IPagedResponse<Department>> {
    const response = await http.get<IPagedResponse<Department>>(this.basePath, { params });
    return response.data;
  }

  async getDepartment(id: string): Promise<Department> {
    const response = await http.get<Department>(`${this.basePath}/${id}`);
    return response.data;
  }

  async createDepartment(department: CreateDepartmentRequest): Promise<Department> {
    const response = await http.post<Department>(this.basePath, department);
    return response.data;
  }

  async updateDepartment(id: string, department: UpdateDepartmentRequest): Promise<Department> {
    const response = await http.put<Department>(`${this.basePath}/${id}`, department);
    return response.data;
  }

  async deleteDepartment(id: string): Promise<void> {
    await http.delete(`${this.basePath}/${id}`);
  }
}

export const departmentService = new DepartmentService();
