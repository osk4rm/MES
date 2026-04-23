import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface ProductGroupResponse {
  id: string;
  syncId?: string | null;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  parent?: { id: string; code: string } | null;
}

export interface BrowseProductGroupsRequest extends IPagedRequest {
  name?: string;
  code?: string;
  isActive?: boolean;
  parentId?: string;
}

export interface CreateProductGroupRequest {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  parentId?: string | null;
  syncId?: string | null;
}

export interface UpdateProductGroupRequest extends CreateProductGroupRequest {
  id: string;
}

const BASE = '/api/product-groups';

export const productGroupService = {
  async browse(req: BrowseProductGroupsRequest): Promise<IPagedResponse<ProductGroupResponse>> {
    const { data } = await http.get<IPagedResponse<ProductGroupResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<ProductGroupResponse> {
    const { data } = await http.get<ProductGroupResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateProductGroupRequest): Promise<ProductGroupResponse> {
    const { data } = await http.post<ProductGroupResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateProductGroupRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
