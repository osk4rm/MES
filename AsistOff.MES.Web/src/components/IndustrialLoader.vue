<template>
  <div class="industrial-loader-wrapper">
    <div class="industrial-loader">
      <div class="cogs-container">
        <i class="pi pi-cog cog cog1"></i>
        <i class="pi pi-cog cog cog2"></i>
        <i class="pi pi-cog cog cog3"></i>
      </div>
      <div class="loader-text-container">
        <div class="loader-text">Loading AsistOff MES</div>
        <div class="loader-dots">
          <span class="dot"></span>
          <span class="dot"></span>
          <span class="dot"></span>
        </div>
      </div>
      <div class="progress-bar">
        <div class="progress-fill"></div>
      </div>
    </div>
  </div>
</template>

<script setup lang="ts">
</script>

<style scoped>
.industrial-loader-wrapper {
  display: flex;
  align-items: center;
  justify-content: center;
  min-height: 100vh;
  background: linear-gradient(135deg, #1a1d21 0%, #2c2f33 100%);
  position: relative;
  overflow: hidden;
}

.industrial-loader-wrapper::before {
  content: '';
  position: absolute;
  top: 0;
  left: 0;
  right: 0;
  bottom: 0;
  background: radial-gradient(circle at 50% 50%, rgba(100, 116, 139, 0.1) 0%, transparent 70%);
}

.industrial-loader {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 2.5rem;
  z-index: 1;
  position: relative;
}

.cogs-container {
  display: flex;
  align-items: center;
  gap: 1rem;
  position: relative;
}

.cog {
  font-size: 3.5rem;
  color: #64748b;
  margin: 0 0.5rem;
  animation: spin 2s linear infinite;
  filter: drop-shadow(0 4px 12px rgba(100, 116, 139, 0.3));
  transition: all 0.3s ease;
}

.cog1 {
  animation-delay: 0s;
  color: #8b949e;
}

.cog2 {
  color: #7c3aed;
  animation-delay: 0.3s;
  font-size: 4rem;
  filter: drop-shadow(0 6px 16px rgba(124, 58, 237, 0.4));
}

.cog3 {
  color: #fc913a;
  animation-delay: 0.6s;
  font-size: 3rem;
}

.loader-text-container {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 1rem;
}

.loader-text {
  color: #cbd5e1;
  font-size: 1.75rem;
  font-weight: 600;
  letter-spacing: 3px;
  font-family: 'Orbitron', 'Roboto Mono', monospace;
  text-shadow: 0 2px 12px rgba(203, 213, 225, 0.2);
  background: linear-gradient(135deg, #e2e8f0 0%, #94a3b8 100%);
  background-clip: text;
  -webkit-background-clip: text;
  -webkit-text-fill-color: transparent;
  animation: textGlow 2s ease-in-out infinite alternate;
}

.loader-dots {
  display: flex;
  gap: 0.5rem;
  align-items: center;
}

.dot {
  width: 8px;
  height: 8px;
  border-radius: 50%;
  background: #64748b;
  animation: dotPulse 1.5s ease-in-out infinite;
}

.dot:nth-child(1) { animation-delay: 0s; }
.dot:nth-child(2) { animation-delay: 0.3s; }
.dot:nth-child(3) { animation-delay: 0.6s; }

.progress-bar {
  width: 280px;
  height: 4px;
  background: rgba(100, 116, 139, 0.2);
  border-radius: 2px;
  overflow: hidden;
  position: relative;
}

.progress-fill {
  height: 100%;
  background: linear-gradient(135deg, #fc913a 0%, #f9d423 100%);
  border-radius: 2px;
  animation: progressFill 3s ease-in-out infinite;
  box-shadow: 0 0 12px rgba(99, 102, 241, 0.4);
}

@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}

@keyframes textGlow {
  0% {
    filter: drop-shadow(0 0 5px rgba(203, 213, 225, 0.3));
  }
  100% {
    filter: drop-shadow(0 0 20px rgba(203, 213, 225, 0.5));
  }
}

@keyframes dotPulse {
  0%, 80%, 100% {
    transform: scale(0.8);
    opacity: 0.5;
  }
  40% {
    transform: scale(1.2);
    opacity: 1;
  }
}

@keyframes progressFill {
  0% {
    width: 0%;
    transform: translateX(-100%);
  }
  50% {
    width: 70%;
    transform: translateX(0);
  }
  100% {
    width: 100%;
    transform: translateX(0);
  }
}

/* Mobile responsive */
@media (max-width: 768px) {
  .cog {
    font-size: 2.5rem;
  }
  
  .cog2 {
    font-size: 3rem;
  }
  
  .cog3 {
    font-size: 2rem;
  }
  
  .loader-text {
    font-size: 1.25rem;
    letter-spacing: 2px;
  }
  
  .progress-bar {
    width: 200px;
  }
}
</style>
