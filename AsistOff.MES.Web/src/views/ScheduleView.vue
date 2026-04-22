<template>
  <div class="schedule-view">
    <PageHeader 
      title="Production Schedule" 
      icon="pi pi-calendar"
      subtitle="Manage production operations and machine scheduling in real-time"
    >
      <template #actions>
        <IndustrialButton
          variant="secondary"
          icon="pi pi-refresh"
          @click="refreshSchedule"
        >
          Refresh
        </IndustrialButton>
      </template>
    </PageHeader>

    <!-- Schedule Controls -->
    <div class="schedule-controls">
      <FilterBar @clear="clearFilters">
        <FloatingLabelInput
          v-model="machineFilter"
          label="Filter machines"
          prefix-icon="pi pi-search"
          size="small"
        />
        <FloatingLabelInput
          v-model="statusFilter"
          label="Filter by status"
          prefix-icon="pi pi-filter"
          size="small"
        />
        <div class="date-range">
          <FloatingLabelInput
            id="start-date"
            v-model="dateRange.start"
            label="From"
            type="date"
            size="small"
          />
          <FloatingLabelInput
            id="end-date"
            v-model="dateRange.end"
            label="To"
            type="date"
            size="small"
          />
        </div>
      </FilterBar>
    </div>

    <!-- Scheduler Chart -->
    <div class="scheduler-container">
      <div class="scheduler-header">
        <div class="machine-column">
          <div class="header-cell">Machines</div>
        </div>
        <div class="timeline-header" ref="timelineHeader">
          <div
            v-for="hour in timelineHours"
            :key="hour.timestamp"
            class="time-slot"
            :style="{ width: slotWidth + 'px' }"
          >
            <div class="time-label">{{ hour.label }}</div>
            <div class="date-label">{{ hour.date }}</div>
          </div>
        </div>
      </div>

      <div class="scheduler-body">
        <div class="machine-list">
          <div
            v-for="machine in filteredMachines"
            :key="machine.id"
            class="machine-row"
            :class="{ 'machine-offline': machine.status === 'offline' }"
          >
            <div class="machine-info">
              <div class="machine-name">{{ machine.name }}</div>
              <div class="machine-status">
                <StatusBadge
                  :variant="getStatusVariant(machine.status)"
                  :label="machine.status"
                  size="small"
                />
              </div>
              <div class="machine-efficiency">{{ machine.efficiency }}%</div>
            </div>
          </div>
        </div>

        <div class="timeline-body" @scroll="handleScroll">
          <div
            v-for="machine in filteredMachines"
            :key="machine.id"
            class="timeline-row"
          >
            <!-- Debug: Show machine operations count -->
            <div v-if="machine.operations.length === 0" class="no-operations">
              No operations scheduled
            </div>
            <div
              v-for="operation in machine.operations"
              :key="operation.id"
              class="operation-bar"
              :class="[
                `operation-${operation.status}`,
                { 'operation-po-highlighted': hoveredPO === operation.orderNumber }
              ]"
              :style="{ 
                ...getOperationStyle(operation),
                '--po-color': operation.poColor,
                background: operation.poColor,
                borderColor: operation.poColor
              }"
              @click="selectOperation(operation)"
              @mouseenter="hoveredPO = operation.orderNumber"
              @mouseleave="hoveredPO = null"
              :title="getOperationTooltip(operation)"
            >
              <div class="operation-content">
                <div class="operation-po">{{ operation.orderNumber }}</div>
                <div class="operation-title">{{ operation.operationNumber }}</div>
                <div class="operation-name">{{ operation.operationName }}</div>
                <div class="operation-progress">
                  <div 
                    class="progress-bar" 
                    :style="{ width: operation.progress + '%' }"
                  ></div>
                </div>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>

    <!-- Operation Details Modal -->
    <div v-if="selectedOperation" class="operation-modal-overlay" @click="closeModal">
      <div class="operation-modal" @click.stop>
        <div class="modal-header">
          <h3>Operation Details</h3>
          <button class="close-btn" @click="closeModal">
            <i class="pi pi-times"></i>
          </button>
        </div>
        <div class="modal-body">
          <div class="detail-group">
            <label>Order Number:</label>
            <span>{{ selectedOperation.orderNumber }}</span>
          </div>
          <div class="detail-group">
            <label>Product:</label>
            <span>{{ selectedOperation.productName }}</span>
          </div>
          <div class="detail-group">
            <label>Machine:</label>
            <span>{{ selectedOperation.machineName }}</span>
          </div>
          <div class="detail-group">
            <label>Status:</label>
            <StatusBadge
              :variant="getStatusVariant(selectedOperation.status)"
              :label="selectedOperation.status"
              size="small"
            />
          </div>
          <div class="detail-group">
            <label>Progress:</label>
            <span>{{ selectedOperation.progress }}%</span>
          </div>
          <div class="detail-group">
            <label>Duration:</label>
            <span>{{ formatDuration(selectedOperation.duration) }}</span>
          </div>
          <div class="detail-group">
            <label>Start Time:</label>
            <span>{{ formatDateTime(selectedOperation.startTime) }}</span>
          </div>
          <div class="detail-group">
            <label>End Time:</label>
            <span>{{ formatDateTime(selectedOperation.endTime) }}</span>
          </div>
        </div>
        <div class="modal-actions">
          <IndustrialButton variant="secondary" @click="closeModal">
            Close
          </IndustrialButton>
        </div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref, computed, onMounted } from 'vue';
