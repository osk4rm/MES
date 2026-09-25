import { createApp } from 'vue';
import { createPinia } from 'pinia';
import App from './App.vue';
import router from './router';
import i18n from './i18n';

import './style.css';
import 'primeicons/primeicons.css';

const app = createApp(App);
const pinia = createPinia();
app.use(pinia);
app.use(router);
app.use(i18n);

// Cookie session (issue #242): auth state is in-memory only, so there is
// nothing to load here. The router guard re-proves the httpOnly cookie
// session via POST /api/auth/refresh on the first protected navigation
// after a reload.

app.mount('#app');
