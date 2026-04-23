import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const ScanBy = {
  Ean: 1,
  Code: 2
} as const;
export type ScanBy = typeof ScanBy[keyof typeof ScanBy];

export interface ProductGroupShort { id: string; code: string; name: string }
export interface MeasureUnitShort { id: string; name: string; symbol: string }

export interface ProductResponse {
  id: string;
  syncId?: string | null;
  code: string;
  name: string;
  description?: string | null;
  ean?: string | null;
  barcode?: string | null;
  scanBy: ScanBy;
  isActive: boolean;
  group?: ProductGroupShort | null;
  defaultMeasureUnit?: MeasureUnitShort | null;
}

export interface BrowseProductsRequest extends IPagedRequest {
  name?: string;
  code?: string;
  isActive?: boolean;
  groupId?: string;
  ean?: string;
  barcode?: string;
  scanBy?: ScanBy;
}

export interface CreateProductRequest {
  code: string;
  name: string;
  description?: string | null;
  ean?: string | null;
  barcode?: string | null;
  scanBy: ScanBy;
  isActive: boolean;
  productGroupId?: string | null;
  syncId?: string | null;
}

export interface UpdateProductRequest extends CreateProductRequest {
  id: string;
}

const BASE = '/api/products';

export const productService = {
  async browse(req: BrowseProductsRequest): Promise<IPagedResponse<ProductResponse>> {
    const { data } = await http.get<IPagedResponse<ProductResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<ProductResponse> {
    const { data } = await http.get<ProductResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateProductRequest): Promise<ProductResponse> {
    const { data } = await http.post<ProductResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateProductRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