import PageHeader from '../components/PageHeader.vue';
import IndustrialButton from '../components/IndustrialButton.vue';
import FloatingLabelInput from '../components/FloatingLabelInput.vue';
import FilterBar from '../components/FilterBar.vue';
import StatusBadge from '../components/StatusBadge.vue';

interface Operation {
  id: string;
  orderNumber: string;
  operationNumber: string;
  operationName: string;
  productName: string;
  machineName: string;
  startTime: Date;
  endTime: Date;
  duration: number; // in minutes
  status: 'scheduled' | 'running' | 'completed' | 'delayed' | 'paused';
  progress: number;
  priority: 'low' | 'medium' | 'high' | 'urgent';
  quantity: number;
  completedQuantity: number;
  poColor: string; // Add color property for PO grouping
}

interface Machine {
  id: string;
  name: string;
  status: 'online' | 'offline' | 'maintenance' | 'error';
  efficiency: number;
  operations: Operation[];
}

interface TimeSlot {
  timestamp: number;
  label: string;
  date: string;
}

// Reactive data
const machineFilter = ref('');
const statusFilter = ref('');
const dateRange = ref({
  start: '2025-08-10',
  end: '2025-08-10'
});
const selectedOperation = ref<Operation | null>(null);
const hoveredPO = ref<string | null>(null);
const timelineHeader = ref<HTMLElement>();
const slotWidth = 120; // pixels per hour

