# Industrial MES Components

This document outlines the new industrial-themed components created for the AsistOff MES application.

## Components Created

### 1. **IndustrialButton**
**Path:** `src/components/IndustrialButton.vue`

**Features:**
- Multiple variants: primary, secondary, danger, success, warning
- Three sizes: small, medium, large  
- Loading states with spinner
- Icon support (prefix)
- Block-level option
- Industrial gradient effects and hover animations
- Consistent dark theme styling

**Usage:**
```vue
<IndustrialButton 
  variant="primary" 
  icon="pi pi-plus"
  @click="handleAction"
>
  Add Item
</IndustrialButton>
```

### 2. **PageHeader**  
**Path:** `src/components/PageHeader.vue`

**Features:**
- Page title with optional icon
- Subtitle support
- Action buttons slot
- Gradient accent border
- Responsive design
- Dark industrial styling

**Usage:**
```vue
<PageHeader 
  title="Warehouses" 
  icon="pi pi-building"
  subtitle="Manage your warehouse locations"
>
  <template #actions>
    <IndustrialButton>Add</IndustrialButton>
  </template>
</PageHeader>
```

### 3. **IndustrialInput**
**Path:** `src/components/IndustrialInput.vue`

**Features:**
- Dark theme styling
- Prefix/suffix icon support
- Loading indicator
- Error and helper text
- Three sizes: small, medium, large
- Focus glow effects
- Form validation ready

**Usage:**
```vue
<IndustrialInput
  v-model="searchValue"
  label="Search"
  placeholder="Enter search term..."
  prefix-icon="pi pi-search"
  :error="validationError"
/>
```

### 4. **FilterBar**
**Path:** `src/components/FilterBar.vue`

**Features:**
- Container for filter inputs
- Built-in clear functionality
- Gradient accent styling
- Responsive layout
- Dark industrial theme

**Usage:**
```vue
<FilterBar @clear="clearAllFilters">
  <IndustrialInput v-model="nameFilter" placeholder="Filter by name..." />
  <IndustrialInput v-model="statusFilter" placeholder="Filter by status..." />
</FilterBar>
```

### 5. **ActionButtons**
**Path:** `src/components/ActionButtons.vue`

**Features:**
- Configurable action array
- Consistent button styling
- Loading and disabled states
- Tooltip support
- Responsive layout

**Usage:**
```vue
<ActionButtons 
  :actions="rowActions" 
  @action="handleRowAction"
/>
```

### 6. **StatusBadge**
**Path:** `src/components/StatusBadge.vue`

**Features:**
- Multiple status variants: success, warning, danger, info, neutral, active, inactive
- Outline and filled styles
- Pulse animation option
- Loading states
- Icon support
- Industrial gradient styling

**Usage:**
```vue
<StatusBadge 
  variant="success" 
  icon="pi pi-check"
  :pulse="true"
>
  Active
</StatusBadge>
```

### 7. **WidgetCard**
**Path:** `src/components/WidgetCard.vue`

**Features:**
- Dashboard widget container
- Value display with units
- Status indicators
- Action buttons integration
- Loading states
- Click handlers
- Gradient accents and hover effects

**Usage:**
```vue
<WidgetCard
  title="Production Status"
  icon="pi pi-play"
  :value="85"
  unit="%"
  variant="success"
  :status="{ variant: 'success', label: 'Running' }"
  @click="navigateToProduction"
/>
```

### 8. **ConfirmDialog**
**Path:** `src/components/ConfirmDialog.vue`

**Features:**
- Dark themed confirmation modal
- Loading states
- Configurable messages and buttons
- Keyboard navigation (ESC key)
- Industrial styling consistent with theme

**Usage:**
```vue
<ConfirmDialog
  :is-visible="showDialog"
  title="Delete Item"
  message="Are you sure you want to delete this item?"
  @confirm="handleConfirm"
  @cancel="handleCancel"
/>
```

## Updated Views

### WarehousesView.vue
- Now uses PageHeader component
- FilterBar with IndustrialInput components
- ActionButtons for row actions
- Clean, minimal styling relying on component styles

### DashboardView.vue  
- PageHeader integration
- WidgetCard components for dashboard metrics
- Enhanced welcome section
- Responsive grid layout

## Design System Features

### Color Palette
- **Primary:** Purple gradients (#6366f1 to #8b5cf6)
- **Secondary:** Slate grays (#475569 to #64748b)
- **Success:** Green (#10b981 to #059669)
- **Warning:** Amber (#f59e0b to #d97706)
- **Danger:** Red (#ef4444 to #dc2626)
- **Background:** Dark slate (#1e293b to #334155)

### Typography
- **Primary Text:** Light gray (#f1f5f9)
- **Secondary Text:** Medium gray (#cbd5e1)
- **Muted Text:** Dark gray (#94a3b8)

### Effects
- Gradient backgrounds and borders
- Drop shadows with color-matched glows
- Hover animations and transforms
- Loading states and transitions
- Industrial glow effects

## Benefits

1. **Consistency:** All components follow the same design language
2. **Reusability:** Components are highly configurable and reusable
3. **Maintainability:** Centralized styling and behavior
4. **Accessibility:** Proper focus states, keyboard navigation, and contrast
5. **Responsiveness:** Mobile-first responsive design
6. **Performance:** Optimized CSS and minimal DOM manipulation
7. **Developer Experience:** TypeScript support with proper interfaces

## Usage Guidelines

1. **Always use IndustrialButton** instead of native HTML buttons
2. **Wrap page content** with PageHeader for consistency  
3. **Use StatusBadge** for any status indicators
4. **Implement ActionButtons** for data grid actions
5. **Use IndustrialInput** for all form inputs
6. **Wrap filters** in FilterBar component

This component system provides a solid foundation for building consistent, professional-looking industrial MES interfaces while maintaining excellent developer experience and performance.
