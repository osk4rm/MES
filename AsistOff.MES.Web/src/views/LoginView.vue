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
            :label="$t('login.username')"
            v-model="username"
            required
          />
        </div>
        <div class="login-field">
          <CustomInput
            id="password"
            :label="$t('login.password')"
            type="password"
            v-model="password"
            required
          />
        </div>
        <button type="submit" class="p-button p-component login-btn">{{ $t('login.submit') }}</button>
        <button type="button" class="register-link" @click="goRegister">{{ $t('login.register') }}</button>
      </form>
    </div>
  </div>
</template>

<script setup lang="ts">
import { ref } from 'vue'
import { useRouter } from 'vue-router'
import CustomInput from '../components/CustomInput.vue';
import { showSuccessToast, showErrorToast } from '../toast';
import { signIn } from '../services/authService';
import { useAuthStore } from '../stores/authStore';

const username = ref('')
const password = ref('')
const router = useRouter()
const authStore = useAuthStore()

async function onLogin() {
  try {
    const response = await signIn({ username: username.value, password: password.value });
    authStore.setAuth(response.accessToken, null);
    showSuccessToast('Login successful!');
    // TODO: Redirect, etc.
  } catch (error: any) {
    showErrorToast(error?.response?.data?.message || 'Login failed');
  }
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
