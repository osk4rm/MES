export interface NavItem {
  /** i18n key or literal label */
  label: string;
  icon: string;
  route?: string;
  children?: NavItem[];
}

export const sitemap: NavItem[] = [
  { label: 'nav.dashboard', icon: 'pi pi-chart-pie', route: '/dashboard' },
  {
    label: 'nav.production',
    icon: 'pi pi-cog',
    children: [
      { label: 'nav.productionOrders', icon: 'pi pi-list', route: '/production/orders' },
      { label: 'nav.productionRecipes', icon: 'pi pi-book', route: '/production/recipes' }
    ]
  },
  { label: 'nav.schedule', icon: 'pi pi-calendar', route: '/schedule' },
  { label: 'nav.reports', icon: 'pi pi-chart-bar', route: '/reports' },
  {
    label: 'nav.configuration',
    icon: 'pi pi-sliders-h',
    children: [
      { label: 'nav.products', icon: 'pi pi-box', route: '/configuration/products' },
      { label: 'nav.productGroups', icon: 'pi pi-tags', route: '/configuration/product-groups' },
      { label: 'nav.measureUnits', icon: 'pi pi-percentage', route: '/configuration/measure-units' },
      { label: 'nav.warehouses', icon: 'pi pi-building', route: '/configuration/warehouses' },
      { label: 'nav.departments', icon: 'pi pi-sitemap', route: '/configuration/departments' },
      { label: 'nav.operators', icon: 'pi pi-id-card', route: '/configuration/operators' }
    ]
  },
  { label: 'nav.settings', icon: 'pi pi-cog', route: '/settings' }
];
