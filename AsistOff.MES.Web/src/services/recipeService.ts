import http from './http';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { buildPagedParams } from './tenantService';

export interface RecipeResponse {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  primaryProductId?: string | null;
  currentVersionId?: string | null;
  versions?: RecipeVersionSummary[];
}

export interface RecipeVersionSummary {
  id: string;
  versionNumber: number;
  status: RecipeVersionStatus;
  releasedAt?: string | null;
  validFrom?: string | null;
  validTo?: string | null;
  changeNotes?: string | null;
}

export const RecipeVersionStatus = {
  Draft: 1,
  Released: 2,
  Obsolete: 3
} as const;
export type RecipeVersionStatus = typeof RecipeVersionStatus[keyof typeof RecipeVersionStatus];

export interface BrowseRecipesRequest extends IPagedRequest {
  code?: string;
  name?: string;
  isActive?: boolean;
  primaryProductId?: string;
}

export interface CreateRecipeRequest {
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  primaryProductId?: string | null;
  initialVersionNotes?: string | null;
}

export interface UpdateRecipeRequest {
  id: string;
  code: string;
  name: string;
  description?: string | null;
  isActive: boolean;
  primaryProductId?: string | null;
}

const BASE = '/api/recipes';

export const recipeService = {
  async browse(req: BrowseRecipesRequest): Promise<IPagedResponse<RecipeResponse>> {
    const { data } = await http.get<IPagedResponse<RecipeResponse>>(BASE, { params: buildPagedParams(req) });
    return data;
  },
  async get(id: string): Promise<RecipeResponse> {
    const { data } = await http.get<RecipeResponse>(`${BASE}/${id}`);
    return data;
  },
  async create(req: CreateRecipeRequest): Promise<RecipeResponse> {
    const { data } = await http.post<RecipeResponse>(BASE, req);
    return data;
  },
  async update(id: string, req: UpdateRecipeRequest): Promise<void> {
    await http.put(`${BASE}/${id}`, req);
  },
  async remove(id: string): Promise<void> {
    await http.delete(`${BASE}/${id}`);
  }
};
