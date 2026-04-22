import { createRouter, createWebHistory } from 'vue-router';
import type { RouteRecordRaw } from 'vue-router';
import LoginView from './views/LoginView.vue';
import RegisterView from './views/RegisterView.vue';

import DashboardView from './views/DashboardView.vue';
import ProductionView from './views/ProductionView.vue';
import ReportsView from './views/ReportsView.vue';
import WarehousesView from './views/WarehousesView.vue';
import ScheduleView from './views/ScheduleView.vue';

const routes: RouteRecordRaw[] = [
  { path: '/', name: 'Login', component: LoginView },
  { path: '/register', name: 'Register', component: RegisterView },
  {
    path: '/',
    component: () => import('./components/MainLayout.vue'),
    children: [
      { path: 'dashboard', name: 'Dashboard', component: DashboardView },
      { path: 'production-orders', name: 'ProductionOrders', component: ProductionView },
      { path: 'production-recipes', name: 'ProductionRecipes', component: () => import('./views/ProductionView.vue') },
      { path: 'schedule', name: 'Schedule', component: ScheduleView },
      { path: 'report', name: 'Report', component: ReportsView },
      {
        path: 'configuration',
        name: 'Configuration',
        redirect: '/configuration/warehouses',
        children: [
          { path: 'warehouses', name: 'Warehouses', component: WarehousesView },
          { 
            path: 'departments', 
            name: 'Departments', 
            component: () => import('./views/DepartmentsView.vue')
          },
        ]
      },
    ]
  },
];

const router = createRouter({
  history: createWebHistory(),
  routes
});

export default router;
