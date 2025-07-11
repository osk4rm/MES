<template>
  <div class="login-bg">
    <div class="login-container">
      <div class="login-header">
        <i class="pi pi-cog login-cog"></i>
        <span class="login-title">AsistOff MES</span>
      </div>
      <form class="login-form" @submit.prevent="onLogin">
        <div class="login-field">
          <CustomInput
            id="username"
            label="Username"
            v-model="username"
            required
          />
        </div>
        <div class="login-field">
          <CustomInput
            id="password"
            label="Password"
            type="password"
            v-model="password"
            required
          />
        </div>
        <button type="submit" class="p-button p-component login-btn">Login</button>
        <button type="button" class="register-link" @click="goRegister">Register</button>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import CustomInput from '../components/CustomInput.vue';
import { showInfoToast } from '../toast';

const username = ref('')
const password = ref('')
const router = useRouter()

function onLogin() {
  // TODO: Replace with real authentication
  showInfoToast(`Logging in as ${username.value}`)
}

function goRegister() {
  router.push('/register')
}
</script>

<style scoped>
.login-cog {
  font-size: 2.5rem;
  color: #f9d423;
  animation: spin 2.5s linear infinite;
}
@keyframes spin {
  0% { transform: rotate(0deg); }
  100% { transform: rotate(360deg); }
}
</style>
