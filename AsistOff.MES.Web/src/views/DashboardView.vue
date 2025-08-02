<template>
  <div class="mes-dashboard">
    <h1>MES Dashboard</h1>
    <div class="mes-welcome">
      <div class="welcome-content">
        <h2>Welcome to <span class="brand-highlight">AsistOff MES</span></h2>
        <p>Monitor, control, and optimize your manufacturing operations in real time.</p>
        <div class="welcome-features">
          <div class="feature">
            <i class="pi pi-check-circle"></i>
            <span>Real-time monitoring</span>
          </div>
          <div class="feature">
            <i class="pi pi-check-circle"></i>
            <span>Advanced analytics</span>
          </div>
          <div class="feature">
            <i class="pi pi-check-circle"></i>
            <span>Production optimization</span>
          </div>
        </div>
      </div>
      <div class="welcome-visual">
        <div class="floating-icon">
          <i class="pi pi-cog"></i>
        </div>
      </div>
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
    min-height: 100vh;
    background: linear-gradient(135deg, #23272b 0%, #232526 100%);
  }
  
  .mes-dashboard h1 {
    font-size: 2.2rem;
    margin-bottom: 1.5rem;
    color: #f9d423;
    font-family: 'Orbitron', 'Roboto Mono', monospace;
  }
  
  .mes-welcome {
    margin-bottom: 3rem;
    display: flex;
    align-items: center;
    gap: 3rem;
    padding: 2rem;
    background: linear-gradient(135deg, rgba(252, 145, 58, 0.1) 0%, rgba(249, 212, 35, 0.1) 100%);
    border-radius: 16px;
    border: 1px solid rgba(252, 145, 58, 0.2);
    position: relative;
    overflow: hidden;
  }
  
  .mes-welcome::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    bottom: 0;
    background: linear-gradient(45deg, transparent 40%, rgba(252, 145, 58, 0.05) 50%, transparent 60%);
    animation: shimmer 3s ease-in-out infinite;
  }
  
  .welcome-content {
    flex: 1;
    z-index: 1;
    position: relative;
  }
  
  .welcome-content h2 {
    font-size: 2.5rem;
    margin-bottom: 1rem;
    color: #ffe066;
    font-weight: 700;
    line-height: 1.2;
  }
  
  .brand-highlight {
    background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
    background-clip: text;
    -webkit-background-clip: text;
    -webkit-text-fill-color: transparent;
    font-weight: 800;
  }
  
  .welcome-content p {
    font-size: 1.1rem;
    margin-bottom: 1.5rem;
    color: rgba(255, 224, 102, 0.8);
    line-height: 1.6;
  }
  
  .welcome-features {
    display: flex;
    flex-direction: column;
    gap: 0.75rem;
  }
  
  .feature {
    display: flex;
    align-items: center;
    gap: 0.75rem;
    color: rgba(255, 224, 102, 0.9);
    font-size: 0.95rem;
  }
  
  .feature i {
    color: #fc913a;
    font-size: 1rem;
  }
  
  .welcome-visual {
    display: flex;
    align-items: center;
    justify-content: center;
    z-index: 1;
    position: relative;
  }
  
  .floating-icon {
    width: 120px;
    height: 120px;
    border-radius: 50%;
    background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
    display: flex;
    align-items: center;
    justify-content: center;
    color: #23272b;
    font-size: 3rem;
    animation: float 3s ease-in-out infinite;
    box-shadow: 0 10px 30px rgba(252, 145, 58, 0.4);
  }
  
  .mes-widgets {
    display: grid;
    grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
    gap: 2rem;
  }
  
  .mes-widget {
    background: linear-gradient(135deg, rgba(35, 39, 43, 0.9) 0%, rgba(35, 37, 38, 0.9) 100%);
    border-radius: 16px;
    box-shadow: 0 8px 32px rgba(0, 0, 0, 0.3);
    padding: 2rem;
    display: flex;
    flex-direction: column;
    align-items: center;
    color: #ffe066;
    font-size: 1.2rem;
    gap: 1rem;
    border: 1px solid rgba(252, 145, 58, 0.1);
    transition: all 0.3s ease;
    position: relative;
    overflow: hidden;
  }
  
  .mes-widget::before {
    content: '';
    position: absolute;
    top: 0;
    left: 0;
    right: 0;
    height: 3px;
    background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  }
  
  .mes-widget:hover {
    transform: translateY(-5px);
    box-shadow: 0 15px 40px rgba(252, 145, 58, 0.2);
    border-color: rgba(252, 145, 58, 0.3);
  }
  
  .mes-widget i {
    font-size: 2.5rem;
    color: #fc913a;
    margin-bottom: 0.5rem;
  }
  
  @keyframes shimmer {
    0% { transform: translateX(-100%); }
    100% { transform: translateX(100%); }
  }
  
  @keyframes float {
    0%, 100% { transform: translateY(0px); }
    50% { transform: translateY(-10px); }
  }
  
  /* Mobile responsive */
  @media (max-width: 768px) {
    .mes-welcome {
      flex-direction: column;
      text-align: center;
      gap: 2rem;
    }
    
    .welcome-content h2 {
      font-size: 2rem;
    }
    
    .floating-icon {
      width: 80px;
      height: 80px;
      font-size: 2rem;
    }
    
    .mes-widgets {
      grid-template-columns: 1fr;
    }
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
