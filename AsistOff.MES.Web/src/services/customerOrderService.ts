import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export const CustomerOrderStatus = {
  Imported: 1,
  Confirmed: 2,
  PartiallyReleasedToProduction: 3,
  ReleasedToProduction: 4,
  Completed: 5,
  Cancelled: 6
} as const;
export type CustomerOrderStatus = typeof CustomerOrderStatus[keyof typeof CustomerOrderStatus];

export const CustomerOrderLineStatus = {
  Open: 1,
  PartiallyReleasedToProduction: 2,
  ReleasedToProduction: 3,
  Completed: 4,
  Cancelled: 5
} as const;
export type CustomerOrderLineStatus = typeof CustomerOrderLineStatus[keyof typeof CustomerOrderLineStatus];

export const ProductionReleaseStatus = {
  Planned: 1,
  ProductionOrderCreated: 2,
  Cancelled: 3
} as const;
export type ProductionReleaseStatus = typeof ProductionReleaseStatus[keyof typeof ProductionReleaseStatus];

export interface CustomerResponse {
  id: string;
  syncId?: string | null;
  code: string;
  name: string;
  taxId?: string | null;
  email?: string | null;
  phone?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  postalCode?: string | null;
  city?: string | null;
  country?: string | null;
  isActive: boolean;
}

export interface CustomerShortResponse { id: string; code: string; name: string; taxId?: string | null }

export interface ProductionReleaseResponse {
  id: string;
  recipeId: string;
  recipeVersionId: string;
  quantity: number;
  plannedStartDate?: string | null;
  plannedDueDate?: string | null;
  productionOrderId?: string | null;
  status: ProductionReleaseStatus;
  notes?: string | null;
  createdAt: string;
}

export interface CustomerOrderLineResponse {
  id: string;
  syncId?: string | null;
  externalLineId?: string | null;
  lineNumber: number;
  productId: string;
  productCode: string;
  productName: string;
  measureUnitId?: string | null;
  measureUnitCode?: string | null;
  orderedQuantity: number;
  releasedQuantity: number;
  remainingQuantity: number;
  requestedDeliveryDate?: string | null;
  status: CustomerOrderLineStatus;
  unitNetPrice?: number | null;
  lineNetAmount?: number | null;
  notes?: string | null;
  productionReleases: ProductionReleaseResponse[];
}

export interface CustomerOrderResponse {
  id: string;
  syncId?: string | null;
  externalSystem?: string | null;
  externalOrderId?: string | null;
  orderNumber: string;
  customer: CustomerShortResponse;
  customerNameSnapshot: string;
  customerTaxIdSnapshot?: string | null;
  customerAddressSnapshot?: string | null;
  status: CustomerOrderStatus;
  orderDate?: string | null;
  requestedDeliveryDate?: string | null;
  confirmedDeliveryDate?: string | null;
  currency?: string | null;
  totalNetAmount?: number | null;
  totalGrossAmount?: number | null;
  notes?: string | null;
  createdAt: string;
  updatedAt?: string | null;
  lines: CustomerOrderLineResponse[];
}

export interface BrowseCustomersRequest extends IPagedRequest {
  code?: string;
  name?: string;
  taxId?: string;
  isActive?: boolean;
}

export interface BrowseCustomerOrdersRequest extends IPagedRequest {
  orderNumber?: string;
  customerId?: string;
  customerName?: string;
  productId?: string;
  status?: CustomerOrderStatus;
  externalSystem?: string;
  orderDateFrom?: string;
  orderDateTo?: string;
  deliveryDateFrom?: string;
  deliveryDateTo?: string;
}

export interface UpsertCustomerRequest {
  id?: string;
  code: string;
  name: string;
  taxId?: string | null;
  email?: string | null;
  phone?: string | null;
  addressLine1?: string | null;
  addressLine2?: string | null;
  postalCode?: string | null;
  city?: string | null;
  country?: string | null;
  isActive: boolean;
  syncId?: string | null;
}

