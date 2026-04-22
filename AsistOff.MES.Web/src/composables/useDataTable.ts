import { ref, computed, reactive, watch } from 'vue';
import type { 
  IPagedRequest, 
  IPagedResponse, 
  SortField, 
  FilterState, 
  DataTableConfig 
} from '../models/pagedModels';
import { createPagedRequest, DEFAULT_TABLE_CONFIG } from '../models/pagedModels';

/**
 * Composable for handling table state with backend pagination and filtering
 */
export function useDataTable<T, F extends FilterState = FilterState>(
  config: Partial<DataTableConfig> = {}
) {
  const tableConfig = { ...DEFAULT_TABLE_CONFIG, ...config };
  
  // Reactive state
  const currentPage = ref(1);
  const pageSize = ref(tableConfig.pageSize);
  const sort = ref<SortField[]>(tableConfig.defaultSort ? [tableConfig.defaultSort] : []);
  const filters = reactive<F>({} as F);
  const loading = ref(false);
  const data = ref<IPagedResponse<T> | null>(null);
  
  // Computed values
  const totalCount = computed(() => data.value?.totalCount || 0);
  const totalPages = computed(() => data.value?.totalPages || 0);
  const items = computed(() => data.value?.items || []);
  
  const paginationInfo = computed(() => ({
    currentPage: currentPage.value,
    pageSize: pageSize.value,
    totalCount: totalCount.value,
    totalPages: totalPages.value
  }));
  
  // Create request parameters
  const requestParams = computed((): IPagedRequest & F => {
    return createPagedRequest(
      currentPage.value,
      pageSize.value,
      sort.value,
      filters
    ) as IPagedRequest & F;
  });
  
  // Methods for managing table state
  function setData(response: IPagedResponse<T>) {
    data.value = response;
  }
  
  function setLoading(isLoading: boolean) {
    loading.value = isLoading;
  }
  
  function goToPage(page: number) {
    if (page >= 1 && page <= totalPages.value && page !== currentPage.value) {
      currentPage.value = page;
    }
  }
  
  function changePageSize(newPageSize: number) {
    pageSize.value = newPageSize;
    currentPage.value = 1;
  }
  
  function setSort(field: string, direction: 'asc' | 'desc') {
    sort.value = [{ field, direction }];
    currentPage.value = 1;
  }
  
  function clearSort() {
    sort.value = [];
    currentPage.value = 1;
  }
  
  function setFilter<K extends keyof F>(key: K, value: F[K]) {
    (filters as any)[key] = value;
    currentPage.value = 1;
  }
  
  function clearFilter<K extends keyof F>(key: K) {
    delete (filters as any)[key];
    currentPage.value = 1;
  }
  
  function clearAllFilters() {
    Object.keys(filters).forEach(key => {
      delete (filters as any)[key];
    });
    currentPage.value = 1;
  }
  
  function reset() {
    currentPage.value = 1;
    pageSize.value = tableConfig.pageSize;
    sort.value = tableConfig.defaultSort ? [tableConfig.defaultSort] : [];
    clearAllFilters();
    data.value = null;
  }
  
  // Auto-fetch functionality
  let fetchFunction: ((params: IPagedRequest & F) => Promise<IPagedResponse<T>>) | null = null;
  
  function setFetchFunction(fn: (params: IPagedRequest & F) => Promise<IPagedResponse<T>>) {
    fetchFunction = fn;
  }
  
  async function fetch() {
    if (!fetchFunction) {
      console.warn('No fetch function set for useDataTable');
      return;
    }
    
    try {
      loading.value = true;
      const response = await fetchFunction(requestParams.value);
      setData(response);
    } catch (error) {
      console.error('Error fetching data:', error);
      throw error;
    } finally {
      loading.value = false;
    }
  }
  
  // Watch for changes and auto-fetch if function is set
  watch(requestParams, () => {
    if (fetchFunction) {
      fetch();
    }
  }, { deep: true });
  
  return {
    // State
    currentPage,
    pageSize,
    sort,
    filters,
    loading,
    data,
    
    // Computed
    totalCount,
    totalPages,
    items,
    paginationInfo,
    requestParams,
    
    // Methods
    setData,
    setLoading,
    goToPage,
    changePageSize,
    setSort,
    clearSort,
    setFilter,
    clearFilter,
    clearAllFilters,
    reset,
    setFetchFunction,
    fetch
  };
}

// Utility composable for basic CRUD operations with backend pagination
export function useCrudTable<T, CreateT = Partial<T>, UpdateT = Partial<T>, FilterT extends FilterState = FilterState>(
  service: {
    getAll: (params: IPagedRequest & FilterT) => Promise<IPagedResponse<T>>;
    create?: (item: CreateT) => Promise<T>;
    update?: (id: string, item: UpdateT) => Promise<T>;
    delete?: (id: string) => Promise<void>;
  },
  config: Partial<DataTableConfig> = {}
) {
  const table = useDataTable<T, FilterT>(config);
  
  // Set up auto-fetching
  table.setFetchFunction(service.getAll);
  
  // CRUD methods
  async function create(item: CreateT): Promise<T> {
    if (!service.create) {
      throw new Error('Create method not provided');
    }
    
    const result = await service.create(item);
    await table.fetch(); // Refresh data
    return result;
  }
  
  async function update(id: string, item: UpdateT): Promise<T> {
    if (!service.update) {
      throw new Error('Update method not provided');
    }
    
    const result = await service.update(id, item);
    await table.fetch(); // Refresh data
    return result;
  }
  
  async function remove(id: string): Promise<void> {
    if (!service.delete) {
      throw new Error('Delete method not provided');
    }
    
    await service.delete(id);
    
    // If we're on the last page and it becomes empty, go to previous page
    if (table.items.value.length === 1 && table.currentPage.value > 1) {
      table.goToPage(table.currentPage.value - 1);
    } else {
      await table.fetch(); // Refresh data
    }
  }
  
  return {
    ...table,
    create,
    update,
    remove
  };
}
