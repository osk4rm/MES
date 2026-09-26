import { ref, reactive, computed, watch, onUnmounted } from 'vue';
import type { IPagedRequest, IPagedResponse } from '../models/pagedModels';
import { createRequestController, extractErrorMessage, isCanceledError, type HttpRequestInit } from '../services/http';

export interface CrudPageOptions<TItem, TFilters extends object> {
  fetch: (req: IPagedRequest & TFilters, init?: HttpRequestInit) => Promise<IPagedResponse<TItem>>;
  initialFilters?: TFilters;
  pageSize?: number;
  defaultSort?: { field: string; direction: 'asc' | 'desc' };
  /** i18n fallback for the error banner when the failure carries no message. */
  errorFallback?: string;
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
  // Error banner state (issue #273): a failed list fetch surfaces the
  // message with a working retry instead of failing silently.
  const error = ref<string | null>(null);

  // Guards against stale responses (a newer fetch or an unmount wins) so a
  // late response never overwrites fresher state or touches a dead view.
  let generation = 0;
  let unmounted = false;
  let inflight: AbortController | null = null;

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
    const mine = ++generation;
    // Cancel the previous page fetch (rapid filter typing, page change):
    // only the latest response may commit state.
    inflight?.abort();
    const controller = createRequestController();
    inflight = controller;
    loading.value = true;
    error.value = null;
    try {
      const res = await opts.fetch(request.value, { signal: controller.signal });
      if (unmounted || mine !== generation) return;
      items.value = res.items;
      totalCount.value = res.totalCount;
      totalPages.value = res.totalPages;
    } catch (err) {
      if (unmounted || mine !== generation) return;
      // User navigated away or a newer fetch started: stay silent, the next
      // fetch owns the banner.
      if (isCanceledError(err)) return;
      error.value = extractErrorMessage(err, opts.errorFallback ?? 'Load failed');
    } finally {
      if (!unmounted && mine === generation) loading.value = false;
    }
  }

  /** Clears the banner and re-runs the current request. */
  async function retry() {
    await fetch();
  }

  onUnmounted(() => {
    unmounted = true;
    inflight?.abort();
  });

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
    error,
    fetch,
    retry,
    setSort,
    setPage,
    setPageSize,
    setFilter,
    resetFilters
  };
}
