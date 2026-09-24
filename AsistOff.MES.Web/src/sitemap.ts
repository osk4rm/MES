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
      { label: 'nav.productionScrap', icon: 'pi pi-trash', route: '/production/scrap' },
      { label: 'nav.productionRecipes', icon: 'pi pi-book', route: '/production/recipes' },
      { label: 'nav.productionDowntime', icon: 'pi pi-pause-circle', route: '/production/downtime' }
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
      { label: 'nav.machines', icon: 'pi pi-cog', route: '/configuration/machines' },
      { label: 'nav.operators', icon: 'pi pi-id-card', route: '/configuration/operators' },
      { label: 'nav.skills', icon: 'pi pi-star', route: '/configuration/skills' },
      { label: 'nav.reasonCodes', icon: 'pi pi-exclamation-circle', route: '/configuration/reason-codes' },
      { label: 'nav.operationTemplates', icon: 'pi pi-copy', route: '/configuration/operation-templates' }
    ]
  },
  { label: 'nav.settings', icon: 'pi pi-cog', route: '/settings' }
];
