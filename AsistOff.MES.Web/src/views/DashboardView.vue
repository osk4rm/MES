<template>
  <div class="mes-dashboard">
    <h1>MES Dashboard</h1>
    <div class="mes-welcome">
      <h2>Welcome to the Industrial MES Platform</h2>
      <p>Monitor, control, and optimize your manufacturing operations in real time.</p>
    </div>
    <div class="mes-widgets">
      <div class="mes-widget">
        <i class="pi pi-box"></i>
        <span>Production Status</span>
      </div>
      <div class="mes-widget">
        <i class="pi pi-users"></i>
        <span>Operator Overview</span>
      </div>
      <div class="mes-widget">
        <i class="pi pi-chart-bar"></i>
        <span>Performance Metrics</span>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
  .mes-dashboard {
    padding: 2.5rem 2rem;
    color: #ffe066;
  }
  .mes-dashboard h1 {
    font-size: 2.2rem;
    margin-bottom: 1.5rem;
    color: #f9d423;
    font-family: 'Orbitron', 'Roboto Mono', monospace;
  }
  .mes-welcome {
    margin-bottom: 2rem;
  }
  .mes-widgets {
    display: flex;
    gap: 2rem;
  }
  .mes-widget {
    background: #23272b;
    border-radius: 12px;
    box-shadow: 0 2px 12px #0006;
    padding: 1.2rem 2rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    min-width: 180px;
    color: #ffe066;
    font-size: 1.2rem;
    gap: 0.7rem;
  }
:global(#app) {
  width: 100vw !important;
  height: 100vh !important;
  margin: 0 !important;
  padding: 0 !important;
  max-width: 100vw !important;
  box-sizing: border-box !important;
  text-align: initial !important;
}
.mes-layout {
  display: flex;
  height: 100vh;
  width: 100vw;
  background: radial-gradient(ellipse at 60% 40%, #2c3136 60%, #232526 100%);
  font-family: 'Orbitron', 'Roboto Mono', monospace;
}
.mes-sidemap {
  width: 240px;
  background: linear-gradient(180deg, #23272b 80%, #232526 100%);
  color: #ffe066;
  box-shadow: 4px 0 24px #000a;
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 2.5rem 1.2rem 2rem 1.2rem;
  border-right: 1px solid #444;
}
.mes-logo {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  font-size: 1.5rem;
  margin-bottom: 2.5rem;
  color: #f9d423;
  font-weight: bold;
  letter-spacing: 2px;
}
.mes-nav ul {
  list-style: none;
  padding: 0;
  margin: 0;
  width: 100%;
}
.mes-nav li {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  padding: 1rem 0.7rem;
  font-size: 1.13rem;
  cursor: pointer;
  border-radius: 8px;
  font-family: 'Roboto Mono', monospace;
  font-weight: 500;
  letter-spacing: 1px;
  transition: background 0.18s, color 0.18s, box-shadow 0.18s;
}
.mes-nav li:hover {
  background: linear-gradient(90deg, #fc913a22 0%, #f9d42322 100%);
  color: #fc913a;
  box-shadow: 0 2px 8px #0003;
}
.mes-main {
  flex: 1;
  display: flex;
  flex-direction: column;
}
.mes-header {
  display: flex;
  justify-content: space-between;
  align-items: center;
  background: linear-gradient(90deg, #23272b 80%, #232526 100%);
  color: #ffe066;
  padding: 1.2rem 2.2rem 1.2rem 2.2rem;
  box-shadow: 0 2px 16px #0005;
  border-bottom: 1px solid #444;
}
.mes-header h1 {
  font-size: 2.1rem;
  letter-spacing: 2.5px;
  font-family: 'Orbitron', 'Roboto Mono', monospace;
  font-weight: bold;
}
.mes-header-actions {
  display: flex;
  align-items: center;
  gap: 1rem;
}
.mes-logout {
  background: none;
  border: none;
  color: #ff4e50;
  font-size: 1.08rem;
  cursor: pointer;
  display: flex;
  align-items: center;
  gap: 0.5rem;
  font-family: 'Roboto Mono', monospace;
  font-weight: 500;
  letter-spacing: 1px;
  transition: color 0.18s;
}
.mes-logout:hover {
  color: #fc913a;
}
.mes-content {
  flex: 1;
  padding: 2.5rem 2.5rem 2.5rem 2.5rem;
  display: flex;
  flex-direction: column;
  gap: 2.2rem;
}
.mes-welcome {
  background: linear-gradient(90deg, #23272b 80%, #232526 100%);
  color: #fff;
  border-radius: 16px;
  box-shadow: 0 4px 24px #000a;
  padding: 2.2rem 2.5rem;
  text-align: center;
  border: 1px solid #444;
}
.mes-widgets {
  display: flex;
  gap: 2.5rem;
  justify-content: center;
}
.mes-widget {
  background: linear-gradient(135deg, #23272b 80%, #232526 100%);
  color: #ffe066;
  border-radius: 16px;
  box-shadow: 0 4px 24px #000a;
  padding: 2.2rem 2rem;
  min-width: 200px;
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1.2rem;
  font-size: 1.13rem;
  font-family: 'Roboto Mono', monospace;
  font-weight: 500;
  letter-spacing: 1px;
  border: 1px solid #444;
  transition: background 0.18s, color 0.18s, box-shadow 0.18s;
}
.mes-widget:hover {
  background: linear-gradient(90deg, #fc913a22 0%, #f9d42322 100%);
  color: #fc913a;
  box-shadow: 0 4px 32px #000a;
}
</style>
