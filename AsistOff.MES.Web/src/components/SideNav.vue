<template>
  <aside class="mes-sidemap">
    <MesLogo icon="pi pi-cog" label="AsistOff MES" />
    <nav class="mes-nav">
      <ul>
        <li v-for="item in sitemap" :key="item.label">
          <template v-if="!item.children">
            <router-link :to="item.route || '/'" class="nav-parent">
              <i :class="item.icon"></i> {{ item.label }}
            </router-link>
          </template>
          <template v-else>
            <div class="nav-parent" @click="toggleConfig" :class="{ expanded: configOpen }">
              <i :class="item.icon"></i> {{ item.label }}
              <i :class="['pi', configOpen ? 'pi-angle-down' : 'pi-angle-right', 'nav-arrow']"></i>
            </div>
            <transition name="fade">
              <ul v-if="configOpen" class="nav-sub">
                <li v-for="sub in item.children" :key="sub.label">
                  <router-link :to="sub.route || '/'" class="nav-parent">
                    <i :class="sub.icon"></i> {{ sub.label }}
                  </router-link>
                </li>
              </ul>
            </transition>
          </template>
        </li>
      </ul>
    </nav>
  </aside>
</template>

<script setup lang="ts">
import { ref } from 'vue';
import MesLogo from './MesLogo.vue';
import { sitemap } from '../sitemap';
const configOpen = ref(false);
function toggleConfig() {
  configOpen.value = !configOpen.value;
}
</script>

<style scoped>
.mes-nav {
  width: 100%;
}

.mes-nav > ul {
  list-style: none;
  padding: 0;
  margin: 0;
  width: 100%;
  display: flex;
  flex-direction: column;
}

.nav-parent {
  display: flex;
  align-items: center;
  gap: 0.7rem;
  cursor: pointer;
  user-select: none;
  position: relative;
  width: 100%;
  box-sizing: border-box;
  margin-bottom: 0.7rem;
  color: #ffe066;
  text-decoration: none;
  font-family: inherit;
  font-size: inherit;
  padding: 0.8rem 1rem;
  border-radius: 12px;
  background: rgba(35, 39, 43, 0.6);
  transition: all 0.2s ease-in-out;
}

.nav-parent:hover, .nav-parent.router-link-active {
  background: linear-gradient(90deg, rgba(252, 145, 58, 0.15) 0%, rgba(249, 212, 35, 0.15) 100%);
  color: #fc913a;
  box-shadow: 
    0 4px 12px rgba(0, 0, 0, 0.2),
    0 0 0 1px rgba(252, 145, 58, 0.3);
  transform: translateY(-1px);
}

.nav-parent.router-link-active {
  background: linear-gradient(90deg, rgba(252, 145, 58, 0.25) 0%, rgba(249, 212, 35, 0.25) 100%);
  border-color: rgba(252, 145, 58, 0.4);
}
.nav-parent .nav-arrow {
  margin-left: auto;
  transition: transform 0.18s;
}
.nav-parent.expanded .nav-arrow {
  transform: rotate(0deg);
}

.nav-sub {
  list-style: none;
  margin: 0;
  padding: 0;
  width: 100%;
  position: static;
  animation: fadeIn 0.2s;
  display: flex;
  flex-direction: column;
}

.nav-sub .nav-parent {
  padding-left: 2.2rem;
  box-shadow: none;
  margin-bottom: 0;
}

.nav-sub .nav-parent:hover,
.nav-sub .nav-parent.router-link-active {
  background: linear-gradient(90deg, rgba(252, 145, 58, 0.15) 0%, rgba(249, 212, 35, 0.15) 100%);
  color: #fc913a;
  box-shadow: 0 4px 12px rgba(0, 0, 0, 0.2), 0 0 0 1px rgba(252, 145, 58, 0.3);
  transform: translateY(-1px);
}

.fade-enter-active, .fade-leave-active {
  transition: opacity 0.18s;
}
.fade-enter-from, .fade-leave-to {
  opacity: 0;
}

.mes-sidemap {
  width: 280px;
  background: linear-gradient(135deg, #23272b 70%, #232526 100%);
  color: #ffe066;
  box-shadow: 
    0 4px 32px rgba(0, 0, 0, 0.67),
    0 0 0 2px #fc913a,
    inset 0 2px 12px rgba(252, 145, 58, 0.1);
  display: flex;
  flex-direction: column;
  align-items: center;
  padding: 2.5rem 1.2rem 2rem 1.2rem;
  border: 2px solid #fc913a;
  backdrop-filter: blur(8px);
}
</style>