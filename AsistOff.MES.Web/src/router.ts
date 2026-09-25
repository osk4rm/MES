import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import { useAuthStore } from './stores/authStore';

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
      { path: 'settings/roles', name: 'roles', component: () => import('./views/settings/RolesView.vue'), meta: { titleKey: 'nav.roles', icon: 'pi pi-lock' } },
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

router.beforeEach((to) => {
  const auth = useAuthStore();
  if (!auth.token) auth.loadAuth();
  const isPublic = to.meta?.public === true;
  if (!isPublic && !auth.isAuthenticated) {
    return { name: 'login', query: to.fullPath && to.fullPath !== '/' ? { redirect: to.fullPath } : undefined };
  }
  if (isPublic && auth.isAuthenticated && (to.name === 'login' || to.name === 'register')) {
    return { name: 'dashboard' };
  }
  return true;
});

export default router;
