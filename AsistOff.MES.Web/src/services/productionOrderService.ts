import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const ProductionOrderStatus = {
  Planned: 1,
  Released: 2,
  InProgress: 3,
  Completed: 4,
  Closed: 5
} as const;
export type ProductionOrderStatus = typeof ProductionOrderStatus[keyof typeof ProductionOrderStatus];

export interface ProductionOrderResponse {
  id: string;
  code: string;
  productId: string;
  recipeId: string;
  recipeVersionId: string;
  plannedQuantity: number;
  measureUnitId?: string | null;
  priority: number;
  dueDate?: string | null;
  status: ProductionOrderStatus;
  releasedAt?: string | null;
  releasedByUserId?: string | null;
  notes?: string | null;
  syncId?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  producedQuantity: number;
  scrappedQuantity: number;
  remainingQuantity: number;
  confirmationsCount: number;
  completedAt?: string | null;
  closedAt?: string | null;
}

export interface BrowseProductionOrdersRequest extends IPagedRequest {
  code?: string;
  status?: ProductionOrderStatus;
  productId?: string;
  recipeId?: string;
  dueFrom?: string;
  dueTo?: string;
}

export interface CreateProductionOrderRequest {
  code: string;
  productId: string;
  recipeId: string;
  recipeVersionId: string;
  plannedQuantity: number;
  measureUnitId?: string | null;
  priority: number;
  dueDate?: string | null;
  notes?: string | null;
  syncId?: string | null;
}

export interface UpdateProductionOrderRequest extends CreateProductionOrderRequest {
  id: string;
}

const BASE = '/api/production-orders';

export const productionOrderService = {
  async browse(req: BrowseProductionOrdersRequest): Promise<IPagedResponse<ProductionOrderResponse>> {
    const { data } = await http.get<IPagedResponse<ProductionOrderResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<ProductionOrderResponse> {
    const { data } = await http.get<ProductionOrderResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateProductionOrderRequest): Promise<ProductionOrderResponse> {
    const { data } = await http.post<ProductionOrderResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateProductionOrderRequest): Promise<ProductionOrderResponse> {
    const { data } = await http.put<ProductionOrderResponse>(`${BASE}/${id}`, req);
    return data;
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  },
  async release(id: string): Promise<ProductionOrderResponse> {
    const { data } = await http.post<ProductionOrderResponse>(`${BASE}/${id}/release`);
    return data;
  },
  async complete(id: string): Promise<ProductionOrderResponse> {
    const { data } = await http.post<ProductionOrderResponse>(`${BASE}/${id}/complete`);
    return data;
  },
  async close(id: string): Promise<ProductionOrderResponse> {
    const { data } = await http.post<ProductionOrderResponse>(`${BASE}/${id}/close`);
    return data;
  }
};
