import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface ProductionConfirmationResponse {
  id: string;
  productionOrderId: string;
  machineId: string;
  reportedByOperatorId?: string | null;
  reportedAt: string;
  goodQuantity: number;
  scrapQuantity: number;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseProductionConfirmationsRequest extends IPagedRequest {
  productionOrderId?: string;
  machineId?: string;
  from?: string;
  to?: string;
}

export interface ConsumedLotLine {
  lotId: string;
  quantity: number;
}

/** Alias kept for the lots validation helper; both names describe the same payload line. */
export type ConsumedLotInput = ConsumedLotLine;

export interface CreateProductionConfirmationRequest {
  productionOrderId: string;
  machineId: string;
  reportedByOperatorId?: string | null;
  reportedAt: string;
  goodQuantity: number;
  scrapQuantity: number;
  notes?: string | null;
  producedLotId?: string | null;
  consumedLots?: ConsumedLotLine[] | null;
}

export interface MovementPreviewLine {
  movementType: string;
  productId: string;
  quantity: number;
  measureUnitId?: string | null;
  preferredWarehouseId?: string | null;
}

const BASE = '/api/production-confirmations';

export const productionConfirmationService = {
  async browse(req: BrowseProductionConfirmationsRequest): Promise<IPagedResponse<ProductionConfirmationResponse>> {
    const { data } = await http.get<IPagedResponse<ProductionConfirmationResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<ProductionConfirmationResponse> {
    const { data } = await http.get<ProductionConfirmationResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateProductionConfirmationRequest): Promise<ProductionConfirmationResponse> {
    const { data } = await http.post<ProductionConfirmationResponse>(BASE, req);
    return data;
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  },
  async getMovements(id: string): Promise<MovementPreviewLine[]> {
    const { data } = await http.get<MovementPreviewLine[]>(`${BASE}/${id}/movements`);
    return data;
  }
};
