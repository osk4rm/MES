import { createApp } from 'vue'
import App from './App.vue'
import './style.css'
import 'primeicons/primeicons.css';

import PrimeVue from 'primevue/config';
import InputText from 'primevue/inputtext';
import FloatLabel from 'primevue/floatlabel';
import Toast from 'vue-toastification';
import 'vue-toastification/dist/index.css';
import router from './router';

const app = createApp(App)
app.use(router)
app.use(PrimeVue)
app.component('InputText', InputText)
app.component('FloatLabel', FloatLabel)
app.use(Toast, {
  position: 'top-right',
  timeout: 3000,
  closeOnClick: true,
  pauseOnHover: true,
  draggable: true,
  showCloseButtonOnHover: false,
  hideProgressBar: false,
  closeButton: 'button',
  icon: true,
  rtl: false
})
app.mount('#app')
