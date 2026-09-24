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
      { label: 'nav.productionDowntime', icon: 'pi pi-pause-circle', route: '/production/downtime' },
      { label: 'nav.productionLots', icon: 'pi pi-box', route: '/production/lots' },
      { label: 'nav.productionScrap', icon: 'pi pi-trash', route: '/production/scrap' },
      { label: 'nav.productionAndon', icon: 'pi pi-bell', route: '/production/andon' },
      { label: 'nav.productionRecipes', icon: 'pi pi-book', route: '/production/recipes' },
      { label: 'nav.spcCharacteristics', icon: 'pi pi-chart-line', route: '/production/spc-characteristics' },
      { label: 'nav.productionTelemetry', icon: 'pi pi-wave-pulse', route: '/production/telemetry' },
      { label: 'nav.productionOpcUaConnections', icon: 'pi pi-link', route: '/production/opcua-connections' },
      { label: 'nav.productionTelemetryDashboard', icon: 'pi pi-chart-line', route: '/production/telemetry-dashboard' },
      { label: 'nav.productionKanban', icon: 'pi pi-th-large', route: '/production/kanban' }
    ]
  },
  { label: 'nav.schedule', icon: 'pi pi-calendar', route: '/schedule' },
  {
    label: 'nav.reports',
    icon: 'pi pi-chart-bar',
    children: [
      { label: 'nav.oeeDashboard', icon: 'pi pi-chart-bar', route: '/reports/oee' },
      { label: 'nav.reliabilityDashboard', icon: 'pi pi-wrench', route: '/reports/reliability' }
    ]
  },
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
      { label: 'nav.shifts', icon: 'pi pi-clock', route: '/configuration/shifts' },
      { label: 'nav.reasonCodes', icon: 'pi pi-exclamation-circle', route: '/configuration/reason-codes' },
      { label: 'nav.operationTemplates', icon: 'pi pi-copy', route: '/configuration/operation-templates' }
    ]
  },
  { label: 'nav.settings', icon: 'pi pi-cog', route: '/settings' }
];