// Generate dummy data
const generateDummyData = (): Machine[] => {
  const machines: Machine[] = [];
  const statuses: ('scheduled' | 'running' | 'completed' | 'delayed' | 'paused')[] = 
    ['scheduled', 'running', 'completed', 'delayed', 'paused'];
  const priorities: ('low' | 'medium' | 'high' | 'urgent')[] = 
    ['low', 'medium', 'high', 'urgent'];
  const products = [
    'Steel Frame A-100', 'Aluminum Panel B-200', 'Carbon Fiber C-300', 
    'Titanium Bracket D-400', 'Copper Wire E-500', 'Plastic Housing F-600',
    'Glass Cover G-700', 'Rubber Seal H-800', 'Metal Fastener I-900', 
    'Composite Board J-1000'
  ];
  const operationNames = [
    'Cutting', 'Welding', 'Assembly', 'Inspection', 'Packaging', 
    'Machining', 'Painting', 'Testing', 'Polishing', 'Drilling',
    'Grinding', 'Bending', 'Stamping', 'Molding', 'Finishing'
  ];

  // PO color palette - strategically ordered for MAXIMUM contrast between consecutive colors
  const poColors = [
    '#dc2626',  // 1. RED
    '#0891b2',  // 2. CYAN (opposite of red)
    '#ca8a04',  // 3. GOLD/YELLOW (warm, different from cyan)
    '#6b21a8',  // 4. DEEP PURPLE (cool, opposite of yellow)
    '#047857',  // 5. FOREST GREEN (opposite of purple)
    '#b45309',  // 6. DARK AMBER/ORANGE (warm, opposite of green)
    '#1d4ed8',  // 7. NAVY BLUE (cool, opposite of orange)
    '#9a3412',  // 8. DEEP RUST/BROWN (warm, opposite of blue)
    '#15803d',  // 9. DARK GREEN (cool, opposite of rust)
    '#be185d',  // 10. MAGENTA/PINK (warm, opposite of green)
    '#075985',  // 11. DEEP CYAN (cool, opposite of magenta)
    '#d97706',  // 12. DARK ORANGE (warm, opposite of cyan)
    '#4338ca',  // 13. DARK INDIGO (cool, opposite of orange)
    '#991b1b',  // 14. DARK MAROON (warm, opposite of indigo)
    '#0d9488',  // 15. DARK TEAL (cool, opposite of maroon)
    '#7c2d12',  // 16. DARK BROWN (warm, opposite of teal)
    '#2563eb',  // 17. ROYAL BLUE (cool, opposite of brown)
    '#c2410c',  // 18. RUST ORANGE (warm, opposite of blue)
    '#166534',  // 19. VERY DARK GREEN (cool, opposite of rust)
    '#e11d48',  // 20. ROSE/PINK (warm, opposite of dark green)
    '#134e4a',  // 21. VERY DARK TEAL (cool, opposite of rose)
    '#9333ea',  // 22. AMETHYST (warm purple, opposite of teal)
    '#059669',  // 23. DARK EMERALD (cool green, opposite of purple)
    '#6d28d9',  // 24. DEEP VIOLET (warm purple, opposite of emerald)
    '#65a30d',  // 25. OLIVE GREEN (cool, opposite of violet)
    '#b91c1c',  // 26. DEEP CRIMSON (warm red, opposite of olive)
    '#0369a1',  // 27. DARK SKY BLUE (cool, opposite of crimson)
    '#7c3aed',  // 28. MUTED PURPLE (warm, opposite of sky blue)
    '#4d7c0f',  // 29. DARK LIME (cool, opposite of purple)
    '#1e40af'   // 30. DEEP BLUE (cool, final contrasting color)
  ];

  // Create global PO pool that can span across machines
  const globalPOs: { [key: string]: string } = {};
  const availablePOs: string[] = [];
  let colorIndex = 0;
  
  // Pre-generate POs that will be shared across machines
  for (let po = 1; po <= 15; po++) { // Create 15 different POs
    const poNumber = `PO-${2025000 + po}`;
    globalPOs[poNumber] = poColors[colorIndex % poColors.length];
    availablePOs.push(poNumber);
    colorIndex++;
  }

  // Target date: August 10, 2025
  const targetDate = new Date(2025, 7, 10); // Month is 0-indexed, so 7 = August

  for (let i = 1; i <= 6; i++) { // 6 machines
    const machineStatuses: ('online' | 'offline' | 'maintenance' | 'error')[] = 
      ['online', 'online', 'online', 'online', 'maintenance', 'offline'];
    const machineStatus = machineStatuses[Math.floor(Math.random() * machineStatuses.length)];
    
    const operations: Operation[] = [];
    const operationCount = machineStatus === 'online' ? Math.floor(Math.random() * 4) + 3 : 
                          machineStatus === 'maintenance' ? Math.floor(Math.random() * 2) + 1 : 0;
    
    for (let j = 1; j <= operationCount; j++) {
      // More random start times throughout the day (0:01 to 20:00)
      const randomHour = Math.floor(Math.random() * 20); // 0 to 19 hours
      const randomMinute = Math.floor(Math.random() * 60); // 0 to 59 minutes
      const currentTime = new Date(targetDate);
      currentTime.setHours(randomHour, randomMinute, 0, 0);
      
      const duration = Math.floor(Math.random() * 240) + 60; // 1-4 hours in minutes
      const endTime = new Date(currentTime.getTime() + duration * 60 * 1000);
      
      // Ensure we don't go past the target date
      if (endTime.getDate() !== targetDate.getDate()) {
        // Reduce duration to fit within the day
        const remainingMinutes = (targetDate.getTime() + 24 * 60 * 60 * 1000 - currentTime.getTime()) / (1000 * 60);
        if (remainingMinutes > 30) { // Only create operation if at least 30 minutes remain
          endTime.setTime(currentTime.getTime() + Math.min(duration, remainingMinutes - 10) * 60 * 1000);
        } else {
          continue; // Skip this operation
        }
      }
      
      const status = statuses[Math.floor(Math.random() * statuses.length)];
      const quantity = Math.floor(Math.random() * 1000) + 100;
      
      // Select PO from global pool with higher chance of reusing recent POs (for cross-machine operations)
      let poNumber: string;
      if (Math.random() < 0.4 && operations.length > 0) {
        // 40% chance to reuse a PO from current machine (for sequential operations)
        poNumber = operations[Math.floor(Math.random() * operations.length)].orderNumber;
      } else if (Math.random() < 0.3) {
        // 30% chance to pick a completely random PO from the global pool (for cross-machine operations)
        poNumber = availablePOs[Math.floor(Math.random() * availablePOs.length)];
      } else {
        // 30% chance to use a weighted selection favoring recent POs across all machines
        const recentPOsWeight = Math.min(5, availablePOs.length); // Weight recent 5 POs more heavily
        if (Math.random() < 0.6) {
          // Pick from recent POs
          const recentIndex = Math.floor(Math.random() * recentPOsWeight);
          poNumber = availablePOs[availablePOs.length - 1 - recentIndex];
        } else {
          // Pick any PO
          poNumber = availablePOs[Math.floor(Math.random() * availablePOs.length)];
        }
      }
      
      let progress = 0;
      let completedQuantity = 0;
      switch (status) {
        case 'completed':
          progress = 100;
          completedQuantity = quantity;
          break;
        case 'running':
          progress = Math.floor(Math.random() * 70) + 10;
          completedQuantity = Math.floor((progress / 100) * quantity);
          break;
        case 'paused':
        case 'delayed':
          progress = Math.floor(Math.random() * 50) + 5;
          completedQuantity = Math.floor((progress / 100) * quantity);
          break;
        case 'scheduled':
          progress = 0;
          completedQuantity = 0;
          break;
      }

      operations.push({
        id: `op-${i}-${j}`,
        orderNumber: poNumber,
        operationNumber: `OP-${(j * 10).toString().padStart(3, '0')}`,
        operationName: operationNames[Math.floor(Math.random() * operationNames.length)],
        productName: products[Math.floor(Math.random() * products.length)],
        machineName: `Machine ${String.fromCharCode(65 + i - 1)}${i.toString().padStart(2, '0')}`,
        startTime: new Date(currentTime),
        endTime: endTime,
        duration: duration,
        status: status,
        progress: progress,
        priority: priorities[Math.floor(Math.random() * priorities.length)],
        quantity: quantity,
        completedQuantity: completedQuantity,
        poColor: globalPOs[poNumber]
      });
    }

    // Sort operations by start time and resolve overlaps
    operations.sort((a, b) => a.startTime.getTime() - b.startTime.getTime());
    
    // Adjust overlapping operations
    for (let k = 1; k < operations.length; k++) {
      const prevOperation = operations[k - 1];
      const currentOperation = operations[k];
      
      // If current operation starts before previous one ends, adjust it
      if (currentOperation.startTime.getTime() < prevOperation.endTime.getTime()) {
        // Add 5-15 minute gap after previous operation
        const gapMinutes = Math.floor(Math.random() * 10) + 5;
        const newStartTime = new Date(prevOperation.endTime.getTime() + gapMinutes * 60 * 1000);
        const newEndTime = new Date(newStartTime.getTime() + currentOperation.duration * 60 * 1000);
        
        // Check if the adjusted operation fits within the day
        if (newEndTime.getDate() === targetDate.getDate() && newEndTime.getHours() < 24) {
          currentOperation.startTime = newStartTime;
          currentOperation.endTime = newEndTime;
        } else {
          // If it doesn't fit, remove this operation
          operations.splice(k, 1);
          k--; // Adjust index since we removed an element
        }
      }
    }

    machines.push({
      id: `machine-${i}`,
      name: `Machine ${String.fromCharCode(65 + i - 1)}${i.toString().padStart(2, '0')}`,
      status: machineStatus,
      efficiency: Math.floor(Math.random() * 40) + 60,
      operations: operations
    });
  }

  console.log('Generated machines for August 10, 2025:', machines);
  return machines;
};

