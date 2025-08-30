export interface IPagedRequest extends ISortable {
  pageNumber?: number;
  pageSize?: number;
  maxPageSize?: number;
}

export interface ISortable {
  rawSort?: string[];
}

export interface IPagedResponse<T> {
  totalCount: number;
  totalPages: number;
  items: T[];
}

export interface SortField {
  field: string;
  direction: 'asc' | 'desc';
}

export interface PaginationInfo {
  currentPage: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface FilterState {
  [key: string]: any;
}

export interface DataTableConfig {
  pageSize: number;
  pageSizeOptions: number[];
  defaultSort?: SortField;
}

// Default configuration
export const DEFAULT_TABLE_CONFIG: DataTableConfig = {
  pageSize: 10,
  pageSizeOptions: [5, 10, 25, 50, 100],
};

// Helper functions
export function createPagedRequest(
  page: number = 1,
  pageSize: number = 10,
  sort: SortField[] = [],
  filters: FilterState = {}
): IPagedRequest & FilterState {
  const rawSort = sort.map(s => `${s.field},${s.direction}`);
  
  return {
    pageNumber: page,
    pageSize,
    rawSort,
    ...filters
  };
}

export function parseSortString(sortString: string): SortField {
  const parts = sortString.trim().split(' ');
  return {
    field: parts[0],
    direction: parts[1]?.toLowerCase() === 'desc' ? 'desc' : 'asc'
  };
}

export function createSortString(field: string, direction: 'asc' | 'desc'): string {
  return `${field} ${direction}`;
}
