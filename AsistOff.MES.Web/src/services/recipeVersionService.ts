import http from './http';
import type { RecipeVersionStatus } from './recipeService';

export const OperationDependencyType = {
  FinishToStart: 1,
  StartToStart: 2,
  FinishToFinish: 3,
  StartToFinish: 4
} as const;
export type OperationDependencyType = typeof OperationDependencyType[keyof typeof OperationDependencyType];

export const BomQuantityType = {
  PerUnit: 1,
  PerBatch: 2,
  Fixed: 3
} as const;
export type BomQuantityType = typeof BomQuantityType[keyof typeof BomQuantityType];

export const ConsumptionTiming = {
  OnStart: 1,
  OnCompletion: 2,
  Continuous: 3
} as const;
export type ConsumptionTiming = typeof ConsumptionTiming[keyof typeof ConsumptionTiming];

export const OperationOutputType = {
  Product: 1,
  ByProduct: 2,
  Waste: 3,
  Sample: 4
} as const;
export type OperationOutputType = typeof OperationOutputType[keyof typeof OperationOutputType];

export const RunTimeMode = {
  PerUnitSeconds: 1,
  PerBatchMinutes: 2
} as const;
export type RunTimeMode = typeof RunTimeMode[keyof typeof RunTimeMode];

export interface DependencyEntry {
  predecessorOperationId: string;
  dependencyType: OperationDependencyType;
  lagMinutes?: number | null;
}

export interface BomItemDto {
  id: string;
  productId: string;
  measureUnitId?: string | null;
  quantity: number;
  quantityType: BomQuantityType;
  scrapPercentage?: number | null;
  isOptional: boolean;
  preferredWarehouseId?: string | null;
  consumptionTiming: ConsumptionTiming;
  notes?: string | null;
  sortIndex: number;
}

export interface OperationOutputDto {
  id: string;
  productId: string;
  measureUnitId?: string | null;
  quantity: number;
  quantityType: BomQuantityType;
  outputType: OperationOutputType;
  preferredWarehouseId?: string | null;
  notes?: string | null;
  sortIndex: number;
}

export interface ResourceRequirementDto {
  id: string;
  preferredDepartmentId?: string | null;
  preferredMachineId?: string | null;
  requiredCapability?: string | null;
  requiredOperatorCount: number;
  requiredRole?: string | null;
  notes?: string | null;
}

export interface OperationNodeDto {
  id: string;
  recipeVersionId: string;
  code: string;
  name: string;
  description?: string | null;
  operationType?: string | null;
  sortIndex: number;
  setupTimeMinutes?: number | null;
  runTimeMode: RunTimeMode;
  runTimePerUnitSeconds?: number | null;
  runTimePerBatchMinutes?: number | null;
  teardownTimeMinutes?: number | null;
  queueTimeMinutes?: number | null;
  isOptional: boolean;
  allowParallelExecution: boolean;
  expectedQuantity?: number | null;
  dependencies: DependencyEntry[];
  bomItems: BomItemDto[];
  outputs: OperationOutputDto[];
  resourceRequirements: ResourceRequirementDto[];
}

export interface RecipeVersionDetailResponse {
  id: string;
  recipeId: string;
  versionNumber: number;
  status: RecipeVersionStatus;
  releasedAt?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  changeNotes?: string | null;
  operations: OperationNodeDto[];
}

export interface CreateRecipeVersionRequest {
  recipeId: string;
  changeNotes?: string | null;
}

export interface CloneRecipeVersionRequest {
  sourceVersionId: string;
  changeNotes?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
}

export interface UpdateRecipeVersionMetadataRequest {
  versionId: string;
  changeNotes?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
}

export interface AddOperationRequest {
  versionId: string;
  code: string;
  name: string;
  description?: string | null;
  operationType?: string | null;
  sortIndex?: number | null;
  setupTimeMinutes?: number | null;
  runTimeMode: RunTimeMode;
  runTimePerUnitSeconds?: number | null;
  runTimePerBatchMinutes?: number | null;
  teardownTimeMinutes?: number | null;
  queueTimeMinutes?: number | null;
  isOptional: boolean;
  allowParallelExecution: boolean;
  expectedQuantity?: number | null;
}