const machines = ref<Machine[]>(generateDummyData());

// Computed properties
const filteredMachines = computed(() => {
  return machines.value.filter(machine => {
    const matchesMachine = !machineFilter.value || 
      machine.name.toLowerCase().includes(machineFilter.value.toLowerCase());
    const matchesStatus = !statusFilter.value || 
      machine.status.toLowerCase().includes(statusFilter.value.toLowerCase()) ||
      machine.operations.some(op => 
        op.status.toLowerCase().includes(statusFilter.value.toLowerCase())
      );
    return matchesMachine && matchesStatus;
  });
});

const timelineHours = computed((): TimeSlot[] => {
  const slots: TimeSlot[] = [];
  const start = new Date(dateRange.value.start);
  const end = new Date(dateRange.value.end);
  
  const current = new Date(start);
  current.setHours(0, 0, 0, 0);
  
  while (current <= end) {
    for (let hour = 0; hour < 24; hour += 2) { // 2-hour slots
      const slotTime = new Date(current);
      slotTime.setHours(hour);
      
      slots.push({
        timestamp: slotTime.getTime(),
        label: slotTime.toLocaleTimeString('en-US', { 
          hour: '2-digit', 
          minute: '2-digit',
          hour12: false 
        }),
        date: slotTime.toLocaleDateString('en-US', { 
          month: 'short', 
          day: 'numeric' 
        })
      });
    }
    current.setDate(current.getDate() + 1);
  }
  
  return slots;
});