export interface CreateCustomerOrderLineRequest {
  syncId?: string | null;
  externalLineId?: string | null;
  lineNumber: number;
  productId: string;
  measureUnitId?: string | null;
  orderedQuantity: number;
  requestedDeliveryDate?: string | null;
  unitNetPrice?: number | null;
  lineNetAmount?: number | null;
  notes?: string | null;
}

export interface CreateCustomerOrderRequest {
  syncId?: string | null;
  externalSystem?: string | null;
  externalOrderId?: string | null;
  orderNumber: string;
  customerId: string;
  status: CustomerOrderStatus;
  orderDate?: string | null;
  requestedDeliveryDate?: string | null;
  confirmedDeliveryDate?: string | null;
  currency?: string | null;
  totalNetAmount?: number | null;
  totalGrossAmount?: number | null;
  notes?: string | null;
  lines: CreateCustomerOrderLineRequest[];
}

export interface UpdateCustomerOrderRequest extends Omit<CreateCustomerOrderRequest, 'lines'> { id: string }
export interface AddCustomerOrderLineRequest extends CreateCustomerOrderLineRequest { customerOrderId: string }
export interface UpdateCustomerOrderLineRequest extends CreateCustomerOrderLineRequest { id: string; status: CustomerOrderLineStatus }
export interface CreateProductionReleaseRequest {
  customerOrderLineId: string;
  recipeId: string;
  recipeVersionId?: string | null;
  quantity: number;
  plannedStartDate?: string | null;
  plannedDueDate?: string | null;
  notes?: string | null;
}

export const customerService = {
  async browse(req: BrowseCustomersRequest): Promise<IPagedResponse<CustomerResponse>> {
    const { data } = await http.get<IPagedResponse<CustomerResponse>>('/api/customers', { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<CustomerResponse> {
    const { data } = await http.get<CustomerResponse>(`/api/customers/${id}`);
    return data;
  },
  async create(req: UpsertCustomerRequest): Promise<CustomerResponse> {
    const { data } = await http.post<CustomerResponse>('/api/customers', req);
    return data;
  },
  async update(id: string, req: UpsertCustomerRequest): Promise<void> {
    await http.put(`/api/customers/${id}`, { ...req, id });
  },
  async remove(id: string): Promise<void> {
    await http.delete(`/api/customers/${id}`);
  }
};

export const customerOrderService = {
  async browse(req: BrowseCustomerOrdersRequest): Promise<IPagedResponse<CustomerOrderResponse>> {
    const { data } = await http.get<IPagedResponse<CustomerOrderResponse>>('/api/customer-orders', { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<CustomerOrderResponse> {
    const { data } = await http.get<CustomerOrderResponse>(`/api/customer-orders/${id}`);
    return data;
  },
  async create(req: CreateCustomerOrderRequest): Promise<CustomerOrderResponse> {
    const { data } = await http.post<CustomerOrderResponse>('/api/customer-orders', req);
    return data;
  },
  async update(id: string, req: UpdateCustomerOrderRequest): Promise<void> {
    await http.put(`/api/customer-orders/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`/api/customer-orders/${id}`);
  },
  async addLine(orderId: string, req: AddCustomerOrderLineRequest): Promise<CustomerOrderResponse> {
    const { data } = await http.post<CustomerOrderResponse>(`/api/customer-orders/${orderId}/lines`, req);
    return data;
  },
  async updateLine(id: string, req: UpdateCustomerOrderLineRequest): Promise<CustomerOrderResponse> {
    const { data } = await http.put<CustomerOrderResponse>(`/api/customer-orders/lines/${id}`, req);
    return data;
  },
  async removeLine(id: string): Promise<CustomerOrderResponse> {
    const { data } = await http.delete<CustomerOrderResponse>(`/api/customer-orders/lines/${id}`);
    return data;
  },
  async createProductionRelease(lineId: string, req: CreateProductionReleaseRequest): Promise<CustomerOrderResponse> {
    const { data } = await http.post<CustomerOrderResponse>(`/api/customer-orders/lines/${lineId}/production-releases`, req);
    return data;
  }
};
