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
      { path: 'production/orders', name: 'production-orders', component: () => import('./views/ComingSoonView.vue'), meta: { titleKey: 'nav.productionOrders', icon: 'pi pi-list' } },
      { path: 'production/scrap', name: 'production-scrap', component: () => import('./views/production/ScrapView.vue'), meta: { titleKey: 'nav.productionScrap', icon: 'pi pi-trash' } },
      { path: 'production/recipes', name: 'production-recipes', component: () => import('./views/production/RecipesView.vue'), meta: { titleKey: 'nav.productionRecipes', icon: 'pi pi-book' } },
      { path: 'production/recipes/:id', name: 'recipe-detail', component: () => import('./views/production/RecipeDetailView.vue'), meta: { titleKey: 'nav.productionRecipes', icon: 'pi pi-book' } },
      { path: 'production/lots', name: 'production-lots', component: () => import('./views/production/LotsView.vue'), meta: { titleKey: 'nav.productionLots', icon: 'pi pi-box' } },
      { path: 'schedule', name: 'schedule', component: () => import('./views/ComingSoonView.vue'), meta: { titleKey: 'nav.schedule', icon: 'pi pi-calendar' } },
      { path: 'reports', name: 'reports', component: () => import('./views/ComingSoonView.vue'), meta: { titleKey: 'nav.reports', icon: 'pi pi-chart-bar' } },
      { path: 'settings', name: 'settings', component: () => import('./views/ComingSoonView.vue'), meta: { titleKey: 'nav.settings', icon: 'pi pi-cog' } },
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
          { path: 'reason-codes', name: 'reason-codes', component: () => import('./views/configuration/ReasonCodesView.vue') },
          { path: 'operation-templates', name: 'operation-templates', component: () => import('./views/configuration/OperationTemplatesView.vue') }
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