// Methods
const getOperationStyle = (operation: Operation) => {
  const startTime = new Date(dateRange.value.start);
  startTime.setHours(0, 0, 0, 0);
  
  const opStart = operation.startTime.getTime();
  const opEnd = operation.endTime.getTime();
  const timelineStart = startTime.getTime();
  
  const hoursFromStart = (opStart - timelineStart) / (1000 * 60 * 60);
  const durationHours = (opEnd - opStart) / (1000 * 60 * 60);
  
  // Each 2-hour slot is slotWidth pixels wide
  const left = hoursFromStart * (slotWidth / 2);
  const width = durationHours * (slotWidth / 2);
  
  console.log(`Operation ${operation.orderNumber}:`, {
    startTime: operation.startTime,
    endTime: operation.endTime,
    hoursFromStart,
    durationHours,
    left,
    width
  });
  
  return {
    left: `${Math.max(left, 0)}px`,
    width: `${Math.max(width, 20)}px`
  };
};

const getStatusVariant = (status: string): 'success' | 'warning' | 'danger' | 'info' | 'neutral' => {
  switch (status) {
    case 'online':
    case 'running':
    case 'completed':
      return 'success';
    case 'scheduled':
    case 'paused':
      return 'info';
    case 'delayed':
    case 'maintenance':
      return 'warning';
    case 'offline':
    case 'error':
      return 'danger';
    default:
      return 'neutral';
  }
};

const getOperationTooltip = (operation: Operation): string => {
  return `${operation.operationNumber} - ${operation.operationName}
Order: ${operation.orderNumber}
Product: ${operation.productName}
Status: ${operation.status}
Progress: ${operation.progress}%
Duration: ${formatDuration(operation.duration)}
${formatDateTime(operation.startTime)} - ${formatDateTime(operation.endTime)}`;
};

const formatDuration = (minutes: number): string => {
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return `${hours}h ${mins}m`;
};

