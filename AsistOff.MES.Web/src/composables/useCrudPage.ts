import { ref, reactive, computed, watch } from 'vue';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';

export interface CrudPageOptions<TItem, TFilters extends object> {
  fetch: (req: IPagedRequest & TFilters) => Promise<IPagedResponse<TItem>>;
  initialFilters?: TFilters;
  pageSize?: number;
  defaultSort?: { field: string; direction: 'asc' | 'desc' };
}

export function useCrudPage<TItem, TFilters extends object = Record<string, never>>(
  opts: CrudPageOptions<TItem, TFilters>
) {
  const page = ref(1);
  const pageSize = ref(opts.pageSize ?? 10);
  const sortKey = ref<string | null>(opts.defaultSort?.field ?? null);
  const sortDirection = ref<'asc' | 'desc' | null>(opts.defaultSort?.direction ?? null);

  const filters = reactive({ ...(opts.initialFilters ?? ({} as TFilters)) }) as TFilters;

  const items = ref<TItem[]>([]) as import('vue').Ref<TItem[]>;
  const totalCount = ref(0);
  const totalPages = ref(0);
  const loading = ref(false);

  const request = computed<IPagedRequest & TFilters>(() => {
    const rawSort = sortKey.value && sortDirection.value
      ? [`${sortKey.value},${sortDirection.value}`]
      : [];
    return {
      pageNumber: page.value,
      pageSize: pageSize.value,
      rawSort,
      ...filters
    } as IPagedRequest & TFilters;
  });

  async function fetch() {
    loading.value = true;
    try {
      const res = await opts.fetch(request.value);
      items.value = res.items;
      totalCount.value = res.totalCount;
      totalPages.value = res.totalPages;
    } finally {
      loading.value = false;
    }
  }

  function setSort(key: string | null, direction: 'asc' | 'desc' | null) {
    sortKey.value = key;
    sortDirection.value = direction;
    page.value = 1;
  }

  function setPage(p: number) {
    if (p === page.value) return;
    page.value = p;
  }

  function setPageSize(s: number) {
    pageSize.value = s;
    page.value = 1;
  }

  function setFilter<K extends keyof TFilters>(key: K, value: TFilters[K]) {
    (filters as Record<string, unknown>)[key as string] = value;
    page.value = 1;
  }

  function resetFilters() {
    for (const key of Object.keys(filters as Record<string, unknown>)) {
      (filters as Record<string, unknown>)[key] = (opts.initialFilters as Record<string, unknown> | undefined)?.[key];
    }
    page.value = 1;
  }

  watch(request, () => { void fetch(); }, { deep: true });

  return {
    page,
    pageSize,
    sortKey,
    sortDirection,
    filters,
    items,
    totalCount,
    totalPages,
    loading,
    fetch,
    setSort,
    setPage,
    setPageSize,
    setFilter,
    resetFilters
  };
}
