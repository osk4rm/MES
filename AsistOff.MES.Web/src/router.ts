import { createRouter, createWebHistory } from 'vue-router';
import type { RouteLocationNormalized } from 'vue-router';
import { useAuthStore } from './stores/authStore';
import { useToastStore } from './stores/toastStore';
import { restoreSession } from './services/authService';
import { abortPendingRequests } from './services/http';
import { Permissions } from './models/authModels';
import i18n from './i18n';
import type { RouteRecordRaw } from 'vue-router';

const AppShell = () => import('./components/layout/AppShell.vue');

const routes: RouteRecordRaw[] = [
  { path: '/', redirect: '/dashboard' },
  { path: '/login', name: 'login', component: () => import('./views/LoginView.vue'), meta: { public: true } },
  { path: '/register', name: 'register', component: () => import('./views/RegisterView.vue'), meta: { public: true } },
  {
    path: '/',
    component: AppShell,
    children: [
      { path: 'dashboard', name: 'dashboard', component: () => import('./views/DashboardView.vue') },
      { path: 'production', redirect: '/production/orders' },
      { path: 'production/orders', name: 'production-orders', component: () => import('./views/production/ProductionOrdersView.vue'), meta: { titleKey: 'nav.productionOrders', icon: 'pi pi-list' } },
      { path: 'production/orders/:id', name: 'production-order-detail', component: () => import('./views/production/ProductionOrderDetailView.vue'), meta: { titleKey: 'nav.productionOrders', icon: 'pi pi-list' } },
      { path: 'production/scrap', name: 'production-scrap', component: () => import('./views/production/ScrapView.vue'), meta: { titleKey: 'nav.productionScrap', icon: 'pi pi-trash' } },
      { path: 'production/andon', name: 'production-andon', component: () => import('./views/production/AndonView.vue'), meta: { titleKey: 'nav.productionAndon', icon: 'pi pi-bell' } },
      { path: 'production/recipes', name: 'production-recipes', component: () => import('./views/production/RecipesView.vue'), meta: { titleKey: 'nav.productionRecipes', icon: 'pi pi-book' } },
      { path: 'production/recipes/:id', name: 'recipe-detail', component: () => import('./views/production/RecipeDetailView.vue'), meta: { titleKey: 'nav.productionRecipes', icon: 'pi pi-book' } },
      { path: 'production/spc-characteristics', name: 'spc-characteristics', component: () => import('./views/production/SpcCharacteristicsView.vue'), meta: { titleKey: 'nav.spcCharacteristics', icon: 'pi pi-chart-line' } },
      { path: 'production/downtime', name: 'production-downtime', component: () => import('./views/production/DowntimeView.vue'), meta: { titleKey: 'nav.productionDowntime', icon: 'pi pi-pause-circle' } },
      { path: 'production/lots', name: 'production-lots', component: () => import('./views/production/LotsView.vue'), meta: { titleKey: 'nav.productionLots', icon: 'pi pi-box' } },
      { path: 'production/telemetry', name: 'production-telemetry', component: () => import('./views/production/TelemetryView.vue'), meta: { titleKey: 'nav.productionTelemetry', icon: 'pi pi-wave-pulse' } },
      { path: 'production/opcua-connections', name: 'production-opcua-connections', component: () => import('./views/production/OpcUaConnectionsView.vue'), meta: { titleKey: 'nav.productionOpcUaConnections', icon: 'pi pi-link' } },
      { path: 'production/telemetry-dashboard', name: 'production-telemetry-dashboard', component: () => import('./views/production/TelemetryDashboardView.vue'), meta: { titleKey: 'nav.productionTelemetryDashboard', icon: 'pi pi-chart-line' } },
      { path: 'production/kanban', name: 'production-kanban', component: () => import('./views/production/KanbanBoardView.vue'), meta: { titleKey: 'nav.productionKanban', icon: 'pi pi-th-large' } },
      { path: 'schedule', name: 'schedule', component: () => import('./views/production/ScheduleDispatchView.vue'), meta: { titleKey: 'nav.schedule', icon: 'pi pi-calendar' } },
      { path: 'reports', redirect: '/reports/oee' },
      { path: 'reports/oee', name: 'reports-oee', component: () => import('./views/production/OeeDashboardView.vue'), meta: { titleKey: 'nav.oeeDashboard', icon: 'pi pi-chart-bar' } },
      { path: 'reports/reliability', name: 'reports-reliability', component: () => import('./views/production/ReliabilityDashboardView.vue'), meta: { titleKey: 'nav.reliabilityDashboard', icon: 'pi pi-wrench' } },
      { path: 'settings', name: 'settings', component: () => import('./views/ComingSoonView.vue'), meta: { titleKey: 'nav.settings', icon: 'pi pi-cog' } },
      // RBAC management (issue #273): every roles/permissions endpoint
      // requires tenant.admin on the backend, so the route carries the same
      // requirement. Production/configuration list views stay readable on
      // purpose — the backend keeps all browse/get reads open to any
      // authenticated user, and write-gating them would lock read-only
      // shopfloor tablets out of the dispatch board.
      { path: 'settings/roles', name: 'roles', component: () => import('./views/settings/RolesView.vue'), meta: { titleKey: 'nav.roles', icon: 'pi pi-lock', permission: Permissions.TenantAdmin } },
      {
        path: 'configuration',
        redirect: '/configuration/products',
        children: [
          { path: 'products', name: 'products', component: () => import('./views/configuration/ProductsView.vue') },
          { path: 'product-groups', name: 'product-groups', component: () => import('./views/configuration/ProductGroupsView.vue') },
          { path: 'measure-units', name: 'measure-units', component: () => import('./views/configuration/MeasureUnitsView.vue') },
          { path: 'warehouses', name: 'warehouses', component: () => import('./views/configuration/WarehousesView.vue') },
          { path: 'departments', name: 'departments', component: () => import('./views/configuration/DepartmentsView.vue') },
          { path: 'machines', name: 'machines', component: () => import('./views/configuration/MachinesView.vue') },
          { path: 'operators', name: 'operators', component: () => import('./views/configuration/OperatorsView.vue') },
          { path: 'skills', name: 'skills', component: () => import('./views/configuration/SkillsView.vue') },
          { path: 'shifts', name: 'shifts', component: () => import('./views/configuration/ShiftsView.vue') },
          { path: 'reason-codes', name: 'reason-codes', component: () => import('./views/configuration/ReasonCodesView.vue') },
          { path: 'operation-templates', name: 'operation-templates', component: () => import('./views/configuration/OperationTemplatesView.vue') },
          { path: 'maintenance', name: 'maintenance', component: () => import('./views/configuration/MaintenanceView.vue') }
        ]
      }
    ]
  },
  { path: '/:pathMatch(.*)*', redirect: '/dashboard' }
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

/**
 * Permission check for the route guard (issue #273), extracted pure for
 * unit tests: the deepest matched route carrying `meta.permission` wins,
 * and grants are exact-match (parity with the backend AuthorizationBehavior).
 */
export function requiredPermissionFor(route: Pick<RouteLocationNormalized, 'matched'>): string | undefined {
  for (let i = route.matched.length - 1; i >= 0; i -= 1) {
    const required = route.matched[i]?.meta?.['permission'];
    if (typeof required === 'string' && required.length > 0) return required;
  }
  return undefined;
}

export function hasRoutePermission(route: Pick<RouteLocationNormalized, 'matched'>, permissions: string[]): boolean {
  const required = requiredPermissionFor(route);
  if (!required) return true;
  return permissions.includes(required);
}

router.beforeEach(async (to) => {
  // Leaving the page: cancel the previous view's tracked list fetches so a
  // stalled terminal never commits state after navigation.
  abortPendingRequests();
  const auth = useAuthStore();
  const isPublic = to.meta?.public === true;
  if (!isPublic && !auth.isAuthenticated) {
    // In-memory session is gone after a reload, but the httpOnly cookies
    // may still be valid — re-prove the session once before bouncing to
    // login (issue #242). User display info stays unknown until sign-in.
    // The refresh claims also restore permission grants (issue #273).
    const restored = await restoreSession();
    if (!restored.ok) {
      return { name: 'login', query: to.fullPath && to.fullPath !== '/' ? { redirect: to.fullPath } : undefined };
    }
    const user = auth.user ?? (restored.email ? { email: restored.email } : null);
    auth.setAuth(user, restored.permissions);
    // Fall through to the permission gate below: a restored session must
    // satisfy meta.permission exactly like a fresh sign-in.
  }
  if (isPublic && auth.isAuthenticated && (to.name === 'login' || to.name === 'register')) {
    return { name: 'dashboard' };
  }
  // Permission gate (issue #273): direct URL entry is blocked the same way
  // as sidebar navigation — redirect to the dashboard with a toast. Backend
  // 403s remain the enforcer; this keeps users out of views they cannot use.
  if (!hasRoutePermission(to, auth.permissions)) {
    try {
      useToastStore().error(i18n.global.t('errors.accessDenied'));
    } catch { /* ignore - pinia may be unavailable in tests */ }
    return { name: 'dashboard' };
  }
  return true;
});

export default router;