const formatDateTime = (date: Date): string => {
  return date.toLocaleString('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
    hour12: false
  });
};

const selectOperation = (operation: Operation) => {
  selectedOperation.value = operation;
};

const closeModal = () => {
  selectedOperation.value = null;
};

const refreshSchedule = () => {
  machines.value = generateDummyData();
};

const clearFilters = () => {
  machineFilter.value = '';
  statusFilter.value = '';
};

const handleScroll = (event: Event) => {
  const target = event.target as HTMLElement;
  if (timelineHeader.value) {
    timelineHeader.value.scrollLeft = target.scrollLeft;
  }
};

onMounted(() => {
  // Set date range to August 10, 2025
  dateRange.value.start = '2025-08-10';
  dateRange.value.end = '2025-08-10';
  
  console.log('Date range set to:', dateRange.value);
  console.log('Machines count:', machines.value.length);
  console.log('Filtered machines count:', filteredMachines.value.length);
  console.log('Timeline hours count:', timelineHours.value.length);
});
</script>

<style scoped>
.schedule-view {
  display: flex;
  flex-direction: column;
  gap: 20px;
  min-height: 100%;
}

.schedule-controls {
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.date-range {
  display: flex;
  gap: 16px;
  align-items: center;
}

.scheduler-container {
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
  border: 1px solid #475569;
  border-radius: 12px;
  overflow: hidden;
  box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
}

.scheduler-header {
  display: flex;
  background: linear-gradient(135deg, #334155 0%, #475569 100%);
  border-bottom: 2px solid #fc913a;
  position: sticky;
  top: 0;
  z-index: 10;
}

.machine-column {
  width: 250px;
  flex-shrink: 0;
  border-right: 1px solid #64748b;
}

.header-cell {
  padding: 16px;
  font-weight: 600;
  color: #f1f5f9;
  text-align: center;
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  background-clip: text;
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
}

.timeline-header {
  display: flex;
  overflow-x: hidden;
  flex: 1;
  /* Hide scrollbar while maintaining scroll functionality */
  scrollbar-width: none; /* Firefox */
  -ms-overflow-style: none; /* Internet Explorer and Edge */
}

.timeline-header::-webkit-scrollbar {
  display: none; /* Chrome, Safari, and Opera */
}

.time-slot {
  border-right: 1px solid #64748b;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 8px 4px;
  min-width: 0;
}

.time-label {
  font-weight: 600;
  color: #f1f5f9;
  font-size: 12px;
}

.date-label {
  color: #94a3b8;
  font-size: 10px;
}

.scheduler-body {
  display: flex;
}

.machine-list {
  width: 250px;
  flex-shrink: 0;
  border-right: 1px solid #64748b;
}

.machine-row {
  border-bottom: 1px solid #64748b;
  height: 80px;
  display: flex;
  align-items: center;
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
  transition: background 0.2s ease;
}

.machine-row.machine-offline {
  background: linear-gradient(135deg, #1e293b 0%, #2d1b2d 100%);
}

.machine-info {
  padding: 12px 16px;
  width: 100%;
  display: flex;
  flex-direction: column;
  gap: 4px;
}

.machine-name {
  font-weight: 600;
  color: #f1f5f9;
  font-size: 14px;
}

.machine-status {
  display: flex;
  align-items: center;
}

.machine-efficiency {
  color: #94a3b8;
  font-size: 12px;
}

.timeline-body {
  flex: 1;
  position: relative;
  overflow-x: auto;
  /* Hide scrollbar while maintaining scroll functionality */
  scrollbar-width: none; /* Firefox */
  -ms-overflow-style: none; /* Internet Explorer and Edge */
}

.timeline-body::-webkit-scrollbar {
  display: none; /* Chrome, Safari, and Opera */
}

.timeline-row {
  border-bottom: 1px solid #64748b;
  height: 80px;
  position: relative;
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
}

.no-operations {
  position: absolute;
  top: 50%;
  left: 20px;
  transform: translateY(-50%);
  color: #94a3b8;
  font-style: italic;
  font-size: 12px;
}

.operation-bar {
  position: absolute;
  top: 8px;
  height: 64px;
  border-radius: 8px;
  cursor: pointer;
  transition: transform 0.2s ease, box-shadow 0.2s ease;
  border: 2px solid transparent;
  overflow: hidden;
  min-width: 20px;
  /* Use PO color as base, with status-specific modifications */
}

.operation-scheduled {
  filter: saturate(0.8);
}

.operation-running {
  box-shadow: 0 0 12px rgba(var(--po-color-rgb, 16, 185, 129), 0.4);
  filter: brightness(1.1);
}

.operation-completed {
  filter: saturate(0.8) brightness(0.95);
}

.operation-delayed {
  filter: hue-rotate(20deg) saturate(1.2);
}

.operation-paused {
  filter: saturate(0.7) brightness(0.9);
}

.operation-bar:hover {
  transform: translateY(-2px);
  box-shadow: 0 4px 16px rgba(0, 0, 0, 0.3);
  z-index: 5;
}

/* Highlight all operations from the same PO */
.operation-po-highlighted {
  outline: 3px solid #fc913a;
  outline-offset: 2px;
  z-index: 10;
  filter: brightness(1.2) saturate(1.1);
  transform: translateY(-1px);
}

.operation-content {
  padding: 4px 6px;
  height: 100%;
  display: flex;
  flex-direction: column;
  justify-content: space-between;
  color: white;
  font-size: 11px;
  gap: 1px;
}

.operation-po {
  font-weight: 700;
  font-size: 10px;
  opacity: 0.9;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
}

.operation-title {
  font-weight: 600;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-size: 11px;
}

.operation-name {
  font-size: 10px;
  opacity: 0.9;
  white-space: nowrap;
  overflow: hidden;
  text-overflow: ellipsis;
  font-weight: 500;
}

.operation-progress {
  background: rgba(255, 255, 255, 0.2);
  border-radius: 2px;
  height: 4px;
  overflow: hidden;
}

.progress-bar {
  background: rgba(255, 255, 255, 0.8);
  height: 100%;
  transition: width 0.3s ease;
  border-radius: 2px;
}

.operation-modal-overlay {
  position: fixed;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: rgba(0, 0, 0, 0.7);
  display: flex;
  align-items: center;
  justify-content: center;
  z-index: 1000;
}

.operation-modal {
  background: linear-gradient(135deg, #1e293b 0%, #334155 100%);
  border: 1px solid #475569;
  border-radius: 12px;
  width: 90%;
  max-width: 500px;
  max-height: 80vh;
  overflow-y: auto;
  box-shadow: 0 20px 60px rgba(0, 0, 0, 0.5);
}

.modal-header {
  padding: 20px;
  border-bottom: 1px solid #475569;
  display: flex;
  justify-content: space-between;
  align-items: center;
}

.modal-header h3 {
  color: #f1f5f9;
  margin: 0;
}

.close-btn {
  background: none;
  border: none;
  color: #94a3b8;
  font-size: 18px;
  cursor: pointer;
  padding: 4px;
  border-radius: 4px;
  transition: color 0.2s ease;
}

.close-btn:hover {
  color: #fc913a;
}

.modal-body {
  padding: 20px;
  display: flex;
  flex-direction: column;
  gap: 16px;
}

.detail-group {
  display: flex;
  justify-content: space-between;
  align-items: center;
  padding: 8px 0;
  border-bottom: 1px solid #475569;
}

.detail-group label {
  font-weight: 600;
  color: #fc913a;
}

.detail-group span {
  color: #f1f5f9;
  text-align: right;
}

.modal-actions {
  padding: 20px;
  border-top: 1px solid #475569;
  display: flex;
  gap: 12px;
  justify-content: flex-end;
}

/* Responsive design */
@media (max-width: 768px) {
  .scheduler-container {
    min-height: 400px;
  }
  
  .machine-column {
    width: 200px;
  }
  
  .time-slot {
    min-width: 80px;
  }
  
  .operation-modal {
    width: 95%;
    margin: 10px;
  }
}
</style>
