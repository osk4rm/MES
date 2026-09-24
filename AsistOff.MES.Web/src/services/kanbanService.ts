import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const KanbanCardStatus = {
  Full: 1,
  Empty: 2,
  Ordered: 3
} as const;
export type KanbanCardStatus = typeof KanbanCardStatus[keyof typeof KanbanCardStatus];

export interface KanbanLoopResponse {
  id: string;
  code: string;
  productId: string;
  consumingMachineId: string;
  supplyingWarehouseId: string;
  cardQuantity: number;
  cardsInCirculation: number;
  isActive: boolean;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface KanbanCardResponse {
  id: string;
  loopId: string;
  cardNumber: string;
  status: KanbanCardStatus;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
}

export interface BrowseKanbanLoopsRequest extends IPagedRequest {
  code?: string;
  productId?: string;
  machineId?: string;
  isActive?: boolean;
}

export interface BrowseKanbanCardsRequest extends IPagedRequest {
  status?: KanbanCardStatus;
}

const BASE = '/api/kanban';

export const kanbanService = {
  async browseLoops(req: BrowseKanbanLoopsRequest = {}): Promise<IPagedResponse<KanbanLoopResponse>> {
    const { data } = await http.get<IPagedResponse<KanbanLoopResponse>>(`${BASE}/loops`, {
      params: buildPagedParams(req)
    });
    return data;
  },
  async browseCards(
    loopId: string,
    status?: KanbanCardStatus,
    paging: IPagedRequest = {}
  ): Promise<IPagedResponse<KanbanCardResponse>> {
    const { data } = await http.get<IPagedResponse<KanbanCardResponse>>(
      `${BASE}/loops/${encodeURIComponent(loopId)}/cards`,
      { params: buildPagedParams({ ...paging, status }) }
    );
    return data;
  },
  async consumeCard(id: string): Promise<KanbanCardResponse> {
    const { data } = await http.post<KanbanCardResponse>(`${BASE}/cards/${encodeURIComponent(id)}/consume`, {});
    return data;
  },
  async orderCard(id: string): Promise<KanbanCardResponse> {
    const { data } = await http.post<KanbanCardResponse>(`${BASE}/cards/${encodeURIComponent(id)}/order`, {});
    return data;
  },
  async replenishCard(id: string): Promise<KanbanCardResponse> {
    const { data } = await http.post<KanbanCardResponse>(`${BASE}/cards/${encodeURIComponent(id)}/replenish`, {});
    return data;
  }
};