export interface UpdateOperationRequest {
  operationId: string;
  code: string;
  name: string;
  description?: string | null;
  operationType?: string | null;
  sortIndex: number;
  setupTimeMinutes?: number | null;
  runTimeMode: RunTimeMode;
  runTimePerUnitSeconds?: number | null;
  runTimePerBatchMinutes?: number | null;
  teardownTimeMinutes?: number | null;
  queueTimeMinutes?: number | null;
  isOptional: boolean;
  allowParallelExecution: boolean;
  expectedQuantity?: number | null;
}

export const recipeVersionService = {
  async get(id: string): Promise<RecipeVersionDetailResponse> {
    const { data } = await http.get<RecipeVersionDetailResponse>(`/api/recipe-versions/${id}`);
    return data;
  },
  async create(req: CreateRecipeVersionRequest): Promise<RecipeVersionDetailResponse> {
    const { data } = await http.post<RecipeVersionDetailResponse>('/api/recipe-versions', req);
    return data;
  },
  async clone(req: CloneRecipeVersionRequest): Promise<RecipeVersionDetailResponse> {
    const { data } = await http.post<RecipeVersionDetailResponse>('/api/recipe-versions/clone', req);
    return data;
  },
  async updateMetadata(id: string, req: UpdateRecipeVersionMetadataRequest): Promise<void> {
    await http.put(`/api/recipe-versions/${id}/metadata`, req);
  },
  async release(id: string): Promise<void> {
    await http.post(`/api/recipe-versions/${id}/release`);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`/api/recipe-versions/${id}`);
  },

  // operations
  async addOperation(req: AddOperationRequest): Promise<OperationNodeDto> {
    const { data } = await http.post<OperationNodeDto>('/api/operations', req);
    return data;
  },
  async updateOperation(id: string, req: UpdateOperationRequest): Promise<void> {
    await http.put(`/api/operations/${id}`, req);
  },
  async deleteOperation(id: string): Promise<void> {
    await http.delete(`/api/operations/${id}`);
  },
  async reorderOperations(versionId: string, order: { operationId: string; sortIndex: number }[]): Promise<void> {
    await http.post(`/api/recipe-versions/${versionId}/operations/reorder`, { order });
  },

  // dependencies
  async setDependencies(operationId: string, dependencies: DependencyEntry[]): Promise<void> {
    await http.put(`/api/operations/${operationId}/dependencies`, { dependencies });
  },

  // bom items
  async addBomItem(operationId: string, item: Omit<BomItemDto, 'id'> & { operationId: string }): Promise<string> {
    const { data } = await http.post<string>(`/api/operations/${operationId}/bom-items`, item);
    return data;
  },
  async updateBomItem(id: string, item: BomItemDto & { bomItemId: string }): Promise<void> {
    await http.put(`/api/bom-items/${id}`, item);
  },
  async removeBomItem(id: string): Promise<void> {
    await http.delete(`/api/bom-items/${id}`);
  },

  // outputs
  async addOutput(operationId: string, out: Omit<OperationOutputDto, 'id'> & { operationId: string }): Promise<string> {
    const { data } = await http.post<string>(`/api/operations/${operationId}/outputs`, out);
    return data;
  },
  async updateOutput(id: string, out: OperationOutputDto & { outputId: string }): Promise<void> {
    await http.put(`/api/outputs/${id}`, out);
  },
  async removeOutput(id: string): Promise<void> {
    await http.delete(`/api/outputs/${id}`);
  },

  // resources
  async addResource(operationId: string, r: Omit<ResourceRequirementDto, 'id'> & { operationId: string }): Promise<string> {
    const { data } = await http.post<string>(`/api/operations/${operationId}/resources`, r);
    return data;
  },
  async updateResource(id: string, r: ResourceRequirementDto & { resourceRequirementId: string }): Promise<void> {
    await http.put(`/api/resources/${id}`, r);
  },
  async removeResource(id: string): Promise<void> {
    await http.delete(`/api/resources/${id}`);
  }
};
