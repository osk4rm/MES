import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';

export interface WarehouseResponse {
  id: string;
  name: string;
  syncId?: string;
}

export interface WarehousesResponse {
  warehouses: WarehouseResponse[];
}

export interface CreateWarehouseRequest {
  name: string;
  syncId?: string;
}

export interface UpdateWarehouseRequest {
  id: string;
  name: string;
}

export interface GetWarehousesRequest extends IPagedRequest {
  name?: string;
}

export class WarehouseService {
  private static readonly BASE_PATH = '/api/warehouses';

  static async getWarehouses(request: GetWarehousesRequest): Promise<IPagedResponse<WarehouseResponse>> {
    try {
      const params = new URLSearchParams();
      
      if (request.pageNumber) params.set('pageNumber', request.pageNumber.toString());
      if (request.pageSize) params.set('pageSize', request.pageSize.toString());
      if (request.rawSort && request.rawSort.length > 0) {
        request.rawSort.forEach(sort => params.append('rawSort', sort));
      }
      if (request.name) params.set('name', request.name);
      
      const url = params.toString() ? `${WarehouseService.BASE_PATH}?${params}` : WarehouseService.BASE_PATH;
      const response = await http.get<IPagedResponse<WarehouseResponse>>(url);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  static async getWarehouse(id: string): Promise<WarehouseResponse> {
    try {
      const response = await http.get<WarehouseResponse>(`${WarehouseService.BASE_PATH}/${id}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  static async createWarehouse(request: CreateWarehouseRequest): Promise<WarehouseResponse> {
    try {
      const response = await http.post<WarehouseResponse>(WarehouseService.BASE_PATH, request);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  static async updateWarehouse(request: UpdateWarehouseRequest): Promise<void> {
    try {
      await http.put(`${WarehouseService.BASE_PATH}/${request.id}`, request);
    } catch (error) {
      throw error;
    }
  }

  static async deleteWarehouse(id: string): Promise<void> {
    try {
      await http.delete(`${WarehouseService.BASE_PATH}/${id}`);
    } catch (error) {
      throw error;
    }
  }
}
