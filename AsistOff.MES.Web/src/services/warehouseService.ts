import http from './http';

export interface WarehouseResponse {
  id: string;
  name: string;
  externalId?: string;
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

export class WarehouseService {
  private static readonly BASE_PATH = '/api/warehouses';

  static async getWarehouses(): Promise<WarehouseResponse[]> {
    try {
      const response = await http.get<WarehousesResponse>(this.BASE_PATH);
      return response.data.warehouses;
    } catch (error) {
      throw error;
    }
  }

  static async getWarehouse(id: string): Promise<WarehouseResponse> {
    try {
      const response = await http.get<WarehouseResponse>(`${this.BASE_PATH}/${id}`);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  static async createWarehouse(request: CreateWarehouseRequest): Promise<WarehouseResponse> {
    try {
      const response = await http.post<WarehouseResponse>(this.BASE_PATH, request);
      return response.data;
    } catch (error) {
      throw error;
    }
  }

  static async updateWarehouse(request: UpdateWarehouseRequest): Promise<void> {
    try {
      await http.put(`${this.BASE_PATH}/${request.id}`, request);
    } catch (error) {
      throw error;
    }
  }

  static async deleteWarehouse(id: string): Promise<void> {
    try {
      await http.delete(`${this.BASE_PATH}/${id}`);
    } catch (error) {
      throw error;
    }
  }
}
