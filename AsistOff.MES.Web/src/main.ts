import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import router from './router';
import i18n from './i18n';
import { useAuthStore } from './stores/authStore';

import './style.css';
import 'primeicons/primeicons.css';

const app = createApp(App);
const pinia = createPinia();
app.use(pinia);
app.use(router);
app.use(i18n);

// Initialize auth state from localStorage before mounting
useAuthStore(pinia).loadAuth();

app.mount('#app');
